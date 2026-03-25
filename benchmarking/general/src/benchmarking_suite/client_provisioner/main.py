from __future__ import annotations

import argparse
from pathlib import Path
from uuid import uuid4

import uvicorn

from benchmarking_suite.client_provisioner.app import ClientProvisionerSettings, create_app


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the client provisioner")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8200)
    parser.add_argument("--public-url", required=True)
    parser.add_argument("--instance-controller-url", required=True)
    parser.add_argument("--name", required=True)
    parser.add_argument("--client-id", default=f"client-{uuid4().hex[:8]}")
    parser.add_argument("--content-root", type=Path, required=True)
    parser.add_argument("--config-root", type=Path, required=True)
    parser.add_argument("--log-root", type=Path, required=True)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    settings = ClientProvisionerSettings(
        public_url=args.public_url,
        instance_controller_url=args.instance_controller_url,
        client_id=args.client_id,
        client_name=args.name,
        content_root=args.content_root,
        config_root=args.config_root,
        log_root=args.log_root,
    )
    uvicorn.run(create_app(settings), host=args.host, port=args.port)


if __name__ == "__main__":
    main()
