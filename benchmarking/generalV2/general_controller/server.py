#!/usr/bin/env python3
import os
import json
import io
import zipfile
import argparse
import threading
from concurrent.futures import ThreadPoolExecutor, as_completed

from datetime import datetime
from flask import Flask, request, jsonify, render_template, send_file
import requests

app = Flask(__name__)

# ── State ─────────────────────────────────────────────────────────────────────
connected_instances = {}   # id -> {address, status, current_params, iterations_done, total_iterations, log_files}
instances_lock = threading.Lock()

session_config      = {}
plyfiles_config     = {}
experiment_config   = {}
websocket_config    = {}
webrtc_ports_config          = []
external_webrtc_config_data  = {}

LOGS_DIR    = "logs"
CONFIGS_DIR = "configs"

os.makedirs(LOGS_DIR,    exist_ok=True)
os.makedirs(CONFIGS_DIR, exist_ok=True)


def _load_json(path, default=None):
    if default is None:
        default = {}
    if os.path.exists(path):
        with open(path) as f:
            return json.load(f)
    return default


def _default_experiment_config():
    return {
        "iterations": 3,
        "duration": 60,
        "network_configs": [
            {
                "targetIP":  "192.168.1.10",
                "bandwidth": "10mbit",
                "latency":   "50ms",
                "loss":      "1%"
            },
            {
                "targetIP":  "192.168.1.10",
                "bandwidth": "50mbit",
                "latency":   "10ms",
                "loss":      "0%"
            }
        ],
        "content_paths": [
            "/data/content/sequence_A",
            "/data/content/sequence_B"
        ],
        "frame_rates": [15, 30],
        "client_app_path": "/opt/myapp/client",
        "client_app_args": ["--server", "192.168.1.100", "--port", "8080"],
        "apply_qdisc": False
    }


def _init_configs():
    global session_config, plyfiles_config, experiment_config, websocket_config, webrtc_ports_config, external_webrtc_config_data
    session_config             = _load_json(os.path.join(CONFIGS_DIR, "session_config.json"))
    plyfiles_config            = _load_json(os.path.join(CONFIGS_DIR, "plyfiles.json"), default=[])
    experiment_config          = _load_json(os.path.join(CONFIGS_DIR, "experiment.json"), default=_default_experiment_config())
    websocket_config           = _load_json(os.path.join(CONFIGS_DIR, "websocket.json"))
    webrtc_ports_config        = _load_json(os.path.join(CONFIGS_DIR, "webrtc_ports.json"), default=[])
    external_webrtc_config_data = _load_json(os.path.join(CONFIGS_DIR, "external_webrtc_config.json"))


# ── Instance Controller endpoints ─────────────────────────────────────────────

@app.route("/connect", methods=["POST"])
def connect():
    data = request.json or {}
    iid  = data.get("id")
    addr = data.get("address")
    if not iid or not addr:
        return jsonify({"status": "error", "message": "id and address required"}), 400

    with instances_lock:
        connected_instances[iid] = {
            "address":          addr,
            "status":           "connected",
            "current_params":   None,
            "iterations_done":  0,
            "total_iterations": 0,
            "log_files":        [],
            "connected_at":     datetime.now().isoformat(),
        }

    print(f"[connect] Instance '{iid}' registered at {addr}")
    return jsonify({"status": "ok"})


@app.route("/upload_logs", methods=["POST"])
def upload_logs():
    iid    = request.form.get("instance_id")
    subdir = request.form.get("subdir", "")

    if not iid:
        return jsonify({"status": "error", "message": "instance_id required"}), 400

    dest = os.path.join(LOGS_DIR, iid, subdir) if subdir else os.path.join(LOGS_DIR, iid)
    os.makedirs(dest, exist_ok=True)

    if "logs_archive" in request.files:
        archive = request.files["logs_archive"]
        with zipfile.ZipFile(io.BytesIO(archive.read())) as zf:
            zf.extractall(dest)
    else:
        for _, f in request.files.items():
            safe_name = os.path.basename(f.filename)
            f.save(os.path.join(dest, safe_name))

    with instances_lock:
        if iid in connected_instances:
            instance_dir = os.path.join(LOGS_DIR, iid)
            all_files = []
            for root, _, files in os.walk(instance_dir):
                for fname in files:
                    all_files.append(os.path.relpath(os.path.join(root, fname), instance_dir))
            connected_instances[iid]["log_files"] = all_files

    return jsonify({"status": "ok"})


@app.route("/update_status", methods=["POST"])
def update_status():
    data = request.json or {}
    iid  = data.get("instance_id")

    if not iid:
        return jsonify({"status": "error", "message": "instance_id required"}), 400

    with instances_lock:
        if iid in connected_instances:
            connected_instances[iid].update({
                "status":           data.get("status", "unknown"),
                "current_params":   data.get("current_params"),
                "iterations_done":  data.get("iterations_done",  0),
                "total_iterations": data.get("total_iterations", 0),
                "updated_at":       datetime.now().isoformat(),
            })

    return jsonify({"status": "ok"})


# ── Dashboard ─────────────────────────────────────────────────────────────────

@app.route("/")
def dashboard():
    return render_template(
        "dashboard.html",
        session_config_json    = json.dumps(session_config,    indent=2),
        plyfiles_config_json   = json.dumps(plyfiles_config,   indent=2),
        experiment_config_json = json.dumps(experiment_config, indent=2),
        websocket_config_json      = json.dumps(websocket_config,      indent=2),
        webrtc_ports_config_json          = json.dumps(webrtc_ports_config,          indent=2),
        external_webrtc_config_json       = json.dumps(external_webrtc_config_data,  indent=2),
    )


# ── REST API used by dashboard JS ─────────────────────────────────────────────

@app.route("/api/instances")
def api_instances():
    with instances_lock:
        return jsonify(dict(connected_instances))


@app.route("/api/config/session", methods=["GET", "POST"])
def api_session_config():
    global session_config
    if request.method == "POST":
        session_config = request.json
        with open(os.path.join(CONFIGS_DIR, "session_config.json"), "w") as f:
            json.dump(session_config, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(session_config)


@app.route("/api/config/plyfiles", methods=["GET", "POST"])
def api_plyfiles_config():
    global plyfiles_config
    if request.method == "POST":
        plyfiles_config = request.json
        with open(os.path.join(CONFIGS_DIR, "plyfiles.json"), "w") as f:
            json.dump(plyfiles_config, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(plyfiles_config)


@app.route("/api/config/websocket", methods=["GET", "POST"])
def api_websocket_config():
    global websocket_config
    if request.method == "POST":
        websocket_config = request.json
        with open(os.path.join(CONFIGS_DIR, "websocket.json"), "w") as f:
            json.dump(websocket_config, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(websocket_config)


@app.route("/api/config/webrtc_ports", methods=["GET", "POST"])
def api_webrtc_ports_config():
    global webrtc_ports_config
    if request.method == "POST":
        webrtc_ports_config = request.json
        with open(os.path.join(CONFIGS_DIR, "webrtc_ports.json"), "w") as f:
            json.dump(webrtc_ports_config, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(webrtc_ports_config)


@app.route("/api/config/external_webrtc_config", methods=["GET", "POST"])
def api_external_webrtc_config():
    global external_webrtc_config_data
    if request.method == "POST":
        external_webrtc_config_data = request.json
        with open(os.path.join(CONFIGS_DIR, "external_webrtc_config.json"), "w") as f:
            json.dump(external_webrtc_config_data, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(external_webrtc_config_data)


@app.route("/api/config/experiment", methods=["GET", "POST"])
def api_experiment_config():
    global experiment_config
    if request.method == "POST":
        experiment_config = request.json
        with open(os.path.join(CONFIGS_DIR, "experiment.json"), "w") as f:
            json.dump(experiment_config, f, indent=2)
        return jsonify({"status": "ok"})
    return jsonify(experiment_config)


@app.route("/api/upload_content", methods=["POST"])
def api_upload_content():
    """Forward uploaded content to all connected instance controllers.

    Returns immediately — actual transfers happen in background threads so
    large files or slow/unreachable instances never block the browser request.
    """
    upload_path = request.form.get("path", "")
    if not upload_path:
        return jsonify({"status": "error", "message": "path required"}), 400

    # Build the full payload in the request context before spawning threads
    server_path = request.form.get("server_path", "")

    if server_path and os.path.isdir(server_path):
        zip_buf = io.BytesIO()
        with zipfile.ZipFile(zip_buf, "w", zipfile.ZIP_DEFLATED) as zf:
            for entry in os.scandir(server_path):
                if entry.is_file():
                    zf.write(entry.path, entry.name)
        payload_bytes = zip_buf.getvalue()
        use_zip = True
    else:
        files_data = {key: (f.filename, f.read(), f.content_type) for key, f in request.files.items()}
        if not files_data:
            return jsonify({"status": "error", "message": "No files provided"}), 400
        use_zip = False

    with instances_lock:
        instance_list = list(connected_instances.items())

    if not instance_list:
        return jsonify({"status": "ok", "message": "No instances connected"})

    def _upload_to(iid, address):
        try:
            if use_zip:
                r = requests.post(
                    f"{address}/upload_content",
                    data={"path": upload_path},
                    files={"archive": ("content.zip", io.BytesIO(payload_bytes), "application/zip")},
                    timeout=300,
                )
            else:
                r = requests.post(
                    f"{address}/upload_content",
                    data={"path": upload_path},
                    files={k: (name, data, ct) for k, (name, data, ct) in files_data.items()},
                    timeout=300,
                )
            return iid, r.status_code
        except Exception as e:
            return iid, str(e)

    results = {}
    with ThreadPoolExecutor(max_workers=len(instance_list)) as pool:
        futures = {pool.submit(_upload_to, iid, inst["address"]): iid for iid, inst in instance_list}
        for future in as_completed(futures):
            iid, result = future.result()
            results[iid] = result

    return jsonify(results)


@app.route("/api/start_experiment", methods=["POST"])
def api_start_experiment():
    """Send start command with current experiment config to all instance controllers."""
    payload = dict(experiment_config)
    payload["session_config"] = session_config
    payload["plyfiles"]       = plyfiles_config
    payload["websocket"]      = websocket_config
    payload["webrtc_ports"]          = webrtc_ports_config
    payload["external_webrtc_config"] = external_webrtc_config_data

    # Allow dashboard to override specific fields
    overrides = request.json or {}
    payload.update(overrides)

    results = {}
    with instances_lock:
        instance_list = list(connected_instances.items())

    for iid, instance in instance_list:
        try:
            resp = requests.post(
                f"{instance['address']}/start",
                json=payload,
                timeout=15,
            )
            results[iid] = resp.status_code
            with instances_lock:
                connected_instances[iid]["status"] = "starting"
        except Exception as e:
            results[iid] = str(e)

    return jsonify(results)


@app.route("/api/logs/<instance_id>")
def api_get_logs(instance_id):
    """Return the list of log files for an instance."""
    with instances_lock:
        inst = connected_instances.get(instance_id)

    if inst is None:
        return jsonify({"status": "error", "message": "Instance not found"}), 404

    return jsonify({"log_files": inst.get("log_files", [])})


@app.route("/api/logs/<instance_id>/download")
def api_download_logs(instance_id):
    """Download all logs for an instance as a zip."""
    log_dir = os.path.join(LOGS_DIR, instance_id)
    if not os.path.isdir(log_dir):
        return jsonify({"status": "error", "message": "No logs found"}), 404

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(log_dir):
            for fname in files:
                fp = os.path.join(root, fname)
                zf.write(fp, os.path.relpath(fp, log_dir))
    buf.seek(0)

    return send_file(
        buf,
        mimetype="application/zip",
        as_attachment=True,
        download_name=f"logs_{instance_id}.zip",
    )


# ── Entry point ───────────────────────────────────────────────────────────────

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="General Controller")
    parser.add_argument("--host",     default="0.0.0.0",  help="Bind host")
    parser.add_argument("--port",     type=int, default=5000, help="Bind port")
    parser.add_argument("--logs-dir", default=LOGS_DIR, help="Base directory for log files")
    args = parser.parse_args()

    LOGS_DIR = args.logs_dir
    os.makedirs(LOGS_DIR, exist_ok=True)

    _init_configs()
    app.run(host=args.host, port=args.port, debug=False)
