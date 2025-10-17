#!/usr/bin/env python3
"""runner.py

Small CLI utility to parse and validate the example JSON configuration
provided in `json_example.json`.

Features:
- Loads JSON from a path (defaults to the bundled `json_example.json`).
- Simple data classes for typed access to sessionManager, providers and clients.
- Prints a concise human readable summary.
- Basic validation and helpful error messages.

This file was added by an automated assistant to satisfy a request to parse the
attached JSON example.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional
import sys

# Ensure repository root is on sys.path so we can import the sibling 'controller' package
_script_dir = Path(__file__).resolve().parent
_repo_root = _script_dir.parent
if str(_repo_root) not in sys.path:
    sys.path.insert(0, str(_repo_root))

from shared.config import (
    RootConfig,
    SessionManagerConfig,
    ProviderConfig,
    ClientConfig,
    load_json,
    parse_root,
    validate_config,
    print_summary,
)


# optional import for HTTP uploads
try:
    import requests
except Exception:  # pragma: no cover - runtime environment may not have requests
    requests = None





def _build_controller_upload_url(controller_ip: str) -> str:
    # controller_ip may be like '127.0.0.1', '127.0.0.1:8000' or 'http://host:8000'
    if controller_ip.startswith("http://") or controller_ip.startswith("https://"):
        base = controller_ip.rstrip("/")
    else:
        if ":" in controller_ip:
            base = f"http://{controller_ip}"
        else:
            base = f"http://{controller_ip}:8000"
    return base + "/upload"


def _build_controller_json_url(controller_ip: str) -> str:
    """Return controller URL for the /json endpoint.

    Accepts controller_ip values similar to other helpers.
    """
    if controller_ip.startswith("http://") or controller_ip.startswith("https://"):
        base = controller_ip.rstrip("/")
    else:
        if ":" in controller_ip:
            base = f"http://{controller_ip}"
        else:
            base = f"http://{controller_ip}:8000"
    return base + "/start_experiment"


def send_config(raw_json: Dict[str, Any], controller_ip: str, dry_run: bool = True) -> int:
    """Send the raw JSON config to the controller /json endpoint.

    - raw_json: the parsed JSON object loaded from file (not a RootConfig)
    - controller_ip: controller host or host:port or http://... string
    - dry_run: if True, don't perform network call; just print what would be done

    Returns 0 on success, non-zero on error.
    """
    url = _build_controller_json_url(controller_ip)
    if dry_run:
        print(f"[dry-run] Would POST config to {url}; payload keys: {list(raw_json.keys())}")
        return 0

    if requests is None:
        print("The 'requests' package is required to perform live config sends. Install it and try again.")
        return 4

    try:
        resp = requests.post(url, json=raw_json, timeout=30)
        if resp.status_code == 200:
            print(f"Config POST successful: {resp.status_code}")
            return 0
        else:
            print(f"ERROR sending config: status={resp.status_code}, body={resp.text}")
            return 5
    except Exception as e:
        print(f"EXCEPTION sending config: {e}")
        return 6


def perform_uploads(cfg: RootConfig, repo_root: Path, dry_run: bool = True) -> int:
    """Loop over clients and upload files from transcoderDirectoryToCopy.

    Behavior:
    - For each client, for each transcoderDirectoryToCopy entry that has two
      elements, the first is treated as the local path (relative to repo_root)
      and the second as the remote target directory passed as the 'dir'
      form-field to the controller's /upload endpoint.
    - Uploads are performed one file at a time.
    - If dry_run is True, no network calls are made; the function prints what
      would be uploaded.

    Returns: 0 on success (or dry-run), non-zero on fatal errors.
    """
    upload_url = _build_controller_upload_url(cfg.controllerIP)

    total_files = 0
    uploaded = 0
    errors = 0

    for client in cfg.clients:
        if not client.transcoderDirectoryToCopy:
            continue
        # we expect a pair [local_dir, remote_dir]
        for pair in [client.transcoderDirectoryToCopy]:
            if not isinstance(pair, (list, tuple)) and len(pair) < 2:
                # In the JSON we expect a list with at least 2 elements
                print(f"Skipping client {client.nodeID}: transcoderDirectoryToCopy malformed: {pair}")
                continue
        # Support both a single list of two elements or a list where the first
        # item is a directory string and second is target; our stored value is
        # assumed to be a list of two strings
        local_dir = client.transcoderDirectoryToCopy[0] if len(client.transcoderDirectoryToCopy) > 0 else None
        remote_dir = client.transcoderDirectoryToCopy[1] if len(client.transcoderDirectoryToCopy) > 1 else ""

        if not local_dir:
            print(f"No local transcoder directory for client {client.nodeID}, skipping")
            continue

        local_path = (repo_root / local_dir).resolve()
        if not local_path.exists():
            print(f"Local directory does not exist for client {client.nodeID}: {local_path}")
            continue

        # iterate files recursively
        files_to_send = [p for p in local_path.rglob("*") if p.is_file()]
        if not files_to_send:
            print(f"No files found under {local_path} for client {client.nodeID}")
            continue

        print(f"Client {client.nodeID}: will upload {len(files_to_send)} files from {local_path} to '{remote_dir}' on controller {upload_url}")

        for fpath in files_to_send:
            total_files += 1
            rel_name = fpath.name
            # compute subdirectory under the local_path for this file (excluding filename)
            try:
                rel_sub = fpath.relative_to(local_path).parent
            except Exception:
                # fallback: place directly under the base
                rel_sub = Path()

            # build the remote dir for this particular file: prefix with the
            # remote_dir provided in the JSON (second element). If remote_dir
            # is empty, fall back to using the local basename. Then append the
            # file's relative subpath so the remote layout mirrors the local one.
            base_prefix = remote_dir if remote_dir else (Path(local_dir).name if local_dir else "")
            if str(rel_sub) in (".", ""):
                per_file_dir = base_prefix or ""
            else:
                rel_sub_posix = rel_sub.as_posix()
                per_file_dir = (base_prefix + "/" + rel_sub_posix) if base_prefix else rel_sub_posix

            if dry_run:
                print(f"  [dry-run] Would upload: {fpath} -> dir='{per_file_dir}', filename='{rel_name}'")
                uploaded += 1
                continue

            # perform actual upload
            if requests is None:
                print("The 'requests' package is required to perform live uploads. Install it and try again.")
                return 4

            try:
                with fpath.open("rb") as fh:
                    files = {"file": (rel_name, fh)}
                    # send the per-file directory so the server recreates the
                    # local subdirectory structure under the provided remote_dir
                    data = {"dir": per_file_dir, "nodeID": client.nodeID}
                    resp = requests.post(upload_url, files=files, data=data, timeout=30)
                if resp.status_code == 200:
                    uploaded += 1
                    print(f"  uploaded: {fpath} -> {per_file_dir} (200)")
                else:
                    errors += 1
                    print(f"  ERROR uploading {fpath} -> {per_file_dir}: status={resp.status_code}, body={resp.text}")
            except Exception as e:
                errors += 1
                print(f"  EXCEPTION uploading {fpath}: {e}")

        # --- handle providersConfigToCopy: single file(s) to upload per client
        if client.providersConfigToCopy:
            print(f"Client {client.nodeID}: processing providersConfigToCopy: {client.providersConfigToCopy}")
            # support either a single pair [local, remote] or a flat list
            pairs: List[List[str]] = []
            pc = client.providersConfigToCopy
            if isinstance(pc, (list, tuple)) and pc and isinstance(pc[0], (list, tuple)):
                # already a list of pairs
                pairs = [list(x) for x in pc]
            elif isinstance(pc, (list, tuple)) and len(pc) >= 2 and all(isinstance(x, str) for x in pc):
                # a single pair
                pairs = [list(pc[:2])]
            else:
                print(f"Skipping providersConfigToCopy for client {client.nodeID}: unexpected format: {pc}")

            for local_file, remote_target in pairs:
                total_files += 1
                local_path_file = (repo_root / local_file).resolve()
                if not local_path_file.exists() or not local_path_file.is_file():
                    print(f"Provider config file not found for client {client.nodeID}: {local_path_file}")
                    errors += 1
                    continue

                # remote_target is a path; we send its parent as dir and its name as filename
                remote_parent = str(Path(remote_target).parent).replace('\\', '/')
                remote_name = Path(remote_target).name

                if dry_run:
                    print(f"  [dry-run] Would upload provider config: {local_path_file} -> dir='{remote_parent}', filename='{remote_name}'")
                    uploaded += 1
                    continue

                if requests is None:
                    print("The 'requests' package is required to perform live uploads. Install it and try again.")
                    return 4

                try:
                    with local_path_file.open('rb') as fh:
                        files = {"file": (remote_name, fh)}
                        data = {"dir": remote_parent, "nodeID": client.nodeID}
                        resp = requests.post(upload_url, files=files, data=data, timeout=30)
                    if resp.status_code == 200:
                        uploaded += 1
                        print(f"  uploaded provider config: {local_path_file} -> {remote_parent}/{remote_name} (200)")
                    else:
                        errors += 1
                        print(f"  ERROR uploading provider config {local_path_file} -> {remote_parent}/{remote_name}: status={resp.status_code}, body={resp.text}")
                except Exception as e:
                    errors += 1
                    print(f"  EXCEPTION uploading provider config {local_path_file}: {e}")

    print(f"\nSummary: total files considered: {total_files}, uploaded/dry-run count: {uploaded}, errors: {errors}")
    return 0 if errors == 0 else 5


def main(argv: Optional[List[str]] = None) -> int:
    p = argparse.ArgumentParser(description="Parse and summarise a runner json config")
    p.add_argument("--file", "-f", default=None, help="Path to JSON file (defaults to bundled json_example.json)")
    p.add_argument("--validate", action="store_true", help="Run basic validation checks and return non-zero on failure")
    p.add_argument("--upload", action="store_true", help="Perform upload dry-run of transcoder files to controller (no network calls)")
    p.add_argument("--live", action="store_true", help="Perform live uploads to controller (requires 'requests' package)")
    p.add_argument("--send-config", action="store_true", help="POST the parsed JSON config to the controller /json endpoint (dry-run unless --live)")
    args = p.parse_args(argv)

    script_dir = Path(__file__).resolve().parent
    json_path = Path(args.file) if args.file else script_dir / "json_example.json"

    try:
        raw = load_json(json_path)
    except Exception as e:
        print(f"Error reading JSON: {e}", file=sys.stderr)
        return 2

    cfg = parse_root(raw)
    print(cfg.experimentDurationSeconds)
    # If upload requested, perform upload actions (dry-run by default)
    if args.upload:
        repo_root = script_dir.parent
        dry_run = not args.live
        return perform_uploads(cfg, repo_root, dry_run=dry_run)

    if args.send_config:
        dry_run = not args.live
        return send_config(raw, cfg.controllerIP, dry_run=dry_run)

    if args.validate:
        errs = validate_config(cfg)
        if errs:
            print("Validation errors:")
            for e in errs:
                print(" -", e)
            return 3
        else:
            print("Validation: OK")
            return 0
    
    print_summary(cfg)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
