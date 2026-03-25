from __future__ import annotations

import argparse
import shlex
from pathlib import Path
from uuid import uuid4

import uvicorn

from benchmarking_suite.instance_controller.app import InstanceSettings, create_app


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the instance controller")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8100)
    parser.add_argument("--public-url", required=True)
    parser.add_argument("--general-controller-url", required=True)
    parser.add_argument("--name", required=True)
    parser.add_argument("--instance-id", default=f"instance-{uuid4().hex[:8]}")
    parser.add_argument("--data-dir", type=Path, default=Path("./runtime/instance"))
    parser.add_argument("--content-root", type=Path, required=True)
    parser.add_argument("--session-manager", required=True)
    parser.add_argument("--session-manager-arg", action="append", default=[])
    parser.add_argument("--session-manager-args", default="")
    parser.add_argument("--client-command", action="append", required=True)
    parser.add_argument("--client-args", default="")
    parser.add_argument("--qdisc-script", type=Path, default=None)
    parser.add_argument("--disable-network-shaping", action="store_true")
    parser.add_argument("--local-log-dir", action="append", default=[])
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    session_manager_args = [*args.session_manager_arg, *shlex.split(args.session_manager_args)]
    client_command = [*args.client_command, *shlex.split(args.client_args)]
    settings = InstanceSettings(
        public_url=args.public_url,
        general_controller_url=args.general_controller_url,
        instance_id=args.instance_id,
        instance_name=args.name,
        data_dir=args.data_dir,
        content_root=args.content_root,
        session_manager_command=[args.session_manager, *session_manager_args],
        client_command=client_command,
        qdisc_script=args.qdisc_script,
        enable_network_shaping=not args.disable_network_shaping,
        local_log_dirs=[Path(path) for path in args.local_log_dir],
    )
    uvicorn.run(create_app(settings), host=args.host, port=args.port)


if __name__ == "__main__":
    main()
