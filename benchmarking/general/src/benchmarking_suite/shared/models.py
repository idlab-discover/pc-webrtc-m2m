from __future__ import annotations

from pathlib import Path
from typing import Any

from pydantic import BaseModel, Field, HttpUrl


class ContentInventoryEntry(BaseModel):
    name: str
    file_count: int


class ClientConnectionPayload(BaseModel):
    client_id: str
    client_name: str
    base_url: HttpUrl
    inventory: list[ContentInventoryEntry] = Field(default_factory=list)


class InstanceConnectionPayload(BaseModel):
    instance_id: str
    instance_name: str
    base_url: HttpUrl
    metadata: dict[str, Any] = Field(default_factory=dict)


class NetworkConfiguration(BaseModel):
    targetIP: str
    latencyMs: int | None = None
    bandwidthKbit: int | None = None
    packetLossPercent: float | None = None
    jitterMs: int | None = None


class ExperimentConfig(BaseModel):
    iterations: int = Field(gt=0)
    durationSeconds: int = Field(gt=0)
    networkConfigurations: list[NetworkConfiguration] = Field(default_factory=list)
    contentPaths: list[str] = Field(default_factory=list)
    frameRates: list[int] = Field(default_factory=list)
    sessionConfig: dict[str, Any] = Field(default_factory=dict)
    plyfilesConfig: list[dict[str, Any]] = Field(default_factory=list)


class StartExperimentPayload(BaseModel):
    experiment: ExperimentConfig


class StatusUpdatePayload(BaseModel):
    instance_id: str
    phase: str
    message: str = ""
    progress: dict[str, Any] = Field(default_factory=dict)


class ClientStartPayload(BaseModel):
    command: list[str]
    cwd: str | None = None
    env: dict[str, str] = Field(default_factory=dict)


class ConfigUploadPayload(BaseModel):
    destination_path: str
    json_content: Any


class DownloadLogsPayload(BaseModel):
    path: str | None = None


class ExperimentRuntimeConfig(BaseModel):
    iteration: int
    frame_rate: int
    content_path: str
    network_configuration: NetworkConfiguration
    session_config_client_1: dict[str, Any]
    session_config_client_2: dict[str, Any]
    plyfiles_config: list[dict[str, Any]]


class SavedConfigSet(BaseModel):
    session_config_path: Path
    spectator_session_config_path: Path
    plyfiles_config_path: Path
