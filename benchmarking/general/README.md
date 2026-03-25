# Benchmarking Suite

This workspace contains three cooperating services:

- `general-controller`: central dashboard, config editor, experiment trigger, log collector.
- `instance-controller`: per-host orchestrator that applies network shaping, runs experiments, and aggregates logs.
- `client-provisioner`: per-client controller that starts the client application, receives content/configs, and serves logs.

## Installation

```bash
python -m venv .venv
source .venv/bin/activate
pip install -e .
```

## Run

General controller:

```bash
general-controller --host 0.0.0.0 --port 8000 --data-dir ./runtime/general
```

Instance controller:

```bash
instance-controller \
  --host 0.0.0.0 \
  --port 8100 \
  --public-url http://127.0.0.1:8100 \
  --general-controller-url http://127.0.0.1:8000 \
  --name instance-a \
  --data-dir ./runtime/instance-a \
  --content-root ./content \
  --session-manager /path/to/session_manager \
  --session-manager-args "-c /path/to/config.json" \
  --client-command /path/to/client_app \
  --client-args "-c ARG -d TEST" \
  --qdisc-script ./scripts/configure_qdisc.sh \
  --local-log-dir ./logs/a \
  --local-log-dir ./logs/b

# Disable qdisc/network shaping completely:
# instance-controller ... --disable-network-shaping
```

Example:
```bash
instance-controller \
  --host 0.0.0.0 \
  --port 8100 \
  --public-url http://127.0.0.1:8100 \
  --general-controller-url http://127.0.0.1:8000 \
  --name instance-a \
  --data-dir ./runtime/instance-a \
  --content-root ./content \
  --session-manager /home/matthias/Documents/pc-webrtc-m2m/session_manager \
  --session-manager-args "-c /home/matthias/Documents/pc-webrtc-m2m/session_manager/config/temp_config.json" \
  --client-command /path/to/client_app \
  --client-args "-c ARG -d TEST" \
  --local-log-dir ./logs/a \
  --local-log-dir ./logs/b \
  --disable-network-shaping
```


Client provisioner:

```bash
client-provisioner \
  --host 0.0.0.0 \
  --port 8200 \
  --public-url http://127.0.0.1:8200 \
  --instance-controller-url http://127.0.0.1:8100 \
  --name client-a \
  --content-root ./client-content \
  --config-root ./client-config \
  --log-root ./client-logs
```

## Notes

- The dashboard is served from the general controller root path.
- The qdisc helper script uses `ip` and `tc` and therefore requires Linux and appropriate privileges.
- Use `--disable-network-shaping` on the instance controller to skip qdisc entirely.
- Experiment and base config JSONs are stored under the general controller data directory and can be edited in the dashboard.
