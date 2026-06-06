"""TDD tests for lockfile.py — exclusive lock with kill-old behavior."""
import fcntl
import os
import subprocess
import time
from pathlib import Path
from unittest.mock import patch, call, MagicMock

import pytest

from unity_mcp.lockfile import acquire_lock, release_lock, read_pid_from_port_file, is_pid_alive


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

def test_kill_and_retake_kills_unity_mcp_process(tmp_path):
    """When lock is held by another unity_mcp process, it gets SIGTERM'd and we retake the lock."""
    lock_file = tmp_path / "server-9500.lock"

    fake_pid = 54321
    # Pre-write PID in lock file so acquire can read it
    lock_file.write_text(f"{fake_pid}\n")

    def flock_side_effect(fd, op):
        # First call (LOCK_EX|NB) raises BlockingIOError, second succeeds
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            if not hasattr(flock_side_effect, "_called"):
                flock_side_effect._called = True
                raise BlockingIOError
        # All other calls succeed (LOCK_EX retry after kill)

    with (
        patch("fcntl.flock", side_effect=flock_side_effect) as mock_flock,
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", side_effect=[True, False]),
        patch("os.kill") as mock_kill,
        patch("time.sleep"),
    ):
        fd = acquire_lock(lock_dir=tmp_path, port=9500)
        assert fd is not None
        mock_kill.assert_called_once_with(fake_pid, 15)  # SIGTERM


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


def test_raises_runtime_error_when_lock_never_released(tmp_path):
    """If lock is never released after kill, raise RuntimeError."""
    lock_file = tmp_path / "server-9500.lock"
    lock_file.write_text("54321\n")

    def flock_always_blocked(fd, op):
        if op == (fcntl.LOCK_EX | fcntl.LOCK_NB):
            raise BlockingIOError

    with (
        patch("fcntl.flock", side_effect=flock_always_blocked),
        patch("unity_mcp.lockfile._is_unity_mcp_pid", return_value=True),
        patch("unity_mcp.lockfile.is_pid_alive", return_value=True),
        patch("os.kill"),
        patch("time.sleep"),
    ):
        with pytest.raises(RuntimeError, match="Cannot acquire exclusive lock"):
            acquire_lock(lock_dir=tmp_path, port=9500)


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
