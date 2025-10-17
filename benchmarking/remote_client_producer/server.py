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
import re
import shutil
import subprocess
import time
import zipfile
import datetime
import io
import tempfile
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


def discover_non_loopback_ips(remote_host: str, remote_port: int) -> list[str]:
    ips: set[str] = set()
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
            s.connect((remote_host, remote_port))
            ips.add(s.getsockname()[0])
    except Exception:
        pass

    try:
        hostname = socket.gethostname()
        for ip in socket.gethostbyname_ex(hostname)[2]:
            if not ip.startswith("127."):
                ips.add(ip)
    except Exception:
        pass

    try:
        for res in socket.getaddrinfo(socket.gethostname(), None, family=socket.AF_INET):
            ip = res[4][0]
            if not ip.startswith("127."):
                ips.add(ip)
    except Exception:
        pass

    if not ips:
        return ["127.0.0.1"]
    return sorted(ips)


def post_subscription(controller_base: str, node_id: str, addresses: list[str]):
    url = urllib.parse.urljoin(controller_base, "/subscribe_client")
    payload = json.dumps({"nodeID": node_id, "addresses": addresses}).encode("utf-8")
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


def _is_valid_relative_dir(rel_path: str) -> bool:
    if not rel_path:
        return True
    p = Path(rel_path)
    if p.is_absolute():
        return False
    if any(part == ".." for part in p.parts):
        return False
    if ":" in rel_path:
        return False
    if rel_path.startswith("/") or rel_path.startswith("\\"):
        return False
    return True


def create_app(client_path: Path, controller_base: str, node_id: str, addresses: list[str]):
    app = Flask(__name__, static_folder=str(client_path), static_url_path="")
    # Keep track of started client subprocesses for this server process
    started_processes: list[dict] = []

    @app.route("/", methods=["GET"])
    def health():
        return jsonify({"status": "ok", "nodeID": node_id, "addresses": addresses})

    @app.route("/upload", methods=["POST"])
    def upload():
        if 'file' not in request.files:
            return jsonify({"error": "No file part in the request"}), 400

        file = request.files['file']
        rel_dir = (request.form.get('dir') or '').strip()

        if not _is_valid_relative_dir(rel_dir):
            return jsonify({"error": "Invalid directory path; must be a relative path without traversal"}), 400

        base_dir = BASE_UPLOAD_DIR
        try:
            save_dir = (base_dir / rel_dir).resolve()
            base_resolved = base_dir.resolve()
        except Exception:
            base_resolved = base_dir

        if not str(save_dir).startswith(str(base_resolved)):
            return jsonify({"error": "Invalid directory path"}), 400

        save_dir.mkdir(parents=True, exist_ok=True)

        filename = file.filename or 'uploaded'
        filename = os.path.basename(filename)
        filename = re.sub(r"[^A-Za-z0-9_.-]", "_", filename)
        dest = save_dir / filename
        try:
            file.save(str(dest))
        except Exception as e:
            return jsonify({"error": f"Failed to save file: {e}"}), 500

        rel_saved = dest.relative_to(base_resolved)
        return jsonify({"status": "ok", "saved_path": str(rel_saved)})

    @app.route("/start_clients", methods=["POST"])
    def start_clients():
        """Start n client instances as subprocesses.

        Expected JSON body:
          {
            "nClients": int,
            "clientType": str,            # informational
            "transcoderConfig": str,
            "providerConfig": str,
            "transcoderType": str,
            "managerIP": str,
            # optional: "executable": str (path to executable to run). If omitted,
            # the current Python interpreter and this script are used.
          }
        The subprocesses are started with the following args appended:
          --manager {managerIP} --providers {providerConfig} --tr {transcoderType} --trcfg {transcoderConfig}
        """
        data = None
        try:
            data = request.get_json(force=True)
        except Exception:
            return jsonify({"error": "Invalid or missing JSON body"}), 400

        required = ["nClients", "clientType", "transcoderConfig", "providerConfig", "transcoderType", "managerIP"]
        missing = [k for k in required if k not in data]
        if missing:
            return jsonify({"error": f"Missing parameters: {', '.join(missing)}"}), 400

        try:
            n = int(data.get("nClients"))
        except Exception:
            return jsonify({"error": "nClients must be an integer"}), 400
        if n <= 0:
            return jsonify({"error": "nClients must be > 0"}), 400

        client_counter_start = int(data.get("clientCounterStart"))
        client_type = str(data.get("clientType"))
        tr_cfg = f"uploads/{str(data.get('transcoderConfig'))}"
        prov_cfg = f"uploads/{str(data.get('providerConfig'))}"
        tr_type = str(data.get("transcoderType"))
        manager_ip = str(data.get("managerIP"))

        # Optional executable override
        executable = "peer.exe"
        if executable:
            exec_path = f"{client_path}//{executable}"
        else:
            # Default: use the current Python interpreter and the same script file.
            # Assumption: the target client can be started this way. If a different
            # executable is needed, pass it via the optional `executable` field.
            exec_path = sys.executable

        started = []
        for i in range(n):
            # Build command. If exec_path is the Python interpreter, we append this
            # script path so the process runs this file. If exec_path is a direct
            # executable, use it as-is.
            if exec_path == sys.executable:
                cmd = [exec_path, sys.argv[0], "--manager", manager_ip, "--providers", prov_cfg, "--tr", tr_type, "--trcfg", tr_cfg, "-c", str(client_counter_start + i)]
            else:
                cmd = [exec_path, "--manager", manager_ip, "--providers", prov_cfg, "--tr", tr_type, "--trcfg", tr_cfg, "-c", str(client_counter_start + i)]

            try:
                # Start in background, discard stdout/stderr to avoid blocking
                p = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                proc_info = {"pid": p.pid, "cmd": cmd, "clientType": client_type}
                started_processes.append({"pid": p.pid, "cmd": cmd, "started_at": time.time(), "clientType": client_type})
                started.append(proc_info)
                # small delay to avoid races when starting many processes at once
                print("Started process:", proc_info)
                time.sleep(0.05)
            except Exception as e:
                print(f"Failed to start process {cmd}")
                return jsonify({"error": f"Failed to start process {exec_path}: {e}", "started": started}), 500

        return jsonify({"status": "started", "count": len(started), "processes": [{"pid": s["pid"], "cmd": " ".join(s["cmd"]), "clientType": s["clientType"]} for s in started]})

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
    parser.add_argument("client_path", help="Path to the client application directory to serve.")
    parser.add_argument("node_id", nargs="?", default=None, help="Optional nodeID to advertise; if omitted a random UUID will be generated")
    parser.add_argument("--port", type=int, default=8081, help="Port to serve the client on (default: 8081)")
    args = parser.parse_args(argv)

    controller = args.controller
    client_path = Path(args.client_path).expanduser().resolve()
    port = args.port

    if not client_path.exists() or not client_path.is_dir():
        print(f"client_path does not exist or is not a directory: {client_path}")
        sys.exit(2)

    scheme, ctrl_host, ctrl_port, ctrl_base = parse_controller(controller)
    ips = discover_non_loopback_ips(ctrl_host, ctrl_port)

    node_id = args.node_id or str(uuid.uuid4())
    addresses = [f"http://{ip}:{port}" for ip in ips]

    print(f"Subscribing to controller at {ctrl_base}/subscribe_client")
    print(f"Node ID: {node_id}")
    print(f"Addresses: {addresses}")

    status, body = post_subscription(ctrl_base, node_id, addresses)
    if status is None:
        print("Warning: subscription failed, continuing to serve local client anyway.")
    elif status >= 400:
        print("Controller returned error; check controller logs and request payload.")

    app = create_app(client_path, ctrl_base, node_id, addresses)
    print(f"Starting Flask server serving '{client_path}' on 0.0.0.0:{port} (advertised addresses: {addresses})")
    app.run(host="0.0.0.0", port=port, debug=False)


if __name__ == "__main__":
    main()
