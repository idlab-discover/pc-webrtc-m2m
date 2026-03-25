from __future__ import annotations

import asyncio
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import httpx
from fastapi import FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import HTMLResponse, JSONResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates

from benchmarking_suite.shared.files import archive_directory_to_bytes, ensure_directory, load_json, save_json, utc_timestamp
from benchmarking_suite.shared.models import ExperimentConfig, InstanceConnectionPayload, StartExperimentPayload, StatusUpdatePayload


DEFAULT_EXPERIMENT = {
    "iterations": 1,
    "durationSeconds": 30,
    "networkConfigurations": [],
    "contentPaths": [],
    "frameRates": [30],
}
DEFAULT_SESSION_CONFIG = {"camFPS": 30, "supportModes": [{"modeName": "mdc"}]}
DEFAULT_PLYFILES = [{"directoryPath": ""}]


@dataclass
class InstanceRecord:
    instance_id: str
    instance_name: str
    base_url: str
    metadata: dict[str, Any] = field(default_factory=dict)
    status: dict[str, Any] = field(default_factory=dict)
    uploaded_logs: list[str] = field(default_factory=list)


class GeneralControllerState:
    def __init__(self, data_dir: Path) -> None:
        self.data_dir = ensure_directory(data_dir)
        self.instances: dict[str, InstanceRecord] = {}
        self.config_dir = ensure_directory(self.data_dir / "configs")
        self.logs_dir = ensure_directory(self.data_dir / "logs")
        self._ensure_defaults()

    def _ensure_defaults(self) -> None:
        save_json(self.config_dir / "experiment.json", load_json(self.config_dir / "experiment.json", DEFAULT_EXPERIMENT))
        save_json(self.config_dir / "session_config.json", load_json(self.config_dir / "session_config.json", DEFAULT_SESSION_CONFIG))
        save_json(self.config_dir / "plyfiles.json", load_json(self.config_dir / "plyfiles.json", DEFAULT_PLYFILES))

    def dashboard_state(self) -> dict[str, Any]:
        return {
            "instances": [
                {
                    "instance_id": record.instance_id,
                    "instance_name": record.instance_name,
                    "base_url": record.base_url,
                    "metadata": record.metadata,
                    "status": record.status,
                    "uploaded_logs": record.uploaded_logs,
                }
                for record in self.instances.values()
            ],
            "configs": {
                "experiment": load_json(self.config_dir / "experiment.json", DEFAULT_EXPERIMENT),
                "session_config": load_json(self.config_dir / "session_config.json", DEFAULT_SESSION_CONFIG),
                "plyfiles": load_json(self.config_dir / "plyfiles.json", DEFAULT_PLYFILES),
            },
        }


def create_app(data_dir: Path) -> FastAPI:
    state = GeneralControllerState(data_dir)
    app = FastAPI(title="General Controller")
    app.state.controller = state

    templates = Jinja2Templates(directory=str(Path(__file__).parent / "templates"))
    app.mount("/static", StaticFiles(directory=str(Path(__file__).parent / "static")), name="static")

    @app.get("/", response_class=HTMLResponse)
    async def dashboard(request: Request) -> HTMLResponse:
        return templates.TemplateResponse(
            request=request,
            name="dashboard.html",
            context={"state": state.dashboard_state()},
        )

    @app.get("/api/dashboard/state")
    async def dashboard_state() -> JSONResponse:
        return JSONResponse(state.dashboard_state())

    @app.post("/api/instances/connect")
    async def connect_instance(payload: InstanceConnectionPayload) -> dict[str, str]:
        state.instances[payload.instance_id] = InstanceRecord(
            instance_id=payload.instance_id,
            instance_name=payload.instance_name,
            base_url=str(payload.base_url).rstrip("/"),
            metadata=payload.metadata,
        )
        return {"status": "connected"}

    @app.post("/api/instances/{instance_id}/status")
    async def update_status(instance_id: str, payload: StatusUpdatePayload) -> dict[str, str]:
        if instance_id not in state.instances:
            raise HTTPException(status_code=404, detail="Unknown instance")
        state.instances[instance_id].status = payload.model_dump()
        return {"status": "updated"}

    @app.post("/api/instances/{instance_id}/logs")
    async def upload_logs(instance_id: str, relative_path: str = Form(...), file: UploadFile = File(...)) -> dict[str, str]:
        if instance_id not in state.instances:
            raise HTTPException(status_code=404, detail="Unknown instance")
        target = ensure_directory(state.logs_dir / instance_id / Path(relative_path).parent) / Path(relative_path).name
        target.write_bytes(await file.read())
        state.instances[instance_id].uploaded_logs.append(str(Path(relative_path)))
        return {"status": "stored"}

    @app.post("/api/configs/{config_name}")
    async def save_config(config_name: str, body: dict[str, Any]) -> dict[str, str]:
        config_map = {
            "experiment": state.config_dir / "experiment.json",
            "session_config": state.config_dir / "session_config.json",
            "plyfiles": state.config_dir / "plyfiles.json",
        }
        if config_name not in config_map:
            raise HTTPException(status_code=404, detail="Unknown config")
        save_json(config_map[config_name], body)
        return {"status": "saved"}

    async def _send_archive_to_instances(archive_path: Path, target_path: str) -> None:
        if not state.instances:
            raise HTTPException(status_code=400, detail="No connected instances")
        async with httpx.AsyncClient(timeout=120.0) as client:
            for record in state.instances.values():
                with archive_path.open("rb") as handle:
                    files = {"archive": (archive_path.name, handle, "application/gzip")}
                    data = {"target_path": target_path}
                    response = await client.post(f"{record.base_url}/api/content/upload", data=data, files=files)
                    response.raise_for_status()

    @app.post("/api/content/upload")
    async def upload_content(
        target_path: str = Form(...),
        server_source_path: str | None = Form(default=None),
        files: list[UploadFile] = File(default_factory=list),
    ) -> dict[str, str]:
        with tempfile.TemporaryDirectory() as temp_dir_name:
            temp_dir = Path(temp_dir_name)
            source_dir = temp_dir / "content"
            ensure_directory(source_dir)

            if server_source_path:
                source = Path(server_source_path).expanduser().resolve()
                if not source.exists() or not source.is_dir():
                    raise HTTPException(status_code=400, detail="Server source path must be an existing directory")
                extracted_root = source
            else:
                for upload in files:
                    relative_name = upload.filename or "upload.bin"
                    destination = ensure_directory((source_dir / relative_name).parent) / Path(relative_name).name
                    destination.write_bytes(await upload.read())
                extracted_root = source_dir

            archive_path = temp_dir / f"content-{utc_timestamp()}.tar.gz"
            archive_path.write_bytes(archive_directory_to_bytes(extracted_root))
            await _send_archive_to_instances(archive_path, target_path)
        return {"status": "uploaded"}

    @app.post("/api/experiments/start")
    async def start_experiment() -> dict[str, str]:
        experiment = ExperimentConfig.model_validate(
            {
                **load_json(state.config_dir / "experiment.json", DEFAULT_EXPERIMENT),
                "sessionConfig": load_json(state.config_dir / "session_config.json", DEFAULT_SESSION_CONFIG),
                "plyfilesConfig": load_json(state.config_dir / "plyfiles.json", DEFAULT_PLYFILES),
            }
        )
        payload = StartExperimentPayload(experiment=experiment)
        if not state.instances:
            raise HTTPException(status_code=400, detail="No connected instances")
        async with httpx.AsyncClient(timeout=None) as client:
            responses = await asyncio.gather(
                *[
                    client.post(f"{record.base_url}/api/start", json=payload.model_dump(mode="json"))
                    for record in state.instances.values()
                ]
            )
        for response in responses:
            response.raise_for_status()
        return {"status": "started"}

    return app
