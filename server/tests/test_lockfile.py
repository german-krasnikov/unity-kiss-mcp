"""TDD tests for lockfile.py — exclusive lock with kill-old behavior."""
import fcntl
import os
import subprocess
import time
from pathlib import Path
from unittest.mock import patch, call, MagicMock

import pytest

from unity_mcp.lockfile import (
    acquire_lock, release_lock, read_pid_from_port_file, is_pid_alive,
    _lock_nb, _unlock,
)


def test_acquire_creates_file_with_pid(tmp_path):
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_file = tmp_path / "server-9500.lock"
    assert lock_file.exists()
    assert int(lock_file.read_text().strip()) == os.getpid()
    release_lock(fd)


def test_release_lock(tmp_path):
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    with patch("fcntl.flock") as mock_flock, patch("os.close") as mock_close:
        release_lock(fd)
        mock_flock.assert_called_once_with(fd, fcntl.LOCK_UN)
        mock_close.assert_called_once_with(fd)


def test_acquire_after_release(tmp_path):
    fd1 = acquire_lock(lock_dir=tmp_path, port=9500)
    release_lock(fd1)
    fd2 = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_file = tmp_path / "server-9500.lock"
    assert int(lock_file.read_text().strip()) == os.getpid()
    release_lock(fd2)


def test_different_ports_dont_conflict(tmp_path):
    """Two locks on different ports should coexist."""
    fd1 = acquire_lock(lock_dir=tmp_path, port=9500)
    fd2 = acquire_lock(lock_dir=tmp_path, port=9501)
    assert (tmp_path / "server-9500.lock").exists()
    assert (tmp_path / "server-9501.lock").exists()
    release_lock(fd1)
    release_lock(fd2)


def test_lockfile_o_cloexec(tmp_path):
    """O_CLOEXEC must be set on the lock file descriptor."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    try:
        flags = fcntl.fcntl(fd, fcntl.F_GETFD)
        assert flags & fcntl.FD_CLOEXEC, "O_CLOEXEC not set"
    finally:
        release_lock(fd)


# ---------------------------------------------------------------------------
# Kill-and-retake behavior
# ---------------------------------------------------------------------------

def test_takeover_kills_and_acquires_for_live_unity_mcp_process(tmp_path):
    """When lock is held by a live unity_mcp process, SIGTERM is sent and lock is acquired."""
    lock_file = tmp_path / "server-9500.lock"
    fake_pid = 54321
    lock_file.write_text(f"{fake_pid}\n")

    call_count = {"n": 0}
    def flock_side_effect(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError
        # Second call (in wait loop) succeeds

    with (
        patch("fcntl.flock", side_effect=flock_side_effect),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("unity_mcp.lockfile._kill_pid") as mock_kill_pid,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill_pid.assert_called_once_with(fake_pid)


def test_stale_lock_no_kill_if_pid_dead(tmp_path):
    """If lock is held but PID is dead (stale), no SIGTERM is sent — just retake."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    call_count = {"n": 0}
    def flock_side_effect(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError
        # Second call succeeds

    with (
        patch("fcntl.flock", side_effect=flock_side_effect),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=False),
        patch("os.kill") as mock_kill,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill.assert_not_called()


def test_kill_only_unity_mcp_processes(tmp_path):
    """If lock holder is NOT a unity_mcp process, do not kill it."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    call_count = {"n": 0}
    def flock_side_effect(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError
        # Second call succeeds

    with (
        patch("fcntl.flock", side_effect=flock_side_effect),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=False),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("os.kill") as mock_kill,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill.assert_not_called()


def test_raises_runtime_error_when_zombie_lock_never_released(tmp_path):
    """If a zombie lock can't be acquired after waiting, raise RuntimeError."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    def flock_always_blocked(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            raise BlockingIOError

    with (
        patch("fcntl.flock", side_effect=flock_always_blocked),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=False),  # dead PID
        patch("os.kill"),
        patch("time.sleep"),
    ):
        with pytest.raises(RuntimeError, match="Cannot acquire exclusive lock"):
            acquire_lock(lock_dir=tmp_path, port=9500)


# ---------------------------------------------------------------------------
# Takeover: send SIGTERM when live process holds the lock
# ---------------------------------------------------------------------------

def test_takeover_sends_sigterm_and_acquires(tmp_path):
    """Happy path: old live unity_mcp process blocks lock → SIGTERM sent → lock acquired."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    call_count = {"n": 0}
    def flock_side_effect(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError

    with (
        patch("fcntl.flock", side_effect=flock_side_effect),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("unity_mcp.lockfile._kill_pid") as mock_kill_pid,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill_pid.assert_called_once_with(54321)


def test_dead_zombie_lock_still_cleaned_up(tmp_path):
    """Zombie lock (dead PID) is cleaned up — lock acquired, no error raised."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    call_count = {"n": 0}
    def flock_side(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError

    with (
        patch("fcntl.flock", side_effect=flock_side),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=False),  # dead PID
        patch("os.kill") as mock_kill,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill.assert_not_called()


def test_is_unity_mcp_pid_returns_true_for_self():
    """Our own process is always a unity_mcp process in tests."""
    from unity_mcp.lockfile import _is_unity_mcp_pid
    # We can't guarantee our own cmdline contains unity_mcp, so mock ps output
    with patch("subprocess.check_output", return_value=b"python -m unity_mcp.server\n"):
        assert _is_unity_mcp_pid(os.getpid()) is True


def test_is_unity_mcp_pid_returns_false_for_other():
    from unity_mcp.lockfile import _is_unity_mcp_pid
    with patch("subprocess.check_output", return_value=b"/usr/bin/vim\n"):
        assert _is_unity_mcp_pid(12345) is False


def test_is_unity_mcp_pid_returns_false_when_ps_fails():
    from unity_mcp.lockfile import _is_unity_mcp_pid
    with patch("subprocess.check_output", side_effect=subprocess.CalledProcessError(1, "ps")):
        assert _is_unity_mcp_pid(12345) is False


# ---------------------------------------------------------------------------
# C10. read_pid_from_port_file
# ---------------------------------------------------------------------------

def test_read_pid_from_port_file(tmp_path):
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    pid = 12345
    port_file = ports_dir / f"{pid}.port"
    port_file.write_text("9500\n/path/to/project\nMyProject")
    with patch.object(Path, "home", return_value=tmp_path):
        result = read_pid_from_port_file(9500)
    assert result == pid


def test_read_pid_returns_none_for_missing():
    assert read_pid_from_port_file(9999) is None


def test_is_pid_alive_for_own_process():
    assert is_pid_alive(os.getpid()) is True


def test_is_pid_alive_for_dead_pid():
    assert is_pid_alive(99999999) is False


def test_is_pid_alive_for_none():
    assert is_pid_alive(None) is False


# ---------------------------------------------------------------------------
# P2 gaps: _read_pid_from_fd, read_pid_from_port_file edge cases, cleanup
# ---------------------------------------------------------------------------

def test_read_pid_from_fd_returns_none_for_corrupt_content(tmp_path):
    """_read_pid_from_fd returns None when file contains non-integer data."""
    from unity_mcp.lockfile import _read_pid_from_fd
    f = tmp_path / "corrupt.lock"
    f.write_bytes(b"not-a-pid\n")
    fd = os.open(str(f), os.O_RDWR)
    try:
        result = _read_pid_from_fd(fd)
        assert result is None
    finally:
        os.close(fd)


def test_read_pid_from_port_file_corrupt_json(tmp_path):
    """read_pid_from_port_file skips files where int(lines[0]) raises ValueError."""
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad_file = ports_dir / "12345.port"
    bad_file.write_text("not-a-port\n/path/to/project")
    with patch.object(Path, "home", return_value=tmp_path):
        result = read_pid_from_port_file(9500)
    assert result is None


def test_read_pid_from_port_file_non_integer_stem(tmp_path):
    """read_pid_from_port_file skips files with non-integer stem (e.g. 'abc.port')."""
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad_file = ports_dir / "abc.port"
    bad_file.write_text("9500\n/path/to/project")
    with patch.object(Path, "home", return_value=tmp_path):
        result = read_pid_from_port_file(9500)
    assert result is None


def test_lockfile_cleanup_on_abnormal_exit(tmp_path):
    """Lock file is cleaned up (unlocked) even when release_lock is called after crash simulation."""
    fd = acquire_lock(lock_dir=tmp_path, port=9500)
    lock_file = tmp_path / "server-9500.lock"
    assert lock_file.exists()
    # Simulate abnormal exit: release_lock still unlocks the fd
    release_lock(fd)
    # After release the lock can be re-acquired (file is free)
    fd2 = acquire_lock(lock_dir=tmp_path, port=9500)
    assert fd2 is not None
    release_lock(fd2)


# ---------------------------------------------------------------------------
# Cross-platform abstraction layer tests (B5)
# ---------------------------------------------------------------------------

def test_lock_nb_and_unlock_importable():
    """_lock_nb and _unlock are importable and callable (abstraction boundary)."""
    assert callable(_lock_nb)
    assert callable(_unlock)


def test_is_pid_alive_permission_error_returns_true():
    """PermissionError from os.kill means process exists but we can't signal it — return True."""
    with patch("os.kill", side_effect=PermissionError("operation not permitted")):
        assert is_pid_alive(12345) is True


def test_is_unity_mcp_pid_powershell_match(monkeypatch):
    """On Windows (mocked), PowerShell CIM returns cmdline with unity_mcp → True."""
    import unity_mcp.lockfile as lm
    monkeypatch.setattr(lm, "_IS_WIN", True)
    cmdline = b"python -m unity_mcp.server\r\n"
    with patch("subprocess.check_output", return_value=cmdline):
        result = lm._is_unity_mcp_pid(12345)
    assert result is True


def test_is_unity_mcp_pid_powershell_no_match(monkeypatch):
    """On Windows (mocked), PowerShell CIM returns unrelated cmdline → False."""
    import unity_mcp.lockfile as lm
    monkeypatch.setattr(lm, "_IS_WIN", True)
    with patch("subprocess.check_output", return_value=b"C:\\Windows\\system32\\notepad.exe\r\n"):
        result = lm._is_unity_mcp_pid(12345)
    assert result is False


def test_is_unity_mcp_pid_powershell_fails_tasklist_fallback_match(monkeypatch):
    """When PowerShell fails, falls back to tasklist; 'python' in output → True (weak check)."""
    import unity_mcp.lockfile as lm
    monkeypatch.setattr(lm, "_IS_WIN", True)
    csv_output = b'"python.exe","12345","Console","1","12,345 K"\r\n'
    call_count = {"n": 0}
    def check_output_side_effect(cmd, **kwargs):
        call_count["n"] += 1
        if call_count["n"] == 1:
            raise FileNotFoundError("powershell not found")
        return csv_output
    with patch("subprocess.check_output", side_effect=check_output_side_effect):
        result = lm._is_unity_mcp_pid(12345)
    assert result is True


def test_is_unity_mcp_pid_powershell_fails_tasklist_fallback_no_match(monkeypatch):
    """When PowerShell fails, falls back to tasklist; no 'python' → False."""
    import unity_mcp.lockfile as lm
    monkeypatch.setattr(lm, "_IS_WIN", True)
    call_count = {"n": 0}
    def check_output_side_effect(cmd, **kwargs):
        call_count["n"] += 1
        if call_count["n"] == 1:
            raise FileNotFoundError("powershell not found")
        return b"INFO: No tasks running.\r\n"
    with patch("subprocess.check_output", side_effect=check_output_side_effect):
        result = lm._is_unity_mcp_pid(12345)
    assert result is False


def test_is_unity_mcp_pid_all_fallbacks_fail(monkeypatch):
    """When both PowerShell and tasklist raise OSError → return False (fail-safe)."""
    import unity_mcp.lockfile as lm
    monkeypatch.setattr(lm, "_IS_WIN", True)
    with patch("subprocess.check_output", side_effect=OSError("all gone")):
        result = lm._is_unity_mcp_pid(12345)
    assert result is False


# ---------------------------------------------------------------------------
# A1: _is_zombie tests
# ---------------------------------------------------------------------------

def test_is_zombie_returns_true_for_zombie_process():
    from unity_mcp.lockfile import _is_zombie
    with patch("subprocess.check_output", return_value=b"Z\n"):
        assert _is_zombie(12345) is True


def test_is_zombie_returns_false_for_running_process():
    from unity_mcp.lockfile import _is_zombie
    with patch("subprocess.check_output", return_value=b"S\n"):
        assert _is_zombie(12345) is False


def test_is_zombie_returns_false_when_ps_fails():
    from unity_mcp.lockfile import _is_zombie
    with patch("subprocess.check_output", side_effect=subprocess.CalledProcessError(1, "ps")):
        assert _is_zombie(12345) is False


def test_zombie_pid_does_not_raise_runtime_error(tmp_path):
    """Zombie PID (alive but zombie state) must NOT raise — fall through to wait loop."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    call_count = {"n": 0}
    def flock_side_effect(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            call_count["n"] += 1
            if call_count["n"] == 1:
                raise BlockingIOError
        # Second call succeeds

    with (
        patch("fcntl.flock", side_effect=flock_side_effect),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("unity_mcp.lockfile._is_zombie", return_value=True),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None


# ---------------------------------------------------------------------------
# _kill_pid tests
# ---------------------------------------------------------------------------

def test_takeover_old_process_doesnt_die(tmp_path):
    """SIGTERM sent but lock never releases — raises generic RuntimeError."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    def flock_always_blocked(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            raise BlockingIOError

    with (
        patch("fcntl.flock", side_effect=flock_always_blocked),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("unity_mcp.lockfile._kill_pid") as mock_kill_pid,
        patch("time.sleep"),
    ):
        with pytest.raises(RuntimeError, match="Cannot acquire exclusive lock"):
            acquire_lock(lock_dir=tmp_path, port=9500)
        assert mock_kill_pid.call_count == 1


def test_kill_pid_dead_process():
    """_kill_pid with dead PID (ProcessLookupError) does not raise."""
    from unity_mcp.lockfile import _kill_pid
    with patch("os.kill", side_effect=ProcessLookupError):
        _kill_pid(99999)  # must not raise


def test_kill_pid_permission_error():
    """_kill_pid with PermissionError does not raise."""
    from unity_mcp.lockfile import _kill_pid
    with patch("os.kill", side_effect=PermissionError):
        _kill_pid(99999)  # must not raise
