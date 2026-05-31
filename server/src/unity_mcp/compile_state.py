"""Heuristic detector for Unity C# compile / domain-reload windows.

Pure stdlib, instance-local state. Degrades gracefully: unknown project path =>
is_unity_busy() always False.
"""
import os
import time
from pathlib import Path
from typing import Optional

from unity_mcp.lockfile import read_pid_from_port_file, is_pid_alive
from unity_mcp.unity_state import read_state_for_port

_DISCONNECT_WINDOW_S = 30.0
_DEFAULT_REMAINING_S = 5.0
_MAX_REMAINING_S = 60.0


class CompileStateProbe:
    def __init__(self, unity_project_path: Optional[Path] = None,
                 port: Optional[int] = None) -> None:
        self._path = unity_project_path
        self._port: Optional[int] = port
        self._last_disconnect_ts: Optional[float] = None
        self._compile_elapsed: Optional[float] = None
        self._last_known_duration: Optional[float] = None

    @property
    def has_project(self) -> bool:
        return self._path is not None

    def mark_recompile_issued(self) -> None:
        self._last_disconnect_ts = time.monotonic()

    def is_unity_busy(self) -> bool:
        if self._recent_disconnect():
            return True
        if self._lock_file_exists():
            return True
        return False

    def is_process_dead(self) -> bool:
        """True when we know the Unity process is dead (PID found but not alive)."""
        if self._port is None:
            return False
        pid = read_pid_from_port_file(self._port)
        return pid is not None and not is_pid_alive(pid)

    def has_strong_busy_signal(self) -> bool:
        """State file (authoritative) → lock file."""
        if self._port is not None:
            state = read_state_for_port(self._port)
            if state is not None:
                if not state.is_stale and state.is_busy:
                    return True
            elif self.is_process_dead():
                return False
        return self._lock_file_exists()

    def estimated_remaining_s(self) -> float:
        if self._last_known_duration and self._compile_elapsed is not None:
            remaining = self._last_known_duration - self._compile_elapsed
            return min(_MAX_REMAINING_S, max(1.0, remaining))
        return _DEFAULT_REMAINING_S

    def update_compile_info(self, status_str: str) -> None:
        """Parse 'compiling|12.3' or 'idle|8.2' from compile_status response."""
        try:
            parts = status_str.split("|")
            if len(parts) != 2:
                return
            state, val = parts[0], float(parts[1])
            if state == "compiling":
                self._compile_elapsed = val
                self._last_known_duration = max(self._last_known_duration or 0, val)
            elif state == "idle" and val > 0:
                self._last_known_duration = val
                self._compile_elapsed = None
        except (ValueError, IndexError):
            pass

    @staticmethod
    def autodetect_project_path() -> Optional[Path]:
        p = os.environ.get("UNITY_MCP_PROJECT_PATH")
        if p:
            path = Path(p)
            if path.exists() and (path / "Library").exists():
                return path
        return None

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    def _recent_disconnect(self) -> bool:
        if self._last_disconnect_ts is None:
            return False
        return (time.monotonic() - self._last_disconnect_ts) < _DISCONNECT_WINDOW_S

    def _lock_file_exists(self) -> bool:
        if self._path is None:
            return False
        try:
            return (self._path / "Library" / "BeeDriver" / "Lock").exists()
        except OSError:
            return False
