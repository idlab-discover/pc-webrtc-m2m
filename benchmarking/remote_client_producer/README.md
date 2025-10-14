remote_client_producer

Simple helper to expose a local client application and register it with the controller's /subscribe_client endpoint.

Usage

python server.py <controller_addr> <client_path> [node_id] [--port PORT]

Examples

python server.py localhost:8000 ..\runner\base_configs\client my-node-01 --port 9001

What it does
- POSTs to http://<controller>/subscribe_client with JSON {"nodeID": "<node_id or uuid>", "addresses": ["http://<this_host>:<port>"]}
- Serves the specified client_path via a simple HTTP file server

Notes
- controller_addr may be a host, host:port or full URL. If no scheme is provided, http:// is assumed.
- The script will continue to run and serve files even if the subscription request fails.
