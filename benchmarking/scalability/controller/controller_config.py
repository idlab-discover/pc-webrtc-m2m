@dataclass
class ControllerConfig:
    sessionManagerPath: str = "session_manager"

# Controller also needs to have "subscribe" endpoint so lient nodes can subscribe themself to this controller