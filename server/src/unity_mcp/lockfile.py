"""PID lockfile — exclusive lock, kills old unity_mcp server if needed."""
import logging
import os
import signal
import subprocess
import sys
import time
from pathlib import Path
from typing import Optional

log = logging.getLogger("unity_mcp.lockfile")

_IS_WIN = sys.platform == "win32"
_MAX_RETRIES = 2
_MAX_KILL_WAIT_S = 3.0
_POLL_INTERVAL_S = 0.1

# Lock sentinel byte lives at offset 1024 — far outside PID data (bytes 0-31).
# On Windows, mandatory locking blocks reads on locked bytes, so the lock region
# MUST NOT overlap PID data that other processes need to read.
# On Unix, advisory fcntl.flock locks the whole file anyway — offset is a no-op.
_LOCK_OFFSET = 1024

_OPEN_FLAGS = os.O_RDWR | os.O_CREAT | getattr(os, "O_CLOEXEC", 0)

if _IS_WIN:
    import msvcrt

    def _lock_nb(fd: int) -> None:
        """Non-blocking exclusive lock on sentinel byte at offset 1024."""
        os.lseek(fd, _LOCK_OFFSET, os.SEEK_SET)
        msvcrt.locking(fd, msvcrt.LK_NBLCK, 1)

    def _unlock(fd: int) -> None:
        """Release the sentinel byte lock."""
        os.lseek(fd, _LOCK_OFFSET, os.SEEK_SET)
        msvcrt.locking(fd, msvcrt.LK_UNLCK, 1)

else:
    import fcntl

    def _lock_nb(fd: int) -> None:
        """Non-blocking exclusive flock (advisory, whole-file)."""
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)

    def _unlock(fd: int) -> None:
        fcntl.flock(fd, fcntl.LOCK_UN)


def _write_pid(fd: int) -> None:
    """Write current PID to bytes 0-31 of the lockfile."""
    os.ftruncate(fd, 0)
    os.lseek(fd, 0, os.SEEK_SET)
    os.write(fd, f"{os.getpid()}\n".encode())


def _read_pid_from_fd(fd: int) -> Optional[int]:
    """Read PID from bytes 0-31. Always readable — outside the locked region."""
    os.lseek(fd, 0, os.SEEK_SET)
    data = os.read(fd, 32).decode(errors="ignore").strip()
    try:
        return int(data)
    except ValueError:
        return None


def _is_zombie(pid: int) -> bool:
    """Return True if process is a zombie (stat contains 'Z'). Windows always False."""
    if _IS_WIN:
        return False
    try:
        out = subprocess.check_output(
            ["ps", "-p", str(pid), "-o", "stat="],
            stderr=subprocess.DEVNULL,
        )
        return b"Z" in out
    except subprocess.CalledProcessError:
        return False


def _is_unity_mcp_pid(pid: int) -> bool:
    """Return True if pid's cmdline belongs to unity_mcp."""
    if _IS_WIN:
        return _is_unity_mcp_pid_win(pid)
    try:
        out = subprocess.check_output(
            ["ps", "-p", str(pid), "-o", "command="],
            stderr=subprocess.DEVNULL,
        )
        return b"unity_mcp" in out
    except subprocess.CalledProcessError:
        return False


def _is_unity_mcp_pid_win(pid: int) -> bool:
    """Windows: check cmdline via PowerShell CIM; fallback to tasklist weak check."""
    # Primary: PowerShell CIM gives us the full command line (matches unity_mcp exactly).
    try:
        out = subprocess.check_output(
            [
                "powershell", "-NoProfile", "-Command",
                f"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; "
                f"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}').CommandLine",
            ],
            stderr=subprocess.DEVNULL,
        )
        return b"unity_mcp" in out.lower()
    except (OSError, subprocess.SubprocessError):
        pass  # PowerShell unavailable — fall through to tasklist

    # Fallback: tasklist gives only image name, not full cmdline — weaker guarantee
    # (any recycled python PID would match). Documented intentional limitation.
    try:
        out = subprocess.check_output(
            ["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
            stderr=subprocess.DEVNULL,
        )
        return b"python" in out.lower()
    except (FileNotFoundError, subprocess.SubprocessError, OSError):
        return False  # fail-safe: don't kill what we can't identify


def _kill_pid(pid: int) -> None:
    """Send SIGTERM (Unix) or TerminateProcess (Windows). Windows is abrupt — no cleanup."""
    try:
        os.kill(pid, signal.SIGTERM)
    except (ProcessLookupError, PermissionError, OSError):
        pass


def is_pid_alive(pid: Optional[int]) -> bool:
    """Return True if the process with given PID exists."""
    if pid is None:
        return False
    try:
        os.kill(pid, 0)
        return True
    except PermissionError:
        return True  # exists but can't signal — common on Windows
    except (OSError, ProcessLookupError):
        return False


def acquire_lock(lock_dir=None, port: int = 9500) -> int:
    if lock_dir is None:
        lock_dir = Path.home() / ".unity-mcp"
    lock_dir = Path(lock_dir)
    lock_dir.mkdir(parents=True, exist_ok=True)
    lock_file = lock_dir / f"server-{port}.lock"

    fd = os.open(str(lock_file), _OPEN_FLAGS, 0o600)

    for attempt in range(_MAX_RETRIES + 1):
        try:
            _lock_nb(fd)
            # Got the lock — write our PID
            _write_pid(fd)
            return fd
        except (BlockingIOError, OSError):
            if attempt == _MAX_RETRIES:
                break
            # PID data (bytes 0-31) is always readable — outside locked region.
            old_pid = _read_pid_from_fd(fd)
            if attempt == 0 and old_pid and is_pid_alive(old_pid) and not _is_zombie(old_pid) and _is_unity_mcp_pid(old_pid):
                log.info("Sending SIGTERM to old MCP session PID %d on port %d", old_pid, port)
                _kill_pid(old_pid)
            # Dead/zombie lock — wait for it to be released
            deadline = time.monotonic() + _MAX_KILL_WAIT_S
            while time.monotonic() < deadline:
                time.sleep(_POLL_INTERVAL_S)
                try:
                    _lock_nb(fd)
                    _write_pid(fd)
                    return fd
                except (BlockingIOError, OSError):
                    continue

    os.close(fd)
    raise RuntimeError(f"Cannot acquire exclusive lock for port {port}")


def release_lock(fd: int):
    try:
        _unlock(fd)
    except OSError:
        pass  # already unlocked (process dying, fd closing)
    os.close(fd)


def read_pid_from_port_file(port: int) -> Optional[int]:
    """Read Unity PID from ~/.unity-mcp/ports/{pid}.port matching the given port."""
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if not ports_dir.exists():
        return None
    for f in ports_dir.glob("*.port"):
        try:
            lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
            if int(lines[0]) == port:
                return int(f.stem)
        except (ValueError, IndexError, OSError):
            continue
    return None


def read_reload_port() -> Optional[int]:
    """Discover reload mini-server port from ~/.unity-mcp/ports/{pid}.reload-port.

    F2: multi-line format port\\nProjectDir\\nProjectName.
    CWD longest-match selects among multiple alive instances (mirrors read_unity_port).
    Falls back to most-recently-modified file if no CWD match.
    """
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if not ports_dir.exists():
        return None

    candidates = []  # (path_len, mtime, port, project_path)
    for f in ports_dir.glob("*.reload-port"):
        try:
            pid = int(f.stem)
            if not is_pid_alive(pid):
                continue
            lines = f.read_text(encoding="utf-8").strip().split("\n")
            port = int(lines[0])
            project_path = lines[1].strip() if len(lines) > 1 else ""
            candidates.append((f.stat().st_mtime, port, project_path))
        except (ValueError, OSError):
            continue

    if not candidates:
        return None

    if len(candidates) == 1:
        return candidates[0][1]

    # CWD longest-match disambiguation (mirrors server_filtering.py:read_unity_port)
    cwd = os.getcwd()
    cwd_matches = [
        (len(pp), mtime, port)
        for mtime, port, pp in candidates
        if pp and (cwd == pp or cwd.startswith(pp + os.sep))
    ]
    if cwd_matches:
        cwd_matches.sort(reverse=True)
        return cwd_matches[0][2]

    candidates.sort(reverse=True)  # newest mtime first
    return candidates[0][1]


def read_project_path_from_port_file(port: int) -> Optional[Path]:
    """Read Unity project path from ~/.unity-mcp/ports/{pid}.port matching the given port.

    Port file format: line0=port, line1=absolute project path, line2=project name.

    P8: PID-liveness gate — skip stale .port files from dead Unity processes.
    Without this, a dead Unity's port file could resolve the wrong project's dll
    when a new Unity starts on the same port.

    Returns Path(lines[1]) if it exists on disk AND the PID is alive, else None.
    """
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if not ports_dir.exists():
        return None
    for f in ports_dir.glob("*.port"):
        try:
            pid = int(f.stem)
            lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
            if int(lines[0]) != port or len(lines) < 2:
                continue
            # P8: skip stale port files from dead processes
            if not is_pid_alive(pid):
                continue
            p = Path(lines[1])
            if p.exists():
                return p
        except (ValueError, IndexError, OSError):
            continue
    return None
