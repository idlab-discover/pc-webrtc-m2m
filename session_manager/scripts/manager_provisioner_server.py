from flask import Flask, request, jsonify
from pathlib import Path
import os
import sys
import argparse
import requests
import subprocess
from werkzeug.utils import secure_filename
import threading
import time
import datetime
import zipfile
import tempfile
import shutil

from config import parse_root, print_summary, validate_config


# Ensure repository root is on sys.path so sibling package 'shared' can be imported
_script_dir = Path(__file__).resolve().parent
_repo_root = _script_dir.parent
if str(_repo_root) not in sys.path:
    sys.path.insert(0, str(_repo_root))

app = Flask(__name__)


# Optional manager binary/path configured via CLI. Stored as a string or None.
manager_path: str | None = None
app.config["manager_path"] = None
app.config["manager_file"] = None
app.config["manager_process"] = None

@app.route("/", methods=["GET"])
def health():
    """Simple health check endpoint."""
    return jsonify({"status": "ok", "message": "server running"})


@app.route("/start", methods=["POST"])
def receive_json():
    """
    POST /json
    Expects: application/json body containing a JSON object (dictionary).
    Returns 400 if no valid JSON is provided.
    Returns 200 with an echo of the JSON on success.

    Contract:
    - Input: JSON object (maps to Python dict)
    - Output: { status: 'ok', received: <the object> } or { error: <message> }
    - Error modes: 400 for missing/invalid JSON or if JSON is not an object
    """
    data = request.get_json(silent=True)
    if data is None:
        print("No JSON received or invalid JSON")
        return jsonify({"error": "Invalid or missing JSON in request body"}), 400
    if not isinstance(data, dict):
        print("Received JSON is not an object")
        return jsonify({"error": "Expected a JSON object (dictionary)"}), 400
    
    # Try to parse and validate as Runner/Controller config using shared helpers
    try:
        cfg = parse_root(data)
        
    except Exception as e:
        print(f"Failed to parse JSON as config: {e}")
        return jsonify({"error": f"Failed to parse JSON as config: {e}"}), 400

    errs = validate_config(cfg)
    if errs:
        print(f"Config validation failed with errors: {errs}")
        return jsonify({"error": "config validation failed", "details": errs}), 400
    
    print_summary(cfg)

    # START SESSION MANAGER HERE
    # If a manager binary and config path were provided via CLI, start the
    # manager in the background (pass the config using -c <config_path>).
    manager_started_info = None
    mgr_path = app.config.get("manager_path")
    mgr_file = app.config.get("manager_file")
    full_mgr_path = None
    if mgr_path is None:
        mgr_path = ""
    if mgr_file:
        full_mgr_path = os.path.join(mgr_path, mgr_file)
    elif mgr_path:
        full_mgr_path = mgr_path
    cfg_path = f"{mgr_path}/config/temp_config.json" if mgr_path else "config/temp_config.json"
    # Save received JSON to cfg_path
    with open(cfg_path, "w", encoding="utf-8") as fh:
        import json
        json.dump(data, fh, indent=2)
    try:
        # Start the manager process in the foreground so its stdout/stderr are
        # visible in this process. We still do not wait for it to finish;
        # store the Popen handle so other parts of the app can inspect/terminate it.
        # iF process is already running, close it first
        existing_process = app.config.get("manager_process")
        if existing_process is not None:
            print(f"Terminating existing manager process (pid={existing_process.pid})")
            existing_process.terminate()
            existing_process.wait(timeout=5)
        popen = subprocess.Popen([full_mgr_path, "-c", cfg_path], stdout=None, stderr=None)
        app.config["manager_process"] = popen
        print(f"Started manager process in foreground (pid={popen.pid}): {mgr_path} -c {cfg_path}")
        manager_started_info = {"started": True, "manager_path": mgr_path, "config_path": cfg_path}
    except Exception as e:
        print(f"Failed to start manager process {mgr_path} -c {cfg_path}: {e}")
        manager_started_info = {"started": False, "reason": str(e)}
        resp_body = {"status": "error", "error": f"failed to start manager process: {e}"}
        return jsonify(resp_body), 400
    
    
    time.sleep(2)
    
    resp_body = {"status": "ok", "received": data, "message": "valid configuration"}
    return jsonify(resp_body), 200

if __name__ == "__main__":
    # Default host/port for local testing; change as needed
    parser = argparse.ArgumentParser(description="Controller server")
    parser.add_argument("--manager_path", dest="manager_path", help="Path to the server manager directory", default=None)
    parser.add_argument("--file_name", dest="file_name", help="Name of the server manager file", default=None)
    parser.add_argument("--host", dest="host", help="Host to bind to", default="0.0.0.0")
    parser.add_argument("--port", dest="port", help="Port to bind to", type=int, default=8000)
    parser.add_argument("--debug", dest="debug", action="store_true", help="Run Flask in debug mode")
    args = parser.parse_args()

    # Store manager path in module-global and Flask config so endpoints can access it
    if args.manager_path:
        manager_path = str(Path(args.manager_path))
    else:
        manager_path = ""
    app.config["manager_path"] = manager_path
    print(f"Configured manager_path: {manager_path}")
    if args.file_name:
        file_name = str(Path(args.file_name))
        app.config["manager_file"] = file_name
        print(f"Configured manager_file: {file_name}")

    app.run(host=args.host, port=args.port, debug=args.debug)
# e.g.  python3 ./scripts/manager_provisioner_server.py --port 9999 --manager_path /home/matthias/Documents/pc-webrtc-m2m/session_manager --file_name session_manager_linux.exe