"""Append-only JSONL crash/disconnect logger with entry-count rotation."""
import atexit
import json
import time
import traceback
from pathlib import Path
from typing import Optional


def _crash_path(log_dir: Optional[Path] = None) -> Path:
    return Path(log_dir or Path.home() / ".unity-mcp") / "crash.jsonl"


def log_crash(exc: BaseException, *, log_dir=None) -> None:
    try:
        path = _crash_path(log_dir)
        path.parent.mkdir(parents=True, exist_ok=True)
        entry = {
            "ev": "crash",
            "t": time.time(),
            "exc": type(exc).__name__,
            "msg": str(exc),
            "tb": "".join(traceback.format_exception(type(exc), exc, exc.__traceback__)),
        }
        with path.open("a") as f:
            f.write(json.dumps(entry) + "\n")
    except Exception:
        pass


class CrashLogger:
    """Append-only JSONL crash log with entry-count rotation."""

    MAX_BYTES = 15 * 1024 * 1024  # 15 MB

    def __init__(self, log_dir: Optional[Path] = None, max_entries: int = 500):
        self._path = _crash_path(log_dir)
        self._closed = False
        try:
            self._path.parent.mkdir(parents=True, exist_ok=True)
            self._path.touch(exist_ok=True)
            self._rotate(max_entries)
        except Exception:
            pass
        atexit.register(self.close)

    def _rotate(self, max_entries: int) -> None:
        try:
            size = self._path.stat().st_size if self._path.exists() else 0
            lines = self._path.read_text().splitlines()
            lines = [l for l in lines if l.strip()]
            if len(lines) >= max_entries or size > self.MAX_BYTES:
                keep = lines[-(max_entries // 2):]
                self._path.write_text("\n".join(keep) + "\n")
        except Exception:
            pass

    def _write(self, entry: dict) -> None:
        if self._closed:
            return
        try:
            if self._path.exists() and self._path.stat().st_size > self.MAX_BYTES:
                self._rotate(500)
            entry["t"] = time.time()
            with self._path.open("a") as f:
                f.write(json.dumps(entry) + "\n")
        except Exception:
            pass

    def log_disconnect(self, *, cmd: str, retry: int, error_type: str,
                       unity_busy: bool, port: int) -> None:
        self._write({"ev": "disconnect", "cmd": cmd, "retry": retry,
                     "err": error_type, "busy": unity_busy, "port": port})

    def log_reconnect(self, *, outage_s: float, retries: int, port: int) -> None:
        self._write({"ev": "reconnect", "outage_s": outage_s,
                     "retries": retries, "port": port})

    def close(self) -> None:
        self._closed = True
