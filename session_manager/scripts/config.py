from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional

@dataclass
class ProviderConfig:
    type: str = ""
    key: str = ""
    connectedTo: List[str] = field(default_factory=list)

@dataclass
class RootConfig:
    address: str = ""
    verifyAuthKey: bool = False
    ignorePreferredClientID: bool = False
    defaultProviderConfigPath: str = ""
    provisionerType: str = ""
    provisionerConfigPath: str = ""
    providersToCreate: List[ProviderConfig] = field(default_factory=list)
    enableMetrics: bool = False
    metricsServerAddress: str = ""
    providerAsMetricsServer: str = ""
    metricsConfigPath: str = ""


def load_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(f"JSON file not found: {path}")
    with path.open("r", encoding="utf-8") as fh:
        return json.load(fh)


def parse_root(d: Dict[str, Any]) -> RootConfig:
    rc = RootConfig()
    # optional controller IP can be provided at top-level
    if "address" in d:
        rc.address = d.get("address") or rc.address
    if "verifyAuthKey" in d:
        rc.verifyAuthKey = bool(d.get("verifyAuthKey"))
    if "ignorePreferredClientID" in d:
        rc.ignorePreferredClientID = bool(d.get("ignorePreferredClientID"))
    if "defaultProviderConfigPath" in d:
        rc.defaultProviderConfigPath = d.get("defaultProviderConfigPath") or rc.defaultProviderConfigPath
    if "provisionerType" in d:
        rc.provisionerType = d.get("provisionerType") or rc.provisionerType
    if "provisionerConfigPath" in d:
        rc.provisionerConfigPath = d.get("provisionerConfigPath") or rc.provisionerConfigPath
    for p in d.get("providersToCreate", []) or []:
        rc.providersToCreate.append(
            ProviderConfig(
                type=p.get("type") or "",
                key=p.get("key") or "",
                connectedTo=p.get("connectedTo") or [],
            )
        )
    if "enableMetrics" in d:
        rc.enableMetrics = bool(d.get("enableMetrics"))
    if "metricsServerAddress" in d:
        rc.metricsServerAddress = d.get("metricsServerAddress") or rc.metricsServerAddress
    if "providerAsMetricsServer" in d:
        rc.providerAsMetricsServer = d.get("providerAsMetricsServer") or rc.providerAsMetricsServer
    if "metricsConfigPath" in d:
        rc.metricsConfigPath = d.get("metricsConfigPath") or rc.metricsConfigPath

    return rc


def validate_config(cfg: RootConfig) -> List[str]:
    errs: List[str] = []
    if not cfg.address:
        errs.append("address is required")
    for i, p in enumerate(cfg.providersToCreate):
        if not p.type:
            errs.append(f"providersToCreate[{i}].type is required")
        if not p.key:
            errs.append(f"providersToCreate[{i}].key is required")
    if cfg.enableMetrics:
        if not cfg.metricsConfigPath:
            errs.append("metricsConfigPath is required when enableMetrics is true")
    return errs


def print_summary(cfg: RootConfig) -> None:
    print("Parsed configuration summary:\n")
    print(f"  Address: {cfg.address}")
    print(f"  Verify Auth Key: {cfg.verifyAuthKey}")
    print(f"  Ignore Preferred Client ID: {cfg.ignorePreferredClientID}")
    print(f"  Default Provider Config Path: {cfg.defaultProviderConfigPath}")
    print(f"  Provisioner Type: {cfg.provisionerType}")
    print(f"  Provisioner Config Path: {cfg.provisionerConfigPath}")
    print(f"  Providers to Create ({len(cfg.providersToCreate)}):")
    for i, p in enumerate(cfg.providersToCreate):
        print(f"    [{i}] Type: {p.type}, Key: {p.key}, Connected To: {p.connectedTo}")
    print(f"  Enable Metrics: {cfg.enableMetrics}")
    print(f"  Metrics Server Address: {cfg.metricsServerAddress}")
    print(f"  Provider as Metrics Server: {cfg.providerAsMetricsServer}")
    print(f"  Metrics Config Path: {cfg.metricsConfigPath}")
