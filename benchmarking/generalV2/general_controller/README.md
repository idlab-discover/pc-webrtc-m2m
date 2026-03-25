# General Controller

Central hub for the benchmarking system. Hosts a web dashboard and coordinates all connected Instance Controllers.

## Responsibilities
- Accepts connections from Instance Controllers
- Hosts a dashboard for editing configs, uploading content, and starting experiments
- Receives status updates and log uploads from Instance Controllers

## Install

```bash
pip install -r requirements.txt
```

## Usage

```bash
python server.py [--host HOST] [--port PORT]
```

| Argument | Default | Description |
|----------|---------|-------------|
| `--host` | `0.0.0.0` | Interface to bind to |
| `--port` | `5000` | Port to listen on |

## Example

```bash
python server.py --host 0.0.0.0 --port 5000
```

Then open `http://<machine-ip>:5000` in a browser.

## Dashboard features
- **Instances table** – live status, progress, and log files for each connected Instance Controller (auto-refreshes every 4 s)
- **Config editors** – edit `experiment.json`, `session_config.json`, and `plyfiles.json` in-browser; changes are saved to `configs/`
- **Upload content** – push a directory of files to all Instance Controllers (from the browser or a server-side path)
- **Start experiment** – sends the current experiment config to every Instance Controller

## Generated directories

| Path | Contents |
|------|----------|
| `configs/` | Persisted JSON config files |
| `logs/<instance-id>/` | Logs uploaded by each Instance Controller |
