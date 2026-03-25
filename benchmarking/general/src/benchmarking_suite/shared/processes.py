from __future__ import annotations

import os
import subprocess
import time
from pathlib import Path


def start_process(command: list[str], cwd: str | None = None, env: dict[str, str] | None = None) -> subprocess.Popen[str]:
    merged_env = os.environ.copy()
    if env:
        merged_env.update(env)
    return subprocess.Popen(
        command,
        cwd=cwd,
        env=merged_env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        text=True,
    )


def stop_process(process: subprocess.Popen[str], grace_seconds: float = 5.0) -> str:
    if process.poll() is not None:
        return "already-stopped"
    process.terminate()
    deadline = time.time() + grace_seconds
    while time.time() < deadline:
        if process.poll() is not None:
            return "terminated"
        time.sleep(0.2)
    process.kill()
    process.wait(timeout=5)
    return "killed"


def run_command(command: list[str], cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, cwd=str(cwd) if cwd else None, check=True, text=True, capture_output=True)
