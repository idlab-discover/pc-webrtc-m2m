#!/usr/bin/env python3
import os
import json
import io
import shutil
import zipfile
import subprocess
import threading
import argparse
import time
from datetime import datetime
from flask import Flask, request, jsonify, send_file
import requests

app = Flask(__name__)

# ── Config filled from CLI args ────────────────────────────────────────────────
instance_controller_address = None
content_path                = None
default_log_dir             = None
provisioner_address         = None
provisioner_id              = None
verbose_subprocess          = False

# ── Runtime state ──────────────────────────────────────────────────────────────
running_process = None
process_lock    = threading.Lock()


# ── Registration ───────────────────────────────────────────────────────────────

def _connect_to_instance_controller():
    """Scan content_path for subdirectories, send their names + file counts."""
    directories = []
    if content_path and os.path.isdir(content_path):
        for entry in os.scandir(content_path):
            if entry.is_dir():
                count = sum(1 for f in os.scandir(entry.path) if f.is_file())
                directories.append({"name": entry.name, "file_count": count})

    payload = {
        "id":          provisioner_id,
        "address":     provisioner_address,
        "directories": directories,
    }
    try:
        r = requests.post(
            f"{instance_controller_address}/connect",
            json=payload,
            timeout=10,
        )
        print(f"[ic] Connected: {r.status_code}")
    except Exception as e:
        print(f"[ic] Connect failed: {e}")


# ── Subprocess output streaming ─────────────────────────────────────────────────

def _stream_output(stream, prefix):
    for line in stream:
        print(f"[{prefix}] {line}", end="", flush=True)


# ── Endpoints ──────────────────────────────────────────────────────────────────

@app.route("/start", methods=["POST"])
def start():
    """Start the client application in the background."""
    global running_process

    data = request.json or {}
    app_path = data.get("path", "")
    app_args = data.get("args", [])

    if not app_path:
        return jsonify({"status": "error", "message": "path is required"}), 400

    if isinstance(app_args, str):
        app_args = app_args.split()

    with process_lock:
        if running_process and running_process.poll() is None:
            return jsonify({"status": "error", "message": "Application already running"}), 409

        try:
            cmd = [app_path] + app_args
            if verbose_subprocess:
                running_process = subprocess.Popen(
                    cmd,
                    stdout=subprocess.PIPE,
                    stderr=subprocess.PIPE,
                    text=True,
                )
                threading.Thread(target=_stream_output, args=(running_process.stdout, "app:out"), daemon=True).start()
                threading.Thread(target=_stream_output, args=(running_process.stderr, "app:err"), daemon=True).start()
            else:
                running_process = subprocess.Popen(
                    cmd,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                )
            print(f"[app] Started PID {running_process.pid}: {' '.join(cmd)}")
            return jsonify({"status": "ok", "pid": running_process.pid})
        except Exception as e:
            return jsonify({"status": "error", "message": str(e)}), 500


@app.route("/quit", methods=["POST"])
def quit_app():
    """Stop the running client application."""
    global running_process

    with process_lock:
        if not running_process or running_process.poll() is not None:
            running_process = None
            return jsonify({"status": "not_running"}), 200

        running_process.terminate()
        try:
            running_process.wait(timeout=5)
            print(f"[app] PID {running_process.pid} terminated")
            running_process = None
            return jsonify({"status": "ended"}), 200
        except subprocess.TimeoutExpired:
            running_process.kill()
            running_process.wait()
            print(f"[app] PID {running_process.pid} killed")
            running_process = None
            return jsonify({"status": "killed"}), 200


@app.route("/upload_content", methods=["POST"])
def upload_content():
    """Upload files into a named subdirectory of content_path."""
    directory = request.form.get("directory")
    if not directory:
        return jsonify({"status": "error", "message": "directory is required"}), 400

    target_dir = os.path.join(content_path, directory)
    os.makedirs(target_dir, exist_ok=True)

    saved = []
    for key, f in request.files.items():
        if key == "archive" or (f.content_type and "zip" in f.content_type):
            with zipfile.ZipFile(io.BytesIO(f.read())) as zf:
                zf.extractall(target_dir)
            saved.append("<zip extracted>")
        else:
            dest = os.path.join(target_dir, os.path.basename(f.filename))
            f.save(dest)
            saved.append(os.path.basename(f.filename))

    return jsonify({"status": "ok", "saved": saved})


@app.route("/upload_config", methods=["POST"])
def upload_config():
    """Write a JSON config to an arbitrary path, creating parent dirs as needed."""
    data = request.json or {}
    upload_path = data.get("path")
    config_data = data.get("config")

    if not upload_path or config_data is None:
        return jsonify({"status": "error", "message": "path and config are required"}), 400

    try:
        os.makedirs(os.path.dirname(os.path.abspath(upload_path)), exist_ok=True)
        with open(upload_path, "w") as f:
            json.dump(config_data, f, indent=2)
        return jsonify({"status": "ok"})
    except Exception as e:
        return jsonify({"status": "error", "message": str(e)}), 500


@app.route("/download_logs", methods=["POST"])
def download_logs():
    """
    Zip all non-archived log files and return the archive.
    After sending, moves the files into an 'archived/<timestamp>' subdirectory.
    """
    data    = request.json or {}
    log_dir = data.get("path", default_log_dir)

    if not log_dir or not os.path.isdir(log_dir):
        return jsonify({"status": "error", "message": f"Log directory not found: {log_dir}"}), 404

    # Collect files, skip anything inside an 'archived' folder
    log_files = []
    for root, dirs, files in os.walk(log_dir):
        dirs[:] = [d for d in dirs if d != "archived"]
        for fname in files:
            log_files.append(os.path.join(root, fname))

    if not log_files:
        return jsonify({"status": "error", "message": "No log files to download"}), 404

    # Build zip in memory
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for fp in log_files:
            zf.write(fp, os.path.relpath(fp, log_dir))
    buf.seek(0)

    # Archive originals
    archive_dir = os.path.join(log_dir, "archived", datetime.now().strftime("%Y-%m-%d_%H-%M-%S"))
    os.makedirs(archive_dir, exist_ok=True)
    for fp in log_files:
        rel  = os.path.relpath(fp, log_dir)
        dest = os.path.join(archive_dir, rel)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.move(fp, dest)

    return send_file(
        buf,
        mimetype="application/zip",
        as_attachment=True,
        download_name=f"logs_{provisioner_id}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.zip",
    )


# ── Entry point ────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Client Provisioner")
    parser.add_argument("--instance-controller", required=True,
                        help="Instance Controller URL, e.g. http://192.168.1.101:5001")
    parser.add_argument("--content-path", required=True,
                        help="Base directory where content is stored / received")
    parser.add_argument("--log-dir", required=True,
                        help="Default directory where the client application writes logs")
    parser.add_argument("--address", required=True,
                        help="This provisioner's URL reachable from Instance Controller, e.g. http://192.168.1.102:5002")
    parser.add_argument("--id", required=True, dest="provisioner_id",
                        help="Unique identifier for this provisioner")
    parser.add_argument("--port", type=int, default=5002)
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--verbose-subprocess", action="store_true",
                        help="Print stdout/stderr of the client application to this terminal")
    args = parser.parse_args()

    instance_controller_address = args.instance_controller
    content_path                = args.content_path
    default_log_dir             = args.log_dir
    provisioner_address         = args.address
    provisioner_id              = args.provisioner_id
    verbose_subprocess          = args.verbose_subprocess

    os.makedirs(content_path,    exist_ok=True)
    os.makedirs(default_log_dir, exist_ok=True)

    threading.Thread(target=_connect_to_instance_controller, daemon=True).start()
    app.run(host=args.host, port=args.port, debug=False)
