"""Flask-based remote client producer server.

This preserves the same CLI contract as the previous script:
  python server.py <controller> <client_path> [node_id] [--port PORT]

On startup the app will POST to the controller /subscribe_client endpoint with
JSON {"nodeID": <node_id or uuid>, "addresses": ["http://<ip>:<port>", ...]} where
addresses contains all non-loopback IPv4 addresses.

Endpoints:
- GET / -> health
- POST /upload -> multipart/form-data 'file' and optional 'dir' (relative under client_path)
- Static files are served under / (Flask static folder configured to client_path)
"""

from pathlib import Path
import argparse
import socket
import uuid
import urllib.request
import urllib.parse
import json
import sys
import os
import zipfile
import re
import shutil
import subprocess
import datetime
import time
from flask import Flask, request, jsonify, send_from_directory, abort

BASE_UPLOAD_DIR = Path(__file__).parent / "uploads"
LOGS_DIR = Path(__file__).parent / "logs"
ARCHIVE_DIR = Path(__file__).parent / "archived_logs"


def parse_controller(controller: str):
    if not controller.startswith("http://") and not controller.startswith("https://"):
        controller = "http://" + controller
    parsed = urllib.parse.urlparse(controller)
    host = parsed.hostname
    port = parsed.port or (443 if parsed.scheme == "https" else 8000)
    base = f"{parsed.scheme}://{host}:{port}"
    return parsed.scheme, host, port, base

def post_subscription(controller_base: str, node_id: str, address: str):
    url = urllib.parse.urljoin(controller_base, "/subscribe_provider")
    payload = json.dumps({"providerKey": node_id, "address": address}).encode("utf-8")
    req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=5) as resp:
            body = resp.read().decode("utf-8")
            print(f"Controller response ({resp.status}): {body}")
            return resp.status, body
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8") if hasattr(e, 'read') else ''
        print(f"Controller returned HTTP error {e.code}: {body}")
        return e.code, body
    except Exception as e:
        print(f"Failed to contact controller at {url}: {e}")
        return None, str(e)


def create_app(client_path: Path, pport: int, controller_base: str, node_id: str, address: str):
    app = Flask(__name__, static_url_path="")
    # Keep track of started client subprocesses for this server process
    started_processes: list[dict] = []

    @app.route("/", methods=["GET"])
    def health():
        return jsonify({"status": "ok", "providerKey": node_id, "address": address})

    @app.route("/start_provider", methods=["POST"])
    def start_provider():
        data = None
        try:
            data = request.get_json(force=True)
        except Exception:
            return jsonify({"error": "Invalid or missing JSON body"}), 400

        required = ["providerKey", "providerType", "managerIP"]
        missing = [k for k in required if k not in data]
        if missing:
            return jsonify({"error": f"Missing parameters: {', '.join(missing)}"}), 400

        ip_filter = data.get("ipFilter")
        provider_key = str(data.get("providerKey"))
        provider_type = str(data.get("providerType"))
        manager_ip = str(data.get("managerIP"))
        # Strip out the port from the address variable
        provider_addr = address.split(":")[0]
        # Optional executable override
        exec_path = f"{client_path}"

        started = []
        if exec_path == sys.executable:
            cmd = [exec_path, sys.argv[0], "--managerIP", manager_ip, "--address", provider_addr, "--providerKey", provider_key, "--port", str(pport), "--ipFilter", ip_filter]
        else:
            cmd = [exec_path, "--managerIP", manager_ip, "--address", provider_addr, "--providerKey", provider_key, "--port", str(pport), "--ipFilter", ip_filter]

        try:
            # Start in background, discard stdout/stderr to avoid blocking
            p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            proc_info = {"pid": p.pid, "cmd": cmd, "providerType": provider_type, "providerKey": provider_key}
            started_processes.append({"pid": p.pid, "cmd": cmd, "started_at": time.time(), "providerType": provider_type, "providerKey": provider_key})
            started.append(proc_info)
            # small delay to avoid races when starting many processes at once
            print("Started process:", proc_info)
            time.sleep(0.05)
        except Exception as e:
            print(f"Failed to start process {cmd} {e}")
            return jsonify({"error": f"Failed to start process {exec_path}: {e}", "started": started}), 500

        return jsonify({"status": "started", "address": provider_addr, "port": pport})

    @app.route("/download_logs", methods=["GET"])
    def download_logs():
        """Create a zip of all files in the local `logs` directory, return it
        to the requester, and move the original log files to
        `archived_logs/<timestamp>/`.
        """
        try:
            logs_dir = LOGS_DIR
            if not logs_dir.exists() or not logs_dir.is_dir():
                return jsonify({"error": "Logs directory does not exist"}), 404

            files = [p for p in logs_dir.iterdir() if p.is_file()]
            if not files:
                return jsonify({"error": "No logs available"}), 404

            ts = datetime.datetime.utcnow().strftime("%Y%m%dT%H%M%SZ")
            archive_subdir = ARCHIVE_DIR / ts
            archive_subdir.mkdir(parents=True, exist_ok=True)

            # Create zip inside the archive directory so it remains available
            # while we move the original files into the same archive folder.
            zip_name = f"logs_{ts}.zip"
            zip_path = archive_subdir / zip_name
            with zipfile.ZipFile(str(zip_path), 'w', compression=zipfile.ZIP_DEFLATED) as zf:
                for f in files:
                    # store only the basename in the archive
                    zf.write(str(f), arcname=f.name)

            # Move the original files into the archive folder
            for f in files:
                try:
                    # If moving into same directory as zip, keep original filename
                    shutil.move(str(f), str(archive_subdir / f.name))
                except Exception as e:
                    # Log and continue; do not fail the download because of one file
                    print(f"Failed to move {f} to archive: {e}")

            # Return the zip file as an attachment
            return send_from_directory(directory=str(archive_subdir), path=zip_name, as_attachment=True)
        except Exception as e:
            print(f"Error while preparing logs archive: {e}")
            return jsonify({"error": f"Internal server error: {e}"}), 500
    # Let Flask serve static files from client_path by letting the static route handle
    return app


def main(argv: list[str] | None = None):
    argv = argv if argv is not None else sys.argv[1:]
    parser = argparse.ArgumentParser(description="Remote client producer (Flask)")
    parser.add_argument("controller", help="Controller address (host, host:port, or URL).")
    parser.add_argument("provider_path", help="Path to the provider application directory to serve.")
    parser.add_argument("provider_key", nargs="?", default=None, help="Optional providerKey to advertise; if omitted a random UUID will be generated")
    parser.add_argument("--port", type=int, default=9002, help="Port to serve this app on (default: 9001)")
    parser.add_argument("--pport", type=int, default=9901, help="Port to serve the app on (default: 9901)")
    parser.add_argument("--addr", type=str, default="127.0.0.1", help="Address to serve the app on (default: 127.0.0.1)")
    args = parser.parse_args(argv)

    controller = args.controller
    provider_path = Path(args.provider_path).expanduser().resolve()
    port = args.port
    pport = args.pport
    if not provider_path.exists():
        print(f"provider_path does not exist: {provider_path}")
        sys.exit(2)

    scheme, ctrl_host, ctrl_port, ctrl_base = parse_controller(controller)

    node_id = args.provider_key or str(uuid.uuid4())
    address = f"{args.addr}:{port}"
    print(f"Subscribing to controller at {ctrl_base}/subscribe_provider")
    print(f"ProviderKey: {node_id}")
    print(f"Address: {address}")

    status, body = post_subscription(ctrl_base, node_id, address)
    if status is None:
        print("Warning: subscription failed, continuing to serve local client anyway.")
    elif status >= 400:
        print("Controller returned error; check controller logs and request payload.")

    app = create_app(provider_path, pport, ctrl_base, node_id, address)
    print(f"Starting Flask server serving '{provider_path}' on 0.0.0.0:{port} (advertised address: {address}:{pport})")
    app.run(host="0.0.0.0", port=port, debug=False)


if __name__ == "__main__":
    main()
