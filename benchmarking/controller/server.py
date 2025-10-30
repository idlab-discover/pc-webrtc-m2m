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


# Ensure repository root is on sys.path so sibling package 'shared' can be imported
_script_dir = Path(__file__).resolve().parent
_repo_root = _script_dir.parent
if str(_repo_root) not in sys.path:
    sys.path.insert(0, str(_repo_root))

from shared.config import parse_root, validate_config, print_summary


app = Flask(__name__)

# Base directory for uploads (kept inside the controller package)
BASE_UPLOAD_DIR = Path(__file__).parent / "uploads"

# Optional manager binary/path configured via CLI. Stored as a string or None.
manager_path: str | None = None
app.config["manager_path"] = None
app.config["config_path"] = None
app.config["manager_process"] = None


# In-memory subscriptions map: nodeID -> list of addresses
SUBSCRIPTIONS: dict[str, list[str]] = {}

# In-memory provider subscriptions map: providerKey -> list of addresses
PROVIDER_SUBSCRIPTIONS: dict[str, list[str]] = {}
PROVIDER_KEY_TO_NODE_ID: dict[str, str] = {}
PROVIDER_IP_FILTER: dict[str, str] = {}
def _collect_logs_after_delay(delay_seconds: int = 10, logs_base: Path | None = None) -> None:
    """Background worker: wait `delay_seconds`, create timestamped subdir under
    `logs_base` (defaults to controller/logs), call /download_logs on all
    subscribed client addresses and unzip any returned zip files into the
    timestamped folder.
    """
    try:
        time.sleep(float(delay_seconds))
    except Exception:
        # If sleep fails for any reason, continue to attempt collection
        pass

    if logs_base is None:
        logs_base = Path(__file__).parent / "logs"
    try:
        logs_base.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"Failed to create logs base directory {logs_base}: {e}")
        return

    ts = datetime.datetime.utcnow().strftime("%Y%m%dT%H%M%SZ")
    target_dir = logs_base / ts
    try:
        target_dir.mkdir(parents=True, exist_ok=True)
    except Exception as e:
        print(f"Failed to create timestamped logs dir {target_dir}: {e}")
        return

    # If a manager process was started by the controller, terminate it before
    # collecting logs so it can flush and close files. Wait a short time after
    # termination to allow OS to settle.
    mgr_proc = app.config.get("manager_process")
    if mgr_proc is not None:
        try:
            pid = getattr(mgr_proc, "pid", None)
            print(f"Stopping manager process (pid={pid}) before collecting logs")
            try:
                mgr_proc.terminate()
            except Exception:
                # best-effort; continue to try kill below
                pass
            try:
                mgr_proc.wait(timeout=5)
                print("Manager process terminated gracefully")
            except Exception:
                try:
                    print("Manager did not exit in time, killing")
                    mgr_proc.kill()
                    mgr_proc.wait(timeout=2)
                except Exception:
                    print("Failed to kill manager process or wait for exit")
            # clear stored reference
            app.config["manager_process"] = None
        except Exception as e:
            print(f"Error while stopping manager process: {e}")

        # wait a short grace period after stopping manager before collecting logs
        try:
            time.sleep(2)
        except Exception:
            pass

    # Move any existing log files from logs_base to the timestamped target_dir
    for log_file in logs_base.glob("*.log"):
        try:
            shutil.move(str(log_file), str(target_dir / log_file.name))
            print(f"Moved log file {log_file} to {target_dir}")
        except Exception as e:
            print(f"Failed to move log file {log_file}: {e}")
    
    # Snapshot subscriptions at the time of collection to avoid concurrent dict mutations
    subs_snapshot = {k: list(v) for k, v in SUBSCRIPTIONS.items()}
    # Snapshot provider subscriptions as well
    prov_subs_snapshot = {k: list(v) for k, v in PROVIDER_SUBSCRIPTIONS.items()}

    for node_id, addresses in subs_snapshot.items():
        success = False
        for addr in addresses: # TODO If successful dont try for other IPs 
            if success:
                break
            url = addr.rstrip("/") + "/download_logs"
            print(f"Requesting logs from {node_id} @ {url}")
            try:
                with requests.get(url, stream=True, timeout=15) as resp:
                    if resp.status_code != 200:
                        print(f"Non-200 response from {url}: {resp.status_code}")
                        continue

                    # Save streamed content to a temporary file
                    try:
                        with tempfile.NamedTemporaryFile(delete=False, suffix=".zip") as tmpf:
                            for chunk in resp.iter_content(chunk_size=8192):
                                if chunk:
                                    tmpf.write(chunk)
                            tmp_path = Path(tmpf.name)
                    except Exception as e:
                        print(f"Failed to write zip from {url} to temp file: {e}")
                        continue

                    # Extract into a node-specific subdirectory to avoid collisions
                    node_target = target_dir / (str(node_id) or "unknown_node")
                    try:
                        node_target.mkdir(parents=True, exist_ok=True)
                    except Exception as e:
                        print(f"Failed to create node target dir {node_target}: {e}")
                        try:
                            tmp_path.unlink()
                        except Exception:
                            pass
                        continue

                    try:
                        with zipfile.ZipFile(str(tmp_path), 'r') as zf:
                            zf.extractall(path=str(node_target))
                        print(f"Extracted logs from {url} to {node_target}")
                    except zipfile.BadZipFile:
                        print(f"Received invalid zip file from {url}")
                    except Exception as e:
                        print(f"Failed to extract zip from {url}: {e}")
                    finally:
                        try:
                            tmp_path.unlink()
                        except Exception:
                            pass
                    success = True
            except Exception as e:
                print(f"Error while downloading logs from {url}: {e}")

    # Also collect logs from providers (providerKey -> addresses)
    for prov_key, addresses in prov_subs_snapshot.items():
        for addr in addresses:
            # Providers may have provided addresses without scheme/host; try to use as-is
            url = f"http://{addr.rstrip('/')}/download_logs"
            print(f"Requesting provider logs for {prov_key} @ {url}")
            try:
                with requests.get(url, stream=True, timeout=15) as resp:
                    if resp.status_code != 200:
                        print(f"Non-200 response from {url}: {resp.status_code}")
                        continue

                    try:
                        with tempfile.NamedTemporaryFile(delete=False, suffix=".zip") as tmpf:
                            for chunk in resp.iter_content(chunk_size=8192):
                                if chunk:
                                    tmpf.write(chunk)
                            tmp_path = Path(tmpf.name)
                    except Exception as e:
                        print(f"Failed to write provider zip from {url} to temp file: {e}")
                        continue

                    # Extract into a provider-specific subdirectory under the same timestamp
                    prov_target = target_dir / (f"provider_{str(prov_key)}")
                    try:
                        prov_target.mkdir(parents=True, exist_ok=True)
                    except Exception as e:
                        print(f"Failed to create provider target dir {prov_target}: {e}")
                        try:
                            tmp_path.unlink()
                        except Exception:
                            pass
                        continue

                    try:
                        with zipfile.ZipFile(str(tmp_path), 'r') as zf:
                            zf.extractall(path=str(prov_target))
                        print(f"Extracted provider logs from {url} to {prov_target}")
                    except zipfile.BadZipFile:
                        print(f"Received invalid zip file from {url}")
                    except Exception as e:
                        print(f"Failed to extract provider zip from {url}: {e}")
                    finally:
                        try:
                            tmp_path.unlink()
                        except Exception:
                            pass
            except Exception as e:
                print(f"Error while downloading provider logs from {url}: {e}")




@app.route("/", methods=["GET"])
def health():
    """Simple health check endpoint."""
    return jsonify({"status": "ok", "message": "server running"})


@app.route("/start_experiment", methods=["POST"])
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
        return jsonify({"error": "Invalid or missing JSON in request body"}), 400
    if not isinstance(data, dict):
        return jsonify({"error": "Expected a JSON object (dictionary)"}), 400
    
    # Try to parse and validate as Runner/Controller config using shared helpers
    try:
        cfg = parse_root(data)
        
    except Exception as e:
        return jsonify({"error": f"Failed to parse JSON as config: {e}"}), 400

    errs = validate_config(cfg)
    if errs:
        return jsonify({"error": "config validation failed", "details": errs}), 400
    
    print_summary(cfg)
    providers = cfg.providers if hasattr(cfg, "providers") and cfg.providers else []
    print(f"Total providers in config: {len(providers)}")
    for p in providers:
        print(f"Configured provider: nodeID={p.nodeID}, type={p.providerType}, key={p.providerKey}")
        existing = PROVIDER_SUBSCRIPTIONS.get(p.providerKey, [])
        PROVIDER_KEY_TO_NODE_ID[p.providerKey] = p.nodeID
        PROVIDER_IP_FILTER[p.providerKey] = p.ipFilter

    # START SESSION MANAGER HERE
    # If a manager binary and config path were provided via CLI, start the
    # manager in the background (pass the config using -c <config_path>).
    manager_started_info = None
    mgr_path = app.config.get("manager_path")
    cfg_path = app.config.get("config_path")
    if mgr_path:
        if not cfg_path:
            print("Manager path configured but no config_path provided; skipping manager start")
            manager_started_info = {"started": False, "reason": "missing config_path"}
        else:
            try:
                # Start the manager process in the foreground so its stdout/stderr are
                # visible in this process. We still do not wait for it to finish;
                # store the Popen handle so other parts of the app can inspect/terminate it.
                popen = subprocess.Popen([mgr_path, "-c", cfg_path], stdout=None, stderr=None)
                app.config["manager_process"] = popen
                print(f"Started manager process in foreground (pid={popen.pid}): {mgr_path} -c {cfg_path}")
                manager_started_info = {"started": True, "manager_path": mgr_path, "config_path": cfg_path}
            except Exception as e:
                print(f"Failed to start manager process {mgr_path} -c {cfg_path}: {e}")
                manager_started_info = {"started": False, "reason": str(e)}
    else:
        print("No manager_path configured; not starting manager process")
        manager_started_info = {"started": False, "reason": "no manager_path configured"}
    
    time.sleep(2)
    # Tell subscribed nodes to start their clients as specified in cfg.clients
    start_results: list[dict] = []

    # Best-effort extraction of manager IP from config using normal attribute access
    manager_ip = cfg.sessionManagerConfig.sessionManagerIP
    print(f"Manager IP for clients to connect to: {manager_ip}")
    clients = cfg.clients if hasattr(cfg, "clients") and cfg.clients else []
    clientCounter = 0
    for client in clients:
        print(f"Processing client nodeID={getattr(client, 'nodeID', None)}")
        try:
            try:
                node_id = client.nodeID
            except AttributeError:
                print(f"Missing nodeID on client config: {client}")
                start_results.append({"nodeID": None, "error": "missing nodeID on client"})
                continue

            addresses = SUBSCRIPTIONS.get(node_id, [])
            if not addresses:
                print(f"No subscription addresses found for client nodeID={node_id}")
                start_results.append({"nodeID": node_id, "error": "no subscription addresses for nodeID"})
                continue

            # choose the first known address for this nodeID
            addr = addresses[0]
            nClients = client.nClients
            url = addr.rstrip("/") + "/start_clients"
            client_type = client.clientType
            transcoder_config = client.transcoderConfig
            provider_config = client.providersConfig
            transcoder_type = client.transcoderType
            ip_filter = client.ipFilter
            payload = {
                "clientCounterStart": clientCounter,
                "nClients": nClients,
                "clientType": client_type,
                "transcoderConfig": transcoder_config,
                "providerConfig": provider_config,
                "transcoderType": transcoder_type,
                "managerIP": manager_ip,
                "ipFilter": ip_filter,
            }
            try:
                print("sddsds")
                clientCounter += nClients
                resp = requests.post(url, json=payload, timeout=5)
                start_results.append({
                    "clientCounterStart": clientCounter,
                    "nodeID": node_id,
                    "address": addr,
                    "status_code": resp.status_code,
                    "ok": 200 <= resp.status_code < 300,
                    "response_text": resp.text,
                })
                print(f"Started client {node_id} at {addr}, response: {resp.status_code} {resp.text}")
            except Exception as e:
                start_results.append({"nodeID": node_id, "address": addr, "error": str(e)})

        except Exception as e:
            start_results.append({"error": f"failed processing client: {e}"})

    # This will probably have to be done in a seperate thread tbh
    # Create a server manager with providers
    #           Use controller config to find path to server manager binary
    # Health check server manager and providers
    # If ready => Create clients, 1 by 1, health check each client.
    #               A: Make all clients start sending at same time
    #               B: Just start sending as soon as possible
    # After X time has passed => shut down clients, providers, server manager.
    # Collect stats from all nodes
    # Save as file on this server, in directory with folder
    # Repeat for each iteration

    # Success — echo back the received object and acknowledge validation
    # Or just dont do that and create a seperate thread and then we just manually gather the results

    resp_body = {"status": "ok", "received": data, "message": "valid configuration"}
    if start_results:
        resp_body["start_results"] = start_results
    if manager_started_info is not None:
        resp_body["manager_start"] = manager_started_info
    
    # Kick off background log collection after a short delay; do not block the
    # HTTP response. Use a daemon thread so it won't prevent process exit.
    try:
        t = threading.Thread(target=_collect_logs_after_delay, args=(cfg.experimentDurationSeconds, None), daemon=True)
        t.start()
        print(f"Started background log collection thread (waiting {cfg.experimentDurationSeconds}s before download)")
    except Exception as e:
        print(f"Failed to start background log collection thread: {e}")

    return jsonify(resp_body), 200


@app.route("/json", methods=["POST"])
def json_echo():
    """
    POST /json
    Expects: application/json body containing a JSON object (dictionary).
    Returns 400 if no valid JSON is provided or JSON is not an object.
    Returns 200 with an echo of the JSON on success.
    """
    data = request.get_json(silent=True)
    if data is None:
        return jsonify({"error": "Invalid or missing JSON in request body"}), 400
    if not isinstance(data, dict):
        return jsonify({"error": "Expected a JSON object (dictionary)"}), 400
    return jsonify({"status": "ok", "received": data}), 200


def _is_valid_relative_dir(rel_path: str) -> bool:
    """Return True if rel_path is a safe relative path (no absolute, no traversal).

    We also reject paths containing drive letters or leading separators. This
    keeps uploads confined to BASE_UPLOAD_DIR.
    """
    if not rel_path:
        return True
    # Reject absolute paths and paths with drive letters
    p = Path(rel_path)
    if p.is_absolute():
        return False
    # Reject any parent-up references
    if any(part == ".." for part in p.parts):
        return False
    # Reject windows drive letters like C:\ provided as 'C:...'
    if ":" in rel_path:
        return False
    # simple additional sanitization: no leading separators
    if rel_path.startswith("/") or rel_path.startswith("\\"):
        return False
    return True


@app.route("/upload", methods=["POST"])
def upload_file():
    """
    POST /upload
    Multipart form-data with fields:
      - file: the uploaded file
      - dir: (optional) relative directory under the server's uploads/ folder

    Behavior:
      - Validates that 'dir' is a relative path and does not contain traversal.
      - Creates the directory (and parents) if it doesn't exist.
      - Saves the uploaded file using a secure filename.
      - Returns the saved path relative to the project on success.
    """
    if 'file' not in request.files:
        return jsonify({"error": "No file part in the request"}), 400

    file = request.files['file']
    nodeID = request.form.get('nodeID', '').strip()
    rel_dir = request.form.get('dir', '').strip()

    if not _is_valid_relative_dir(rel_dir):
        return jsonify({"error": "Invalid directory path; must be a relative path without traversal"}), 400

    # Ensure base upload dir exists
    BASE_UPLOAD_DIR.mkdir(parents=True, exist_ok=True)

    # Compute final save directory and ensure it's inside BASE_UPLOAD_DIR
    save_dir = (BASE_UPLOAD_DIR / rel_dir).resolve()
    try:
        base_resolved = BASE_UPLOAD_DIR.resolve()
    except Exception:
        base_resolved = BASE_UPLOAD_DIR

    if not str(save_dir).startswith(str(base_resolved)):
        return jsonify({"error": "Invalid directory path"}), 400

    save_dir.mkdir(parents=True, exist_ok=True)

    filename = secure_filename(file.filename) or "uploaded"
    dest = save_dir / filename
    file.save(str(dest))

    # Return the saved path relative to the project (controller folder)
    rel_saved = dest.relative_to(Path(__file__).parent)
    # If a nodeID was provided, try to forward the saved file to that
    # subscriber's addresses. Keep only the first address that succeeds.
    forward_results: list[dict] = []
    forwarded_to: str | None = None
    provider_info: dict | None = None

    if nodeID:
        addresses = SUBSCRIPTIONS.get(nodeID, [])
        # We'll attempt each address in order until one succeeds.
        for addr in list(addresses):
            # build upload URL for subscriber; assume HTTP and /upload
            url = addr.rstrip("/") + "/upload"
            try:
                with open(dest, "rb") as fh:
                    files = {"file": (filename, fh)}
                    # include nodeID so the subscriber knows who forwarded it
                    # Forward the same 'dir' parameter so the remote server saves
                    # the file in the same relative directory under its uploads/.
                    data = {"nodeID": nodeID, "dir": rel_dir}
                    resp = requests.post(url, files=files, data=data, timeout=5)

                success = 200 <= resp.status_code < 300
                forward_results.append({"address": addr, "status_code": resp.status_code, "ok": success})
                if success:
                    forwarded_to = addr
                    # prune other addresses and keep only the successful one
                    SUBSCRIPTIONS[nodeID] = [addr]
                    break
            except Exception as e:
                forward_results.append({"address": addr, "error": str(e)})

    response = {"status": "ok", "saved_path": str(rel_saved)}
    if nodeID:
        response["nodeID"] = nodeID
        response["forward_attempts"] = forward_results
        if forwarded_to:
            response["forwarded_to"] = forwarded_to

    return jsonify(response), 200


@app.route("/subscribe_client", methods=["POST"])
def subscribe_client():
    """
    POST /subscribe_client
    Expects JSON body with:
      - nodeID: string
      - addresses: array of strings

    Validates input and stores the subscription in-memory. Returns 400 on
    invalid input, 200 with the stored subscription on success.
    """
    data = request.get_json(silent=True)
    if data is None:
        return jsonify({"error": "Invalid or missing JSON in request body"}), 400
    if not isinstance(data, dict):
        return jsonify({"error": "Expected a JSON object (dictionary)"}), 400

    node_id = data.get("nodeID")
    addresses = data.get("addresses")

    if not isinstance(node_id, str) or not node_id:
        return jsonify({"error": "Field 'nodeID' must be a non-empty string"}), 400

    if not isinstance(addresses, list):
        return jsonify({"error": "Field 'addresses' must be an array of strings"}), 400

    if not all(isinstance(a, str) for a in addresses):
        return jsonify({"error": "All elements in 'addresses' must be strings"}), 400

    # Store subscription in-memory
    SUBSCRIPTIONS[node_id] = addresses
    print(f"Subscribed client {node_id} with addresses: {addresses}")

    return jsonify({"status": "ok", "nodeID": node_id, "addresses": addresses}), 200


@app.route("/subscribe_provider", methods=["POST"])
def subscribe_provider():
    """
    POST /subscribe_provider
    Accepts either JSON body or form-data with fields:
      - providerKey: string
      - address: string

    Stores the address for the providerKey in memory (deduped) and returns it.
    """
    data = request.get_json(silent=True)
    if isinstance(data, dict):
        provider_key = data.get("providerKey")
        address = data.get("address")
    else:
        provider_key = request.form.get("providerKey")
        address = request.form.get("address")

    if not isinstance(provider_key, str) or not provider_key.strip():
        return jsonify({"error": "Field 'providerKey' must be a non-empty string"}), 400
    if not isinstance(address, str) or not address.strip():
        return jsonify({"error": "Field 'address' must be a non-empty string"}), 400

    provider_key = provider_key.strip()
    address = address.strip()

    # Insert or dedupe
    existing = PROVIDER_SUBSCRIPTIONS.get(provider_key, [])
    if address not in existing:
        existing.append(address)
    PROVIDER_SUBSCRIPTIONS[provider_key] = existing

    print(f"Subscribed provider {provider_key} with address: {address}")

    return jsonify({
        "status": "ok",
        "providerKey": provider_key,
        "addresses": existing,
    }), 200


@app.route("/start_provider", methods=["POST"])
def start_provider():
    """
    POST /start_provider
    Accepts either JSON body or form-data with fields:
      - providerKey
      - providerType
      - managerIP

    For now, simply prints the received values and returns status ok.
    """
    data = request.get_json(silent=True)
    if isinstance(data, dict):
        provider_key = data.get("providerKey")
        provider_type = data.get("providerType")
        manager_ip = data.get("managerIP")
    else:
        provider_key = request.form.get("providerKey")
        provider_type = request.form.get("providerType")
        manager_ip = request.form.get("managerIP")

    print(
        f"start_provider called with providerKey={provider_key}, "
        f"providerType={provider_type}, managerIP={manager_ip}"
    )

    # Best-effort forward to subscribed provider for this key, if any
    forward_results: list[dict] = []
    forwarded_to: str | None = None
    provider_info = {"status": "failed", "address": "", "port": 0}
    if isinstance(provider_key, str) and provider_key.strip():
        node_id = PROVIDER_KEY_TO_NODE_ID.get(provider_key.strip())
        addresses = PROVIDER_SUBSCRIPTIONS.get(node_id, [])
        ip_filter = PROVIDER_IP_FILTER.get(provider_key.strip(), "")
        print("Addresses", addresses)
        for addr in list(addresses):
            url = f"http://{addr.rstrip('/')}/start_provider"
            payload = {
                "providerKey": provider_key,
                "providerType": provider_type,
                "managerIP": manager_ip,
                "ipFilter": ip_filter,
            }
            try:
                resp = requests.post(url, json=payload, timeout=5)
                success = 200 <= resp.status_code < 300
                forward_results.append({
                    "address": addr,
                    "status_code": resp.status_code,
                    "ok": success,
                    "response_text": resp.text,
                })
                if success:
                    # Try to parse provider fields from JSON body
                    try:
                        body = resp.json()
                        prov_status = body.get("status")
                        prov_addr = body.get("address")
                        prov_port = body.get("port")
                        # light validation/types
                        if isinstance(prov_status, str) and isinstance(prov_addr, str) and isinstance(prov_port, (int, float)):
                            provider_info = {"status": prov_status, "address": prov_addr, "port": int(prov_port)}
                            return jsonify(provider_info), 200
                    except Exception:
                        pass

                    forwarded_to = addr
                    # prune other addresses and keep only the successful one
                    PROVIDER_SUBSCRIPTIONS[provider_key.strip()] = [addr]
                    break
            except Exception as e:
                forward_results.append({"address": addr, "error": str(e)})

    return jsonify(provider_info), 400


if __name__ == "__main__":
    # Default host/port for local testing; change as needed
    parser = argparse.ArgumentParser(description="Controller server")
    parser.add_argument("--manager_path", dest="manager_path", help="Path to the server manager binary or directory", default=None)
    parser.add_argument("--config_path", dest="config_path", help="Path to the manager config file to pass with -c", default=None)
    parser.add_argument("--host", dest="host", help="Host to bind to", default="0.0.0.0")
    parser.add_argument("--port", dest="port", help="Port to bind to", type=int, default=8000)
    parser.add_argument("--debug", dest="debug", action="store_true", help="Run Flask in debug mode")
    args = parser.parse_args()

    # Store manager path in module-global and Flask config so endpoints can access it
    if args.manager_path:
        manager_path = str(Path(args.manager_path))
        app.config["manager_path"] = manager_path
        print(f"Configured manager_path: {manager_path}")
    if args.config_path:
        app.config["config_path"] = str(Path(args.config_path))
        print(f"Configured config_path: {app.config.get('config_path')}")

    app.run(host=args.host, port=args.port, debug=args.debug)
