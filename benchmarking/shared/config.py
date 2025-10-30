from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional


@dataclass
class SessionManagerConfig:
    nodeID: str
    configDirectoryToCopy: List[str] = field(default_factory=list)
    sessionManagerIP: Optional[str] = None  # New optional field for IP:port

@dataclass
class ProviderConfig:
    nodeID: str
    providerType: str
    providerKey: Optional[str] = None
    ipFilter: str = ""
    configDirectoryToCopy: List[str] = field(default_factory=list)
    connectedTo: List[str] = field(default_factory=list)


@dataclass
class ClientConfig:
    nodeID: str
    clientType: str
    nClients: Optional[int] = 1
    transcoderDirectoryToCopy: List[str] = field(default_factory=list)
    providersConfigToCopy: List[str] = field(default_factory=list)
    # New fields parsed from JSON
    transcoderConfig: Optional[str] = None
    providersConfig: Optional[str] = None
    transcoderType: Optional[str] = None


@dataclass
class RootConfig:
    copyConfigs: bool = False
    controllerIP: str = "127.0.0.1"
    sessionManagerConfig: Optional[SessionManagerConfig] = None
    nIterations: Optional[int] = 1  # New optional field for number of iterations
    experimentDurationSeconds: Optional[int] = 60  # New optional field for duration in seconds
    providers: List[ProviderConfig] = field(default_factory=list)
    clients: List[ClientConfig] = field(default_factory=list)


def load_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        raise FileNotFoundError(f"JSON file not found: {path}")
    with path.open("r", encoding="utf-8") as fh:
        return json.load(fh)


def parse_root(d: Dict[str, Any]) -> RootConfig:
    rc = RootConfig()
    rc.copyConfigs = bool(d.get("copyConfigs", False))
    # optional controller IP can be provided at top-level
    if "controllerIP" in d:
        rc.controllerIP = d.get("controllerIP") or rc.controllerIP
    # optional numeric top-level fields
    if "nIterations" in d:
        try:
            n = d.get("nIterations")
            rc.nIterations = int(n) if n is not None and n != "" else rc.nIterations
        except Exception:
            # keep default on parse error
            pass

    if "experimentDurationSeconds" in d:
        try:
            ed = d.get("experimentDurationSeconds")
            rc.experimentDurationSeconds = int(ed) if ed is not None and ed != "" else rc.experimentDurationSeconds
        except Exception:
            # keep default on parse error
            pass

    sm = d.get("sessionManagerConfig")
    if sm:
        rc.sessionManagerConfig = SessionManagerConfig(
            nodeID=sm.get("nodeID", ""),
            configDirectoryToCopy=sm.get("configDirectoryToCopy", []),
            sessionManagerIP=sm.get("sessionManagerIP")
        )

    for p in d.get("providers", []) or []:
        rc.providers.append(
            ProviderConfig(
                nodeID=p.get("nodeID", ""),
                providerType=p.get("providerType", ""),
                providerKey=p.get("providerKey"),
                ipFilter=p.get("ipFilter", ""),
                configDirectoryToCopy=p.get("configDirectoryToCopy", []),
                connectedTo=p.get("connectedTo", []),
            )
        )

    for c in d.get("clients", []) or []:
        # convert nClients to int if possible
        n = c.get("nClients")
        try:
            n_int = int(n) if n is not None and n != "" else None
        except Exception:
            n_int = None
        rc.clients.append(
            ClientConfig(
                nodeID=c.get("nodeID", ""),
                clientType=c.get("clientType", ""),
                nClients=n_int,
                transcoderDirectoryToCopy=c.get("transcoderDirectoryToCopy", []),
                providersConfigToCopy=c.get("providersConfigToCopy", []),
                transcoderConfig=c.get("transcoderConfig"),
                providersConfig=c.get("providersConfig"),
                transcoderType=c.get("transcoderType"),
            )
        )

    return rc


def validate_config(cfg: RootConfig) -> List[str]:
    errs: List[str] = []
    if cfg.sessionManagerConfig is None:
        errs.append("missing sessionManagerConfig")
    else:
        if not cfg.sessionManagerConfig.nodeID:
            errs.append("sessionManagerConfig.nodeID is empty")

    for i, p in enumerate(cfg.providers):
        if not p.nodeID:
            errs.append(f"provider[{i}].nodeID is empty")
        if not p.providerType:
            errs.append(f"provider[{i}].providerType is empty")

    for i, c in enumerate(cfg.clients):
        if not c.nodeID:
            errs.append(f"client[{i}].nodeID is empty")
        if not c.clientType:
            errs.append(f"client[{i}].clientType is empty")

    return errs


def print_summary(cfg: RootConfig) -> None:
    print("Parsed configuration summary:\n")
    print(f"copyConfigs: {cfg.copyConfigs}")
    print(f"nIterations: {cfg.nIterations}")
    print(f"experimentDurationSeconds: {cfg.experimentDurationSeconds}")
    if cfg.sessionManagerConfig:
        sm = cfg.sessionManagerConfig
        print(f"sessionManager.nodeID: {sm.nodeID}")
        print(f"sessionManager.configDirectoryToCopy: {sm.configDirectoryToCopy}")
    else:
        print("sessionManager: <none>")

    print("\nProviders:")
    if not cfg.providers:
        print("  <none>")
    for p in cfg.providers:
        print(f"  - nodeID: {p.nodeID}")
        print(f"    type: {p.providerType}")
        print(f"    providerKey: {p.providerKey}")
        print(f"    connectedTo: {p.connectedTo}")

    print("\nClients:")
    if not cfg.clients:
        print("  <none>")
    for c in cfg.clients:
        print(f"  - nodeID: {c.nodeID}")
        print(f"    type: {c.clientType}")
        print(f"    nClients: {c.nClients}")
        print(f"    transcoderDirectoryToCopy: {c.transcoderDirectoryToCopy}")
        print(f"    providersConfigToCopy: {c.providersConfigToCopy}")
        print(f"    transcoderConfig: {c.transcoderConfig}")
        print(f"    providersConfig: {c.providersConfig}")
        print(f"    transcoderType: {c.transcoderType}")
