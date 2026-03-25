from __future__ import annotations

import json
import shutil
from contextlib import asynccontextmanager
from dataclasses import dataclass
from pathlib import Path

import httpx
from fastapi import FastAPI, File, Form, HTTPException, Response, UploadFile

from benchmarking_suite.shared.files import archive_directory_to_bytes, ensure_directory, extract_tar_bytes, utc_timestamp
from benchmarking_suite.shared.models import ClientConnectionPayload, ClientStartPayload, ConfigUploadPayload, ContentInventoryEntry, DownloadLogsPayload
from benchmarking_suite.shared.processes import start_process, stop_process


@dataclass
class ClientProvisionerSettings:
    public_url: str
    instance_controller_url: str
    client_id: str
    client_name: str
    content_root: Path
    config_root: Path
    log_root: Path


class ClientProvisionerState:
    def __init__(self, settings: ClientProvisionerSettings) -> None:
        self.settings = settings
        self.process = None

    async def register(self) -> None:
        inventory = []
        if self.settings.content_root.exists():
            for child in self.settings.content_root.iterdir():
                if child.is_dir():
                    inventory.append(
                        ContentInventoryEntry(
                            name=child.name,
                            file_count=sum(1 for item in child.rglob("*") if item.is_file()),
                        )
                    )
        payload = ClientConnectionPayload(
            client_id=self.settings.client_id,
            client_name=self.settings.client_name,
            base_url=self.settings.public_url,
            inventory=inventory,
        )
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(
                f"{self.settings.instance_controller_url.rstrip('/')}/api/clients/connect",
                json=payload.model_dump(mode="json"),
            )
            response.raise_for_status()


def create_app(settings: ClientProvisionerSettings) -> FastAPI:
    state = ClientProvisionerState(settings)

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        await state.register()
        yield

    app = FastAPI(title="Client Provisioner", lifespan=lifespan)

    @app.post("/api/start")
    async def start(payload: ClientStartPayload) -> dict[str, str]:
        if state.process and state.process.poll() is None:
            raise HTTPException(status_code=409, detail="Client process already running")
        state.process = start_process(payload.command, cwd=payload.cwd, env=payload.env)
        return {"status": "started"}

    @app.post("/api/quit")
    async def quit_process() -> dict[str, str]:
        if not state.process:
            return {"status": "not-running"}
        result = stop_process(state.process, 5.0)
        state.process = None
        return {"status": result}

    @app.post("/api/content/upload")
    async def upload_content(target_path: str = Form(...), archive: UploadFile = File(...)) -> dict[str, str]:
        target = settings.content_root / target_path
        extract_tar_bytes(target, await archive.read())
        return {"status": "uploaded"}

    @app.post("/api/config/upload")
    async def upload_config(payload: ConfigUploadPayload) -> dict[str, str]:
        destination = settings.config_root / payload.destination_path
        ensure_directory(destination.parent)
        destination.write_text(json.dumps(payload.json_content, indent=2, sort_keys=True), encoding="utf-8")
        return {"status": "uploaded"}

    @app.post("/api/logs/download")
    async def download_logs(payload: DownloadLogsPayload) -> Response:
        log_source = settings.log_root / payload.path if payload.path else settings.log_root
        if not log_source.exists():
            raise HTTPException(status_code=404, detail="Log path does not exist")
        archive = archive_directory_to_bytes(log_source)
        archived_root = ensure_directory(settings.log_root / "archived" / utc_timestamp())
        if log_source == settings.log_root:
            for child in list(settings.log_root.iterdir()):
                if child.name == "archived":
                    continue
                shutil.move(str(child), str(archived_root / child.name))
        else:
            shutil.move(str(log_source), str(archived_root / log_source.name))
        return Response(content=archive, media_type="application/gzip")

    return app
