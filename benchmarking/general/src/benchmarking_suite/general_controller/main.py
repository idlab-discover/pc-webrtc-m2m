from __future__ import annotations

import argparse
from pathlib import Path

import uvicorn

from benchmarking_suite.general_controller.app import create_app


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the general controller")
    parser.add_argument("--host", default="0.0.0.0")
    parser.add_argument("--port", type=int, default=8000)
    parser.add_argument("--data-dir", type=Path, default=Path("./runtime/general"))
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    uvicorn.run(create_app(args.data_dir), host=args.host, port=args.port)


if __name__ == "__main__":
    main()
