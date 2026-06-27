"""Domain reload state tracker — shared between bridge and heartbeat."""
import time
from dataclasses import dataclass, field
from typing import Optional

DOMAIN_RELOAD_EXPIRY_S: float = 120.0  # was 90.0 — 9 assemblies can take 60s+


@dataclass
class DomainReloadTracker:
    _active: bool = field(default=False, init=False)
    _since: Optional[float] = field(default=None, init=False)

    def mark(self) -> None:
        self._active = True
        self._since = time.monotonic()

    def clear(self) -> None:
        self._active = False
        self._since = None

    def is_active(self) -> bool:
        if not self._active or self._since is None:
            return False
        if time.monotonic() - self._since > DOMAIN_RELOAD_EXPIRY_S:
            self._active = False
            self._since = None
            return False
        return True

    def elapsed(self) -> float:
        if self._since is None:
            return 0.0
        return time.monotonic() - self._since
