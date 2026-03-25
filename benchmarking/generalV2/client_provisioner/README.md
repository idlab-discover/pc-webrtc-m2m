# Client Provisioner

Runs on each client machine. Manages starting and stopping the client application and serves content, config, and log endpoints for the Instance Controller.

## Responsibilities
- Registers with the Instance Controller on startup, reporting available content directories
- Receives content and config files uploaded by the Instance Controller
- Starts and stops the client application on demand
- Packages and sends logs back to the Instance Controller after each run

## Install

```bash
pip install -r requirements.txt
```

## Usage

```bash
python server.py \
  --instance-controller <URL> \
  --content-path <PATH> \
  --log-dir <PATH> \
  --address <URL> \
  --id <ID> \
  [--host HOST] \
  [--port PORT]
```

| Argument | Required | Default | Description |
|----------|----------|---------|-------------|
| `--instance-controller` | Yes | – | URL of the Instance Controller, e.g. `http://192.168.1.101:5001` |
| `--content-path` | Yes | – | Base directory where content folders are stored / received |
| `--log-dir` | Yes | – | Directory where the client application writes its logs |
| `--address` | Yes | – | This machine's URL reachable by the Instance Controller, e.g. `http://192.168.1.102:5002` |
| `--id` | Yes | – | Unique name for this provisioner, e.g. `client-1` |
| `--host` | No | `0.0.0.0` | Interface to bind to |
| `--port` | No | `5002` | Port to listen on |

## Example

```bash
# First client (will receive "mdc" session config)
python server.py \
  --instance-controller http://192.168.1.101:5001 \
  --content-path /data/content \
  --log-dir /var/log/client \
  --address http://192.168.1.102:5002 \
  --id client-1 \
  --port 5002

# Second client (will receive "spectator" session config)
python server.py \
  --instance-controller http://192.168.1.101:5001 \
  --content-path /data/content \
  --log-dir /var/log/client \
  --address http://192.168.1.103:5002 \
  --id client-2 \
  --port 5002
```

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/start` | Start the client application. Body: `{"path": "/path/to/app", "args": ["--arg"]}` |
| `POST` | `/quit` | Stop the running client application. Returns `ended`, `killed`, or `not_running`. |
| `POST` | `/upload_content` | Upload files into a named sub-directory of `--content-path`. Form field: `directory`. |
| `POST` | `/upload_config` | Write a JSON config to a specific path. Body: `{"path": "...", "config": {...}}` |
| `POST` | `/download_logs` | Zip and return logs, then archive them. Body: `{"path": "..."}` (optional, defaults to `--log-dir`). |

## Log archiving

After each `/download_logs` call the collected files are moved to `<log-dir>/archived/<timestamp>/` so subsequent calls only return new logs.
