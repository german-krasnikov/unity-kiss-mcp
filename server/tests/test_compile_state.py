"""Tests for CompileStateProbe — Phase 1 (14 tests)."""
import os
import time
from pathlib import Path
from unittest.mock import patch

import pytest

from unity_mcp.compile_state import CompileStateProbe


# ---------------------------------------------------------------------------
# 1. Degrade-open: no project path
# ---------------------------------------------------------------------------

def test_probe_no_project_path_degrades_open():
    probe = CompileStateProbe(None)
    assert probe.is_unity_busy() is False  # no crash, returns False


# ---------------------------------------------------------------------------
# 2-4. FS-based heuristics
# ---------------------------------------------------------------------------

def test_probe_detects_recent_dll(tmp_path):
    # DLL mtime no longer used for is_unity_busy (removed in Cycle 15).
    # Probe relies on lock file and disconnect window only.
    (tmp_path / "Library" / "ScriptAssemblies").mkdir(parents=True)
    dll = tmp_path / "Library" / "ScriptAssemblies" / "Foo.dll"
    dll.touch()
    probe = CompileStateProbe(tmp_path)
    # No lock file, no recent disconnect → not busy
    assert probe.is_unity_busy() is False


def test_probe_idle_old_dll(tmp_path):
    asm_dir = tmp_path / "Library" / "ScriptAssemblies"
    asm_dir.mkdir(parents=True)
    dll = asm_dir / "Foo.dll"
    dll.touch()
    # backdate mtime by 30s
    old_ts = time.time() - 30
    os.utime(dll, (old_ts, old_ts))

    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is False


def test_probe_lock_file_present(tmp_path):
    (tmp_path / "Library" / "BeeDriver").mkdir(parents=True)
    (tmp_path / "Library" / "BeeDriver" / "Lock").touch()
    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is True


# ---------------------------------------------------------------------------
# 5-6. Disconnect-window heuristic
# ---------------------------------------------------------------------------

def test_probe_recent_disconnect_marks_busy(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.mark_recompile_issued()
    assert probe.is_unity_busy() is True


def test_probe_disconnect_window_expires(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    base_ts = 1000.0
    with patch("unity_mcp.compile_state.time") as mock_time:
        mock_time.monotonic.return_value = base_ts
        probe.mark_recompile_issued()
        # Advance 31 seconds past mark
        mock_time.monotonic.return_value = base_ts + 31
        assert probe.is_unity_busy() is False


# ---------------------------------------------------------------------------
# 7-9. estimated_remaining_s
# ---------------------------------------------------------------------------

def test_probe_estimated_remaining_default(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    assert probe.estimated_remaining_s() == 5.0


def test_probe_estimated_remaining_capped_at_60(tmp_path):
    # estimated_remaining_s caps at 60 when _last_known_duration is very large
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe._last_known_duration = 200.0
    probe._compile_elapsed = 0.0
    result = probe.estimated_remaining_s()
    assert result == 60.0


def test_probe_estimated_shorter_for_older_dll(tmp_path):
    # estimated_remaining_s returns less than default when elapsed is known
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe._last_known_duration = 10.0
    probe._compile_elapsed = 8.0  # 8s elapsed of a 10s compile → 2s left
    assert probe.estimated_remaining_s() < 5.0


# ---------------------------------------------------------------------------
# 10-11. Error resilience
# ---------------------------------------------------------------------------

def test_probe_handles_missing_library_dir(tmp_path):
    # path exists but no Library/ subdir
    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is False  # no crash


def test_probe_handles_permission_error(tmp_path):
    (tmp_path / "Library" / "ScriptAssemblies").mkdir(parents=True)

    def boom(*args, **kwargs):
        raise PermissionError("no access")

    probe = CompileStateProbe(tmp_path)
    with patch("pathlib.Path.iterdir", side_effect=boom):
        assert probe.is_unity_busy() is False


# ---------------------------------------------------------------------------
# 12-13. autodetect_project_path
# ---------------------------------------------------------------------------

def test_autodetect_reads_env(tmp_path, monkeypatch):
    (tmp_path / "Library").mkdir()
    monkeypatch.setenv("UNITY_MCP_PROJECT_PATH", str(tmp_path))
    result = CompileStateProbe.autodetect_project_path()
    assert result == tmp_path


def test_autodetect_returns_none_when_invalid(monkeypatch):
    # No env var → returns None (subprocess detection removed in Cycle 15)
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    assert CompileStateProbe.autodetect_project_path() is None


def test_autodetect_falls_back_to_process_detection(tmp_path, monkeypatch):
    # _detect_from_unity_process removed; autodetect only uses env var
    (tmp_path / "Library").mkdir()
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    assert CompileStateProbe.autodetect_project_path() is None


def test_autodetect_env_takes_priority(tmp_path, monkeypatch):
    (tmp_path / "Library").mkdir()
    monkeypatch.setenv("UNITY_MCP_PROJECT_PATH", str(tmp_path))
    result = CompileStateProbe.autodetect_project_path()
    assert result == tmp_path


def test_has_project_property(tmp_path):
    assert CompileStateProbe(tmp_path).has_project is True
    assert CompileStateProbe(None).has_project is False


# ---------------------------------------------------------------------------
# 14. Injected path doesn't use Path.home
# ---------------------------------------------------------------------------

def test_probe_no_real_home_access(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)

    with patch.object(Path, "home", side_effect=RuntimeError("home accessed!")):
        # Should never call Path.home — injected path used directly
        assert probe.is_unity_busy() is False  # no crash, no RuntimeError


# ---------------------------------------------------------------------------
# 15-19. update_compile_info + estimated_remaining_s with last_known_duration
# ---------------------------------------------------------------------------

def test_update_compile_info_compiling(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("compiling|12.3")
    assert probe._compile_elapsed == pytest.approx(12.3)


def test_update_compile_info_idle(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("idle|8.2")
    assert probe._last_known_duration == pytest.approx(8.2)
    assert probe._compile_elapsed is None


def test_estimated_remaining_uses_last_duration(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("idle|45.0")   # last compile took 45s
    probe.update_compile_info("compiling|10.0")  # 10s elapsed
    result = probe.estimated_remaining_s()
    assert result == pytest.approx(35.0, abs=1.0)


def test_update_compile_info_invalid(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("garbage")
    probe.update_compile_info("")
    probe.update_compile_info("bad|notanumber")
    # No crash, state unchanged
    assert probe._compile_elapsed is None
    assert probe._last_known_duration is None


def test_max_remaining_raised_to_60(tmp_path):
    from unity_mcp.compile_state import _MAX_REMAINING_S
    assert _MAX_REMAINING_S == 60


# ---------------------------------------------------------------------------
# 20-22. has_strong_busy_signal
# ---------------------------------------------------------------------------

def test_has_strong_busy_signal_lock_file(tmp_path):
    (tmp_path / "Library" / "BeeDriver").mkdir(parents=True)
    (tmp_path / "Library" / "BeeDriver" / "Lock").touch()
    probe = CompileStateProbe(tmp_path)
    assert probe.has_strong_busy_signal() is True


def test_has_strong_busy_signal_fresh_dll(tmp_path):
    # DLL mtime no longer used for has_strong_busy_signal (Cycle 15).
    # Without lock file, returns False.
    (tmp_path / "Library" / "ScriptAssemblies").mkdir(parents=True)
    (tmp_path / "Library" / "ScriptAssemblies" / "Test.dll").touch()
    probe = CompileStateProbe(tmp_path)
    assert probe.has_strong_busy_signal() is False


def test_has_strong_busy_signal_idle(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    assert probe.has_strong_busy_signal() is False


# ---------------------------------------------------------------------------
# is_process_dead (used by heartbeat loop + _describe_failure)
# ---------------------------------------------------------------------------

def test_is_process_dead_returns_false_when_no_port():
    probe = CompileStateProbe(port=None)
    assert probe.is_process_dead() is False


def test_is_process_dead_returns_true_when_pid_dead():
    with patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=999999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=False):
        probe = CompileStateProbe(port=9500)
        assert probe.is_process_dead() is True


