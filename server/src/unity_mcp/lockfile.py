"""PID lockfile — exclusive lock, kills old unity_mcp server if needed."""
import fcntl
import logging
import os
import subprocess
import time
from pathlib import Path
from typing import Optional

log = logging.getLogger("unity_mcp.lockfile")

_MAX_RETRIES = 2
_MAX_KILL_WAIT_S = 3.0
_POLL_INTERVAL_S = 0.1


def _is_unity_mcp_pid(pid: int) -> bool:
    """Return True if pid's cmdline contains 'unity_mcp'."""
    try:
        out = subprocess.check_output(
            ["ps", "-p", str(pid), "-o", "command="],
            stderr=subprocess.DEVNULL,
        )
        return b"unity_mcp" in out
    except subprocess.CalledProcessError:
        return False


def _read_pid_from_fd(fd: int) -> Optional[int]:
    os.lseek(fd, 0, os.SEEK_SET)
    data = os.read(fd, 32).decode(errors="ignore").strip()
    try:
        return int(data)
    except ValueError:
        return None


def acquire_lock(lock_dir=None, port: int = 9500) -> int:
    if lock_dir is None:
        lock_dir = Path.home() / ".unity-mcp"
    lock_dir = Path(lock_dir)
    lock_dir.mkdir(parents=True, exist_ok=True)
    lock_file = lock_dir / f"server-{port}.lock"

    fd = os.open(str(lock_file), os.O_RDWR | os.O_CREAT | os.O_CLOEXEC, 0o600)

    for attempt in range(_MAX_RETRIES + 1):
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            # Got the lock
            os.ftruncate(fd, 0)
            os.lseek(fd, 0, os.SEEK_SET)
            os.write(fd, f"{os.getpid()}\n".encode())
            return fd
        except BlockingIOError:
            if attempt == _MAX_RETRIES:
                break
            # Read PID from lockfile and maybe kill it
            old_pid = _read_pid_from_fd(fd)
            if old_pid and is_pid_alive(old_pid) and _is_unity_mcp_pid(old_pid):
                log.info("Killing stale unity_mcp server pid=%d", old_pid)
                os.kill(old_pid, 15)  # SIGTERM
            # Poll until lock is free
            deadline = time.monotonic() + _MAX_KILL_WAIT_S
            while time.monotonic() < deadline:
                time.sleep(_POLL_INTERVAL_S)
                try:
                    fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                    os.ftruncate(fd, 0)
                    os.lseek(fd, 0, os.SEEK_SET)
                    os.write(fd, f"{os.getpid()}\n".encode())
                    return fd
                except BlockingIOError:
                    continue

    os.close(fd)
    raise RuntimeError(f"Cannot acquire exclusive lock for port {port}")


def release_lock(fd: int):
    fcntl.flock(fd, fcntl.LOCK_UN)
    os.close(fd)


def read_pid_from_port_file(port: int) -> Optional[int]:
    """Read Unity PID from ~/.unity-mcp/ports/{pid}.port matching the given port."""
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if not ports_dir.exists():
        return None
    for f in ports_dir.glob("*.port"):
        try:
            lines = f.read_text().strip().split("\n")
            if int(lines[0]) == port:
                return int(f.stem)
        except (ValueError, IndexError, OSError):
            continue
    return None


def is_pid_alive(pid: Optional[int]) -> bool:
    """Return True if the process with given PID exists."""
    if pid is None:
        return False
    try:
        os.kill(pid, 0)
        return True
    except (OSError, ProcessLookupError):
        return False
