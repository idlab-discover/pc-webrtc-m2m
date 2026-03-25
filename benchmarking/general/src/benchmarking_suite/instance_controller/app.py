from __future__ import annotations

import asyncio
import copy
import shutil
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import httpx
from fastapi import FastAPI, File, Form, HTTPException, UploadFile

from benchmarking_suite.shared.files import archive_directory_to_bytes, ensure_directory, extract_tar_bytes, load_json, move_directory_contents, save_json, slugify, utc_timestamp
from benchmarking_suite.shared.models import ClientConnectionPayload, ClientStartPayload, ConfigUploadPayload, DownloadLogsPayload, ExperimentConfig, InstanceConnectionPayload, NetworkConfiguration, StartExperimentPayload, StatusUpdatePayload
from benchmarking_suite.shared.processes import run_command, start_process, stop_process


@dataclass
class ClientRecord:
    client_id: str
    client_name: str
    base_url: str
    inventory: dict[str, int] = field(default_factory=dict)


@dataclass
class InstanceSettings:
    public_url: str
    general_controller_url: str
    instance_id: str
    instance_name: str
    data_dir: Path
    content_root: Path
    session_manager_command: list[str]
    client_command: list[str]
    qdisc_script: Path | None
    enable_network_shaping: bool
    local_log_dirs: list[Path]


class InstanceControllerState:
    def __init__(self, settings: InstanceSettings) -> None:
        self.settings = settings
        self.clients: dict[str, ClientRecord] = {}
        self.current_experiment: asyncio.Task[None] | None = None
        self.status: dict[str, Any] = {"phase": "idle", "message": "Waiting"}
        self.exp_configs_dir = ensure_directory(settings.data_dir / "exp_configs")
        self.logs_dir = ensure_directory(settings.data_dir / "logs")
        self.archive_dir = ensure_directory(settings.data_dir / "archived_local_logs")

    async def register(self) -> None:
        payload = InstanceConnectionPayload(
            instance_id=self.settings.instance_id,
            instance_name=self.settings.instance_name,
            base_url=self.settings.public_url,
            metadata={"content_root": str(self.settings.content_root)},
        )
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(f"{self.settings.general_controller_url.rstrip('/')}/api/instances/connect", json=payload.model_dump(mode="json"))
            response.raise_for_status()

    async def push_status(self, phase: str, message: str, progress: dict[str, Any] | None = None) -> None:
        self.status = {"phase": phase, "message": message, "progress": progress or {}}
        payload = StatusUpdatePayload(
            instance_id=self.settings.instance_id,
            phase=phase,
            message=message,
            progress=progress or {},
        )
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(
                f"{self.settings.general_controller_url.rstrip('/')}/api/instances/{self.settings.instance_id}/status",
                json=payload.model_dump(mode="json"),
            )
            response.raise_for_status()


def create_app(settings: InstanceSettings) -> FastAPI:
    state = InstanceControllerState(settings)

    @asynccontextmanager
    async def lifespan(app: FastAPI):
        await state.register()
        yield

    app = FastAPI(title="Instance Controller", lifespan=lifespan)
    app.state.controller = state

    @app.get("/api/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    @app.post("/api/clients/connect")
    async def connect_client(payload: ClientConnectionPayload) -> dict[str, str]:
        state.clients[payload.client_id] = ClientRecord(
            client_id=payload.client_id,
            client_name=payload.client_name,
            base_url=str(payload.base_url).rstrip("/"),
            inventory={entry.name: entry.file_count for entry in payload.inventory},
        )
        await _sync_client_content(state.clients[payload.client_id])
        return {"status": "connected"}

    async def _sync_client_content(client_record: ClientRecord) -> None:
        for source_dir in settings.content_root.iterdir() if settings.content_root.exists() else []:
            if not source_dir.is_dir():
                continue
            file_count = sum(1 for item in source_dir.rglob("*") if item.is_file())
            if client_record.inventory.get(source_dir.name) == file_count:
                continue
            payload = archive_directory_to_bytes(source_dir)
            async with httpx.AsyncClient(timeout=120.0) as client:
                files = {"archive": (f"{source_dir.name}.tar.gz", payload, "application/gzip")}
                data = {"target_path": source_dir.name}
                response = await client.post(f"{client_record.base_url}/api/content/upload", data=data, files=files)
                response.raise_for_status()

    @app.post("/api/content/upload")
    async def upload_content(target_path: str = Form(...), archive: UploadFile = File(...)) -> dict[str, str]:
        target = settings.content_root / target_path
        extract_tar_bytes(target, await archive.read())
        return {"status": "uploaded"}

    @app.post("/api/start")
    async def start(payload: StartExperimentPayload) -> dict[str, str]:
        if state.current_experiment and not state.current_experiment.done():
            raise HTTPException(status_code=409, detail="Experiment already running")
        state.current_experiment = asyncio.create_task(run_experiment(payload.experiment))
        return {"status": "started"}

    async def run_experiment(experiment: ExperimentConfig) -> None:
        try:
            await state.push_status("preparing", "Generating experiment configs")
            combos = build_experiment_combinations(state, experiment)
            async with httpx.AsyncClient(timeout=None) as client:
                for index, combo in enumerate(combos, start=1):
                    label = build_experiment_label(combo["iteration"], combo["network_configuration"], combo["content_path"], combo["frame_rate"])
                    await state.push_status(
                        "running",
                        f"Running {label}",
                        {"current": index, "total": len(combos), "label": label},
                    )
                    await upload_configs_to_clients(client, experiment, combo)
                    apply_network_configuration(combo["network_configuration"])
                    session_manager = start_process(settings.session_manager_command)
                    try:
                        await asyncio.sleep(5)
                        await start_clients(client)
                        await asyncio.sleep(experiment.durationSeconds)
                    finally:
                        await stop_clients(client)
                        await asyncio.to_thread(stop_process, session_manager, 5.0)
                    experiment_dir = await collect_logs(client, combo)
                    await upload_logs_to_general(client, experiment_dir)
            await state.push_status("completed", "Experiment finished")
        except Exception as exc:
            await state.push_status("failed", str(exc))
            raise

    def build_experiment_combinations(controller_state: InstanceControllerState, experiment: ExperimentConfig) -> list[dict[str, Any]]:
        combinations: list[dict[str, Any]] = []
        print("Building experiment combinations...")
        network_configurations = experiment.networkConfigurations or [None]
        for content_path in experiment.contentPaths:
            for frame_rate in experiment.frameRates:
                config_set = create_saved_config_set(controller_state, experiment, content_path, frame_rate)
                for iteration in range(1, experiment.iterations + 1):
                    combinations.append(
                        {
                            "iteration": iteration,
                            "frame_rate": frame_rate,
                            "content_path": content_path,
                            "network_configuration": "",
                            "config_set": config_set,
                        }
                    )
        print(f"Built {len(combinations)} combinations")
        return combinations

    def create_saved_config_set(controller_state: InstanceControllerState, experiment: ExperimentConfig, content_path: str, frame_rate: int) -> dict[str, Path]:
        stem = f"{slugify(content_path)}-{frame_rate}fps"
        session_config_client_1 = copy.deepcopy(experiment.sessionConfig)
        session_config_client_2 = copy.deepcopy(experiment.sessionConfig)
        session_config_client_1["camFPS"] = frame_rate
        session_config_client_2["camFPS"] = frame_rate
        if not session_config_client_1.get("supportModes"):
            session_config_client_1["supportModes"] = [{"modeName": "mdc"}]
        if not session_config_client_2.get("supportModes"):
            session_config_client_2["supportModes"] = [{"modeName": "spectator"}]
        session_config_client_1["supportModes"][0]["modeName"] = "mdc"
        session_config_client_2["supportModes"][0]["modeName"] = "spectator"
        plyfiles_config = copy.deepcopy(experiment.plyfilesConfig)
        if not plyfiles_config:
            plyfiles_config = [{"directoryPath": content_path}]
        else:
            plyfiles_config[0]["directoryPath"] = content_path

        client_1_path = controller_state.exp_configs_dir / f"{stem}-client1-session_config.json"
        client_2_path = controller_state.exp_configs_dir / f"{stem}-client2-session_config.json"
        plyfiles_path = controller_state.exp_configs_dir / f"{stem}-plyfiles.json"
        save_json(client_1_path, session_config_client_1)
        save_json(client_2_path, session_config_client_2)
        save_json(plyfiles_path, plyfiles_config)
        return {
            "client_1": client_1_path,
            "client_2": client_2_path,
            "plyfiles": plyfiles_path,
        }

    async def upload_configs_to_clients(client: httpx.AsyncClient, experiment: ExperimentConfig, combo: dict[str, Any]) -> None:
        connected_clients = list(state.clients.values())
        if not connected_clients:
            raise RuntimeError("No connected client provisioners")
        config_set = combo["config_set"]
        session_config_paths = [config_set["client_1"], config_set["client_2"]]
        for index, client_record in enumerate(connected_clients):
            session_config_payload = ConfigUploadPayload(
                destination_path="config/session_config.json",
                json_content=load_json(session_config_paths[min(index, len(session_config_paths) - 1)], {}),
            )
            response = await client.post(f"{client_record.base_url}/api/config/upload", json=session_config_payload.model_dump())
            response.raise_for_status()
            plyfiles_payload = ConfigUploadPayload(
                destination_path="config/camera/plyfiles.json",
                json_content=load_json(config_set["plyfiles"], []),
            )
            response = await client.post(f"{client_record.base_url}/api/config/upload", json=plyfiles_payload.model_dump())
            response.raise_for_status()

    def apply_network_configuration(network_configuration: NetworkConfiguration | None) -> None:
        if not settings.enable_network_shaping:
            return
        if network_configuration is None:
            return
        if settings.qdisc_script is None:
            raise RuntimeError("Network shaping is enabled but no --qdisc-script was provided")
        command = [str(settings.qdisc_script), "apply", network_configuration.targetIP]
        if network_configuration.bandwidthKbit is not None:
            command.extend(["--bandwidth-kbit", str(network_configuration.bandwidthKbit)])
        if network_configuration.latencyMs is not None:
            command.extend(["--latency-ms", str(network_configuration.latencyMs)])
        if network_configuration.jitterMs is not None:
            command.extend(["--jitter-ms", str(network_configuration.jitterMs)])
        if network_configuration.packetLossPercent is not None:
            command.extend(["--loss-percent", str(network_configuration.packetLossPercent)])
        run_command(command)

    async def start_clients(client: httpx.AsyncClient) -> None:
        payload = ClientStartPayload(command=settings.client_command)
        for client_record in state.clients.values():
            response = await client.post(f"{client_record.base_url}/api/start", json=payload.model_dump())
            response.raise_for_status()
            await asyncio.sleep(2)

    async def stop_clients(client: httpx.AsyncClient) -> None:
        for client_record in state.clients.values():
            await client.post(f"{client_record.base_url}/api/quit")

    async def collect_logs(client: httpx.AsyncClient, combo: dict[str, Any]) -> Path:
        label = build_experiment_label(combo["iteration"], combo["network_configuration"], combo["content_path"], combo["frame_rate"])
        experiment_dir = ensure_directory(state.logs_dir / label / utc_timestamp())
        for client_record in state.clients.values():
            response = await client.post(
                f"{client_record.base_url}/api/logs/download",
                json=DownloadLogsPayload().model_dump(),
            )
            response.raise_for_status()
            archive_path = experiment_dir / f"{client_record.client_name}-logs.tar.gz"
            archive_path.write_bytes(response.content)
        for local_log_dir in settings.local_log_dirs:
            local_target = ensure_directory(experiment_dir / "local" / local_log_dir.name)
            if local_log_dir.exists():
                shutil.copytree(local_log_dir, local_target, dirs_exist_ok=True)
                archived_target = ensure_directory(state.archive_dir / utc_timestamp() / local_log_dir.name)
                move_directory_contents(local_log_dir, archived_target)
        save_json(
            experiment_dir / "experiment.json",
            {
                "iteration": combo["iteration"],
                "frame_rate": combo["frame_rate"],
                "content_path": combo["content_path"],
                "network_configuration": (
                    combo["network_configuration"].model_dump() if combo["network_configuration"] is not None else None
                ),
            },
        )
        return experiment_dir

    async def upload_logs_to_general(client: httpx.AsyncClient, experiment_dir: Path) -> None:
        archive_payload = archive_directory_to_bytes(experiment_dir)
        relative_path = str(experiment_dir.relative_to(settings.data_dir)) + ".tar.gz"
        files = {"file": (f"{experiment_dir.name}.tar.gz", archive_payload, "application/gzip")}
        data = {"relative_path": relative_path}
        response = await client.post(
            f"{settings.general_controller_url.rstrip('/')}/api/instances/{settings.instance_id}/logs",
            data=data,
            files=files,
        )
        response.raise_for_status()

    def build_experiment_label(iteration: int, network_configuration: NetworkConfiguration | None, content_path: str, frame_rate: int) -> str:
        parts = [
            f"iter-{iteration}",
            f"content-{slugify(content_path)}",
            f"fps-{frame_rate}",
        ]
        if network_configuration is None:
            parts.append("target-none")
            return "__".join(parts)
        parts.append(f"target-{slugify(network_configuration.targetIP)}")
        if network_configuration.bandwidthKbit is not None:
            parts.append(f"bw-{network_configuration.bandwidthKbit}")
        if network_configuration.latencyMs is not None:
            parts.append(f"lat-{network_configuration.latencyMs}")
        if network_configuration.packetLossPercent is not None:
            parts.append(f"loss-{network_configuration.packetLossPercent}")
        return "__".join(parts)

    return app
