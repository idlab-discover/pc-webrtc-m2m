from __future__ import annotations

import io
import json
import shutil
import tarfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def ensure_directory(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path


def load_json(path: Path, default: Any) -> Any:
    if not path.exists():
        return default
    return json.loads(path.read_text(encoding="utf-8"))


def save_json(path: Path, value: Any) -> None:
    ensure_directory(path.parent)
    path.write_text(json.dumps(value, indent=2, sort_keys=True), encoding="utf-8")


def utc_timestamp() -> str:
    return datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")


def slugify(value: str) -> str:
    sanitized = []
    for character in value:
        if character.isalnum() or character in {"-", "_"}:
            sanitized.append(character)
        else:
            sanitized.append("-")
    return "".join(sanitized).strip("-") or "value"


def archive_directory_to_bytes(source: Path) -> bytes:
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w:gz") as archive:
        for child in source.iterdir():
            archive.add(child, arcname=child.name)
    buffer.seek(0)
    return buffer.read()


def extract_tar_bytes(target: Path, payload: bytes) -> None:
    ensure_directory(target)
    with tarfile.open(fileobj=io.BytesIO(payload), mode="r:gz") as archive:
        archive.extractall(target)


def copy_tree_contents(source: Path, target: Path) -> None:
    ensure_directory(target)
    for child in source.iterdir():
        destination = target / child.name
        if child.is_dir():
            shutil.copytree(child, destination, dirs_exist_ok=True)
        else:
            shutil.copy2(child, destination)


def move_directory_contents(source: Path, target: Path) -> None:
    ensure_directory(target)
    for child in list(source.iterdir()):
        shutil.move(str(child), str(target / child.name))


def write_text(path: Path, content: str) -> None:
    ensure_directory(path.parent)
    path.write_text(content, encoding="utf-8")
