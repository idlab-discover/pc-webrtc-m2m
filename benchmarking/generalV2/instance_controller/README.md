# Instance Controller

Runs on each benchmark machine. Manages the experiment loop, coordinates Client Provisioners, and reports back to the General Controller.

## Responsibilities
- Registers with the General Controller on startup
- Accepts Client Provisioner connections and syncs missing content to them
- Runs the full experiment loop for every combination of network config × content × frame rate × iteration
- Collects logs from provisioners and local directories, then uploads them to the General Controller

## Install

```bash
pip install -r requirements.txt
```

## Usage

```bash
python server.py \
  --general-controller <URL> \
  --id <ID> \
  --address <URL> \
  --session-manager <PATH> \
  [--session-manager-args "<ARGS>"] \
  [--local-log-dirs <DIR1,DIR2>] \
  [--content-base-path <PATH>] \
  [--qdisc-script <PATH>] \
  [--host HOST] \
  [--port PORT]
```

| Argument | Required | Default | Description |
|----------|----------|---------|-------------|
| `--general-controller` | Yes | – | URL of the General Controller, e.g. `http://192.168.1.100:5000` |
| `--id` | Yes | – | Unique name for this instance, e.g. `instance-1` |
| `--address` | Yes | – | This machine's URL reachable by other components, e.g. `http://192.168.1.101:5001` |
| `--session-manager` | Yes | – | Path to the `session_manager` executable |
| `--session-manager-args` | No | `""` | Space-separated arguments forwarded to `session_manager` |
| `--local-log-dirs` | No | `""` | Comma-separated directories to collect local logs from after each run |
| `--content-base-path` | No | `""` | Directory containing content sub-folders to sync to Client Provisioners |
| `--qdisc-script` | No | `""` | Path to `setup_qdisc.sh` (only used when `apply_qdisc` is `true` in the experiment config) |
| `--host` | No | `0.0.0.0` | Interface to bind to |
| `--port` | No | `5001` | Port to listen on |

## Example

```bash
python server.py \
  --general-controller http://192.168.1.100:5000 \
  --id instance-1 \
  --address http://192.168.1.101:5001 \
  --session-manager /opt/session_manager \
  --session-manager-args "--config /etc/sm/config.json" \
  --local-log-dirs /var/log/session_manager,/var/log/sfu \
  --content-base-path /data/content \
  --qdisc-script ./setup_qdisc.sh \
  --port 5001
```

## Network shaping (qdisc)

`setup_qdisc.sh` configures `tc` on the interface that owns the target IP.
It requires **root / sudo** and `iproute2`.

```bash
# Direct usage
sudo ./setup_qdisc.sh <ip_address> [bandwidth] [latency] [loss]

# Examples
sudo ./setup_qdisc.sh 192.168.1.10 10mbit 50ms 1%   # bandwidth + latency + loss
sudo ./setup_qdisc.sh 192.168.1.10 10mbit            # bandwidth only
sudo ./setup_qdisc.sh 192.168.1.10                   # clear all shaping
```

Network shaping is only applied during an experiment when `"apply_qdisc": true` is set in the experiment config.

## Generated directories

| Path | Contents |
|------|----------|
| `exp_configs/` | Per-(fps/content) session and plyfiles config files |
| `logs/<params>/<timestamp>/` | Logs from each run (provisioner logs + local logs + `experiment_config.json`) |
