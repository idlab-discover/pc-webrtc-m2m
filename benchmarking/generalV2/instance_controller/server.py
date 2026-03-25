#!/usr/bin/env python3
import os
import json
import io
import copy
import shutil
import zipfile
import subprocess
import threading
import argparse
import time
from datetime import datetime
from flask import Flask, request, jsonify
import requests

app = Flask(__name__)

# ── Config filled from CLI args ────────────────────────────────────────────────
general_controller_address = None
instance_id                = None
instance_address           = None
session_manager_path       = None
session_manager_args       = []
local_log_dirs             = []
content_base_path          = None
qdisc_script_path          = None
verbose_subprocess         = False
config_upload_before_start = False

# ── Runtime state ──────────────────────────────────────────────────────────────
connected_provisioners = {}   # id -> {address, directories}
provisioners_lock      = threading.Lock()

experiment_running = False
experiment_lock    = threading.Lock()

base_session_config          = {}
base_plyfiles_config         = []
base_websocket_config        = {}
base_webrtc_ports_config     = []
base_external_webrtc_config  = {}


# ── General Controller helpers ─────────────────────────────────────────────────

def _gc_fetch_base_configs():
    """Fetch session_config, plyfiles, websocket and webrtc_ports from the General Controller."""
    def _fetch(path, label):
        try:
            r = requests.get(f"{general_controller_address}{path}", timeout=10)
            r.raise_for_status()
            return r.json()
        except Exception as e:
            print(f"[gc] Failed to fetch {label}: {e}")
            return None

    return (
        _fetch("/api/config/session",               "session_config"),
        _fetch("/api/config/plyfiles",              "plyfiles"),
        _fetch("/api/config/websocket",             "websocket"),
        _fetch("/api/config/webrtc_ports",          "webrtc_ports"),
        _fetch("/api/config/external_webrtc_config","external_webrtc_config"),
    )


def _gc_connect():
    payload = {"id": instance_id, "address": instance_address}
    try:
        r = requests.post(f"{general_controller_address}/connect", json=payload, timeout=10)
        print(f"[gc] Connected: {r.status_code}")
    except Exception as e:
        print(f"[gc] Connect failed: {e}")


def _gc_update_status(status, current_params=None, iterations_done=0, total_iterations=0):
    payload = {
        "instance_id":      instance_id,
        "status":           status,
        "current_params":   current_params,
        "iterations_done":  iterations_done,
        "total_iterations": total_iterations,
    }
    try:
        requests.post(f"{general_controller_address}/update_status", json=payload, timeout=5)
    except Exception as e:
        print(f"[gc] Status update failed: {e}")


def _gc_upload_logs(log_dir):
    """Zip log_dir and send to General Controller."""
    if not os.path.isdir(log_dir):
        return

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for root, _, files in os.walk(log_dir):
            for fname in files:
                fp  = os.path.join(root, fname)
                arc = os.path.relpath(fp, os.path.dirname(log_dir))
                zf.write(fp, arc)
    buf.seek(0)

    subdir = os.path.relpath(log_dir, "logs")
    try:
        requests.post(
            f"{general_controller_address}/upload_logs",
            data={"instance_id": instance_id, "subdir": subdir},
            files={"logs_archive": ("logs.zip", buf, "application/zip")},
            timeout=120,
        )
        print("[gc] Logs uploaded")
    except Exception as e:
        print(f"[gc] Log upload failed: {e}")


# ── Provisioner helpers ────────────────────────────────────────────────────────

def _upload_config_to_provisioner(prov_address, path, config_data):
    r = requests.post(
        f"{prov_address}/upload_config",
        json={"path": path, "config": config_data},
        timeout=15,
    )
    r.raise_for_status()


def _download_logs_from_provisioner(prov_id, prov_address, save_dir):
    r = requests.post(f"{prov_address}/download_logs", json={}, timeout=60)
    if r.status_code != 200:
        print(f"[prov:{prov_id}] Log download failed: {r.status_code}")
        return

    dest = os.path.join(save_dir, f"provisioner_{prov_id}")
    os.makedirs(dest, exist_ok=True)
    with zipfile.ZipFile(io.BytesIO(r.content)) as zf:
        zf.extractall(dest)
    print(f"[prov:{prov_id}] Logs saved to {dest}")


# ── qdisc & session manager ────────────────────────────────────────────────────

def _run_qdisc(network_config):
    if not qdisc_script_path:
        print("[qdisc] No script path configured, skipping")
        return

    cmd = [
        "bash", qdisc_script_path,
        network_config.get("targetIP", ""),
        network_config.get("bandwidth", "1000mbit"),
        network_config.get("latency",   "0ms"),
        network_config.get("loss",      "0%"),
    ]
    r = subprocess.run(cmd, capture_output=True, text=True, timeout=30)
    if r.returncode != 0:
        print(f"[qdisc] Script error: {r.stderr.strip()}")
    else:
        print(f"[qdisc] {r.stdout.strip()}")


def _manager_ip():
    """Return the instance address as host:8080 (no protocol prefix)."""
    addr = instance_address
    if "://" in addr:
        addr = addr.split("://", 1)[1]
    host = addr.rsplit(":", 1)[0]
    return f"{host}:8080"


def _stream_output(stream, prefix):
    for line in stream:
        print(f"[{prefix}] {line}", end="", flush=True)


def _start_session_manager():
    cmd = [session_manager_path] + session_manager_args
    print(f"[sm] Starting: {' '.join(cmd)}")
    if verbose_subprocess:
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        threading.Thread(target=_stream_output, args=(proc.stdout, "sm:out"), daemon=True).start()
        threading.Thread(target=_stream_output, args=(proc.stderr, "sm:err"), daemon=True).start()
    else:
        proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return proc


def _stop_session_manager(proc):
    if proc.poll() is not None:
        return
    proc.terminate()
    try:
        proc.wait(timeout=5)
        print("[sm] Terminated gracefully")
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()
        print("[sm] Killed")


# ── Local log archiving ────────────────────────────────────────────────────────

def _archive_local_logs(source_dir, dest_dir):
    if not os.path.isdir(source_dir):
        return

    dest = os.path.join(dest_dir, "local_" + os.path.basename(source_dir.rstrip("/")))
    shutil.copytree(source_dir, dest, dirs_exist_ok=True)

    # Archive originals
    for item in os.listdir(source_dir):
        full = os.path.join(source_dir, item)
        if os.path.isfile(full):
            os.remove(full)
        elif os.path.isdir(full):
            shutil.rmtree(full)


# ── Config file generation ─────────────────────────────────────────────────────

def _generate_exp_configs(frame_rates, content_paths, exp_dir):
    """
    Creates per-(fps) session config files for both clients and
    per-(content) plyfiles configs inside exp_dir.
    Returns dicts keyed by fps / content_idx for easy lookup.
    """
    sc_files  = {}   # fps -> (client0_path, client1_path)
    ply_files = {}   # content_idx -> path

    for fps in frame_rates:
        for client_idx, mode in enumerate(["spectator", "mdc"], start=0):
            sc = copy.deepcopy(base_session_config)
            sc["camFPS"] = fps
            print(f"[config] Generated session config for client{client_idx} at {fps}fps with mode '{mode}' length {len(sc.get('supportedModes', []))}")
            if "supportedModes" in sc and len(sc["supportedModes"]) > 0:
                sc["supportedModes"][0]["modeName"] = mode
            fname = os.path.join(exp_dir, f"session_config_client{client_idx}_{fps}fps.json")
            with open(fname, "w") as f:
                json.dump(sc, f, indent=2)
        sc_files[fps] = (
            os.path.join(exp_dir, f"session_config_client0_{fps}fps.json"),
            os.path.join(exp_dir, f"session_config_client1_{fps}fps.json"),
        )

    for idx, cpath in enumerate(content_paths):
        ply = copy.deepcopy(base_plyfiles_config)
        if isinstance(ply, list) and len(ply) > 0:
            ply[0]["directoryPath"] = cpath
        elif isinstance(ply, dict):
            ply["directoryPath"] = cpath
        fname = os.path.join(exp_dir, f"plyfiles_content{idx}.json")
        with open(fname, "w") as f:
            json.dump(ply, f, indent=2)
        ply_files[idx] = fname

    return sc_files, ply_files


# ── Per-provisioner config upload ─────────────────────────────────────────────

def _prov_host(address):
    """Extract the hostname/IP from a provisioner address URL."""
    addr = address
    if "://" in addr:
        addr = addr.split("://", 1)[1]
    return addr.rsplit(":", 1)[0]


def _upload_configs_to_provisioner(p_idx, pid, prov, sc, ply, cfg_root, local_index=0):
    sc = copy.deepcopy(sc)
    try:
        sc["clientID"] = int(pid.rsplit("-", 1)[-1])
    except (ValueError, IndexError):
        pass
    try:
        _upload_config_to_provisioner(prov["address"], os.path.join(cfg_root, "session_config.json"), sc)
        _upload_config_to_provisioner(prov["address"], os.path.join(cfg_root, "camera", "plyfiles.json"), ply)

        ws = copy.deepcopy(base_websocket_config)
        #ws["managerIP"] = _manager_ip()
        try:
            ws["preferredClientID"] = int(pid.rsplit("-", 1)[-1])
        except (ValueError, IndexError):
            ws["preferredClientID"] = p_idx + 1
        ws["selectedCodecMode"] = sc.get("supportedModes", [{}])[0].get("modeName", "spectator")
        _upload_config_to_provisioner(prov["address"], os.path.join(cfg_root, "session", "websocket.json"), ws)

        if base_webrtc_ports_config:
            ports = copy.deepcopy(base_webrtc_ports_config)
            if isinstance(ports, list) and len(ports) > 0 and local_index > 0:
                range_size = ports[0]["end"] - ports[0]["start"]
                ports[0]["start"] += local_index * range_size
                ports[0]["end"]   += local_index * range_size
            print(f"[prov:{pid}] Uploading WebRTC ports config: {ports}")
            _upload_config_to_provisioner(prov["address"], os.path.join(cfg_root, "session", "webrtc_ports.json"), ports)

        if base_external_webrtc_config:
            _upload_config_to_provisioner(prov["address"], os.path.join(cfg_root, "session", "external_webrtc_config.json"), base_external_webrtc_config)
    except Exception as e:
        print(f"[prov:{pid}] Config upload error: {e}")


# ── Experiment loop ────────────────────────────────────────────────────────────

def _run_experiment(config):
    global experiment_running

    iterations            = config.get("iterations", 1)
    duration              = config.get("duration", 60)
    network_configs       = config.get("network_configs", [])
    content_paths         = config.get("content_paths", [])
    frame_rates           = config.get("frame_rates", [30])
    client_app_root       = config.get("client_app_root", "")
    client_app_exe        = config.get("client_app_exe", "")
    client_app_args       = config.get("client_app_args", [])
    client_config_dir     = config.get("client_config_directory", "config")

    client_exe_path = os.path.join(client_app_root, client_app_exe) if client_app_root else client_app_exe

    global base_session_config, base_plyfiles_config, base_websocket_config, base_webrtc_ports_config, base_external_webrtc_config
    if "session_config" in config and "plyfiles" in config:
        base_session_config  = config["session_config"]
        base_plyfiles_config = config["plyfiles"]
        if "websocket" in config:
            base_websocket_config = config["websocket"]
        if "webrtc_ports" in config:
            base_webrtc_ports_config = config["webrtc_ports"]
        if "external_webrtc_config" in config:
            base_external_webrtc_config = config["external_webrtc_config"]
    else:
        print("[gc] Base configs not in payload – fetching from General Controller")
        sc, ply, ws, ports, ext_wrtc = _gc_fetch_base_configs()
        if sc       is not None: base_session_config         = sc
        if ply      is not None: base_plyfiles_config        = ply
        if ws       is not None: base_websocket_config       = ws
        if ports    is not None: base_webrtc_ports_config    = ports
        if ext_wrtc is not None: base_external_webrtc_config = ext_wrtc

    exp_dir = "exp_configs"
    os.makedirs(exp_dir, exist_ok=True)
    sc_files, ply_files = _generate_exp_configs(frame_rates, content_paths, exp_dir)

    total   = len(network_configs) * len(content_paths) * len(frame_rates) * iterations
    current = 0

    for net_cfg in network_configs:
        for c_idx, cpath in enumerate(content_paths):
            for fps in frame_rates:
                for iteration in range(1, iterations + 1):
                    current += 1
                    label = (
                        f"net_{net_cfg.get('targetIP','?')}_"
                        f"bw{net_cfg.get('bandwidth','').replace(' ', '')}_"
                        f"lat{net_cfg.get('latency','').replace(' ', '')}_"
                        f"loss{net_cfg.get('loss','').replace(' ', '')}_"
                        f"content_{os.path.basename(cpath.rstrip('/'))}_"
                        f"fps{fps}"
                    )
                    print(f"\n=== [{current}/{total}] {label}  iter={iteration} ===")

                    _gc_update_status(
                        "running",
                        current_params={"network": net_cfg, "content": cpath, "fps": fps, "iteration": iteration},
                        iterations_done=current - 1,
                        total_iterations=total,
                    )

                    # Load generated configs
                    sc1_path, sc2_path = sc_files[fps]
                    ply_path           = ply_files[c_idx]
                    with open(sc1_path)  as f: sc1 = json.load(f)
                    with open(sc2_path)  as f: sc2 = json.load(f)
                    with open(ply_path)  as f: ply = json.load(f)

                    with provisioners_lock:
                        prov_list = sorted(connected_provisioners.items(), key=lambda x: x[0])

                    session_configs = [sc1, sc2]
                    cfg_root = os.path.join(client_app_root, client_config_dir) if client_app_root else client_config_dir

                    # Build local_index per provisioner (same host → increment)
                    host_seen = {}
                    local_indices = []
                    for _, (_, prov) in enumerate(prov_list):
                        host = _prov_host(prov["address"])
                        local_indices.append(host_seen.get(host, 0))
                        host_seen[host] = host_seen.get(host, 0) + 1

                    # Upload configs before session manager (default)
                    if not config_upload_before_start:
                        for p_idx, (pid, prov) in enumerate(prov_list):
                            _upload_configs_to_provisioner(
                                p_idx, pid, prov,
                                session_configs[min(p_idx, len(session_configs) - 1)],
                                ply, cfg_root, local_indices[p_idx],
                            )

                    # Network setup (optional)
                    if config.get("apply_qdisc", False):
                        try:
                            _run_qdisc(net_cfg)
                        except Exception as e:
                            print(f"[qdisc] Error: {e}")
                    else:
                        print("[qdisc] Skipped (apply_qdisc is false)")

                    # Start session manager
                    sm_proc = None
                    try:
                        sm_proc = _start_session_manager()
                    except Exception as e:
                        print(f"[sm] Start error: {e}")

                    time.sleep(5)

                    # Start provisioners
                    for p_idx, (pid, prov) in enumerate(prov_list):
                        if config_upload_before_start:
                            _upload_configs_to_provisioner(
                                p_idx, pid, prov,
                                session_configs[min(p_idx, len(session_configs) - 1)],
                                ply, cfg_root, local_indices[p_idx],
                            )
                        try:
                            r = requests.post(
                                f"{prov['address']}/start",
                                json={"path": client_exe_path, "args": client_app_args},
                                timeout=10,
                            )
                            print(f"[prov:{pid}] Start → {r.status_code}")
                        except Exception as e:
                            print(f"[prov:{pid}] Start error: {e}")
                        if config_upload_before_start:
                            time.sleep(10)
                        else:
                            time.sleep(2)

                    # Run for duration
                    print(f"Running for {duration}s …")
                    time.sleep(duration)

                    # Quit provisioners
                    for pid, prov in prov_list:
                        try:
                            r = requests.post(f"{prov['address']}/quit", timeout=10)
                            print(f"[prov:{pid}] Quit → {r.status_code} ({r.text.strip()})")
                        except Exception as e:
                            print(f"[prov:{pid}] Quit error: {e}")

                    # Stop session manager
                    if sm_proc:
                        _stop_session_manager(sm_proc)
                    # Sleep a bit to allow processes to exit and flush logs
                    time.sleep(5)
                    # Collect logs
                    timestamp = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
                    log_dir   = os.path.join("logs", label, timestamp)
                    os.makedirs(log_dir, exist_ok=True)

                    for pid, prov in prov_list:
                        try:
                            _download_logs_from_provisioner(pid, prov["address"], log_dir)
                        except Exception as e:
                            print(f"[prov:{pid}] Log download error: {e}")

                    for src in local_log_dirs:
                        try:
                            _archive_local_logs(src, log_dir)
                        except Exception as e:
                            print(f"[local-logs] Error archiving {src}: {e}")

                    # Save experiment record
                    record = {
                        "network_config": net_cfg,
                        "content_path":   cpath,
                        "frame_rate":     fps,
                        "iteration":      iteration,
                        "duration":       duration,
                        "timestamp":      timestamp,
                    }
                    with open(os.path.join(log_dir, "experiment_config.json"), "w") as f:
                        json.dump(record, f, indent=2)

                    _gc_update_status(
                        "iteration_complete",
                        current_params=record,
                        iterations_done=current,
                        total_iterations=total,
                    )

                    try:
                        _gc_upload_logs(log_dir)
                    except Exception as e:
                        print(f"[gc] Log upload error: {e}")

    _gc_update_status("completed", iterations_done=total, total_iterations=total)
    print("\n=== Experiment complete ===")

    with experiment_lock:
        experiment_running = False


# ── Content sync to provisioner ────────────────────────────────────────────────

def _sync_content(prov_id, prov_address, prov_dirs):
    """Upload any directories the provisioner is missing or has stale file counts."""
    if not content_base_path or not os.path.isdir(content_base_path):
        print(f"[sync] No valid content_base_path configured, skipping sync for prov:{prov_id}")
        return

    prov_map = {d["name"]: d["file_count"] for d in prov_dirs}

    for entry in os.scandir(content_base_path):
        if not entry.is_dir():
            continue
        local_count = sum(1 for f in os.scandir(entry.path) if f.is_file())
        if entry.name not in prov_map or prov_map[entry.name] != local_count:
            print(f"[sync] Uploading '{entry.name}' → prov:{prov_id}")
            _upload_dir_to_provisioner(prov_id, prov_address, entry.path, entry.name)


def _upload_dir_to_provisioner(prov_id, prov_address, local_dir, dir_name):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as zf:
        for f in os.scandir(local_dir):
            if f.is_file():
                zf.write(f.path, f.name)
    buf.seek(0)
    try:
        requests.post(
            f"{prov_address}/upload_content",
            data={"directory": dir_name},
            files={"archive": ("content.zip", buf, "application/zip")},
            timeout=120,
        )
        print(f"[sync] '{dir_name}' uploaded to prov:{prov_id}")
    except Exception as e:
        print(f"[sync] Error uploading to prov:{prov_id}: {e}")


# ── Flask endpoints ────────────────────────────────────────────────────────────

@app.route("/start", methods=["POST"])
def start():
    global experiment_running, experiment_lock

    with experiment_lock:
        if experiment_running:
            return jsonify({"status": "error", "message": "Experiment already running"}), 409

        config = request.json
        if not config:
            return jsonify({"status": "error", "message": "No config provided"}), 400

        experiment_running = True

    t = threading.Thread(target=_run_experiment, args=(config,), daemon=True)
    t.start()
    return jsonify({"status": "ok", "message": "Experiment started"})


@app.route("/upload_content", methods=["POST"])
def upload_content():
    dir_name = request.form.get("path")
    if not dir_name:
        return jsonify({"status": "error", "message": "path required"}), 400

    if content_base_path:
        target_path = os.path.join(content_base_path, dir_name)
    else:
        target_path = dir_name

    os.makedirs(target_path, exist_ok=True)
    saved = []

    for _, f in request.files.items():
        if f.filename == "archive" or f.content_type == "application/zip":
            with zipfile.ZipFile(io.BytesIO(f.read())) as zf:
                zf.extractall(target_path)
            saved.append("<zip extracted>")
        else:
            dest = os.path.join(target_path, os.path.basename(f.filename))
            f.save(dest)
            saved.append(os.path.basename(f.filename))

    # Forward to all connected provisioners
    if content_base_path:
        with provisioners_lock:
            prov_list = list(connected_provisioners.items())
        for pid, prov in prov_list:
            threading.Thread(
                target=_upload_dir_to_provisioner,
                args=(pid, prov["address"], target_path, dir_name),
                daemon=True,
            ).start()

    return jsonify({"status": "ok", "saved": saved})


@app.route("/connect", methods=["POST"])
def connect():
    """Client Provisioner registers itself here."""
    data   = request.json or {}
    pid    = data.get("id")
    paddr  = data.get("address")
    dirs   = data.get("directories", [])

    if not pid or not paddr:
        return jsonify({"status": "error", "message": "id and address required"}), 400

    with provisioners_lock:
        connected_provisioners[pid] = {"address": paddr, "directories": dirs}

    print(f"[prov:{pid}] Connected at {paddr} – dirs: {[d['name'] for d in dirs]}")

    if content_base_path:
        print(f"[prov:{pid}] Syncing content directories to new provisioner …")
        threading.Thread(target=_sync_content, args=(pid, paddr, dirs), daemon=True).start()

    return jsonify({"status": "ok"})


# ── Entry point ────────────────────────────────────────────────────────────────

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Instance Controller")
    parser.add_argument("--general-controller", required=True,
                        help="General Controller URL, e.g. http://192.168.1.100:5000")
    parser.add_argument("--id",      required=True, dest="instance_id",
                        help="Unique identifier for this instance")
    parser.add_argument("--address", required=True,
                        help="This instance's URL reachable from other components, e.g. http://192.168.1.101:5001")
    parser.add_argument("--session-manager", required=True,
                        help="Path to the session_manager executable")
    parser.add_argument("--session-manager-args", default="",
                        help="Space-separated arguments for session_manager")
    parser.add_argument("--local-log-dirs", default="",
                        help="Comma-separated list of local log directories to collect after each run")
    parser.add_argument("--content-base-path", default="",
                        help="Base directory where content is stored for distribution to provisioners")
    parser.add_argument("--qdisc-script", default="",
                        help="Path to setup_qdisc.sh")
    parser.add_argument("--port", type=int, default=5001)
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--verbose-subprocess", action="store_true",
                        help="Print stdout/stderr of the session manager to this terminal")
    parser.add_argument("--config-upload-before-start", action="store_true",
                        help="Upload configs to provisioners just before /start instead of before the session manager")
    args = parser.parse_args()

    general_controller_address = args.general_controller
    instance_id                = args.instance_id
    instance_address           = args.address
    session_manager_path       = args.session_manager
    session_manager_args       = args.session_manager_args.split() if args.session_manager_args else []
    local_log_dirs             = [d.strip() for d in args.local_log_dirs.split(",") if d.strip()]
    content_base_path          = args.content_base_path
    qdisc_script_path          = args.qdisc_script
    verbose_subprocess         = args.verbose_subprocess
    config_upload_before_start = args.config_upload_before_start

    threading.Thread(target=_gc_connect, daemon=True).start()
    app.run(host=args.host, port=args.port, debug=False)
