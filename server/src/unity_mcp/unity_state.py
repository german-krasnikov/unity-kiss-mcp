"""Unity Editor state file reader.

Unity writes ~/.unity-mcp/state/port-{port}.state with:
  line 0: state name (ready/compiling/reloading)
  line 1: unix timestamp when state changed
  line 2: Unity process PID (written by C# but ignored by Python)
"""
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

_STALE_SECONDS = 120.0


@dataclass
class UnityState:
    state: str
    timestamp: float

    @property
    def is_busy(self) -> bool:
        return self.state in ("compiling", "reloading", "restarting")

    @property
    def is_stale(self) -> bool:
        return (time.time() - self.timestamp) > _STALE_SECONDS


def read_state_for_port(port: int) -> Optional[UnityState]:
    """Read Unity state from ~/.unity-mcp/state/port-{port}.state."""
    path = Path.home() / ".unity-mcp" / "state" / f"port-{port}.state"
    try:
        lines = path.read_text().strip().split("\n")
        if len(lines) < 2:
            return None
        return UnityState(state=lines[0], timestamp=float(lines[1]))
    except (OSError, ValueError, IndexError):
        return None
