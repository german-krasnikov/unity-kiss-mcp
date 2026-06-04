"""Tests for _read_unity_port — filesystem/env/PID mocking only, no global state leaks."""
import os
import pytest
from unity_mcp.server import _read_unity_port


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_port_file(ports_dir, pid: int, port: int, project: str = "MyProject", mtime: float = 1000.0):
    """Write a <pid>.port file and set its mtime."""
    f = ports_dir / f"{pid}.port"
    f.write_text(f"{port}\n\n{project}\n")
    os.utime(f, (mtime, mtime))
    return f


# ---------------------------------------------------------------------------
# Env override
# ---------------------------------------------------------------------------

def test_env_override_returns_that_port(monkeypatch, tmp_path):
    monkeypatch.setenv("UNITY_MCP_PORT", "9999")
    # Even if ports_dir exists with valid files, env wins
    assert _read_unity_port() == 9999


def test_env_override_parses_int(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_PORT", "10500")
    assert _read_unity_port() == 10500


# ---------------------------------------------------------------------------
# No env, no lockfiles → default 9500
# ---------------------------------------------------------------------------

def test_fallback_default_when_no_dir(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    # Point home to tmp_path so ~/.unity-mcp/ports never exists
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


def test_fallback_default_when_dir_empty(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# Single live lockfile
# ---------------------------------------------------------------------------

def test_single_live_lockfile_returns_port(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=1234, port=9501, project="Game")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    # os.kill(1234, 0) must not raise → process is "live"
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    result = _read_unity_port()
    assert result == 9501


# ---------------------------------------------------------------------------
# mtime sort: newest lockfile wins
# ---------------------------------------------------------------------------

def test_newest_lockfile_wins(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=100, port=9600, mtime=500.0)   # older
    _make_port_file(ports_dir, pid=200, port=9700, mtime=1500.0)  # newer
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    assert _read_unity_port() == 9700


def test_oldest_lockfile_loses(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    _make_port_file(ports_dir, pid=100, port=9600, mtime=2000.0)  # newer
    _make_port_file(ports_dir, pid=200, port=9700, mtime=100.0)   # older
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    assert _read_unity_port() == 9600


# ---------------------------------------------------------------------------
# Stale PID — ProcessLookupError → file removed, skipped
# ---------------------------------------------------------------------------

def test_dead_pid_process_lookup_error_skips_and_removes(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9999, port=9501)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: (_ for _ in ()).throw(ProcessLookupError()))
    result = _read_unity_port()
    assert result == 9500        # fell through to default
    assert not f.exists()        # file was cleaned up


def test_dead_pid_permission_error_skips_and_removes(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9998, port=9502)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: (_ for _ in ()).throw(PermissionError()))
    result = _read_unity_port()
    assert result == 9500
    assert not f.exists()


def test_dead_pid_os_error_skips_and_removes(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9997, port=9503)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: (_ for _ in ()).throw(OSError()))
    result = _read_unity_port()
    assert result == 9500
    assert not f.exists()


# ---------------------------------------------------------------------------
# Mixed: one dead, one live → live one wins
# ---------------------------------------------------------------------------

def test_mixed_dead_and_live_returns_live_port(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    dead_file = _make_port_file(ports_dir, pid=7777, port=9800, mtime=2000.0)
    _make_port_file(ports_dir, pid=8888, port=9801, mtime=1000.0)

    def fake_kill(pid, sig):
        if pid == 7777:
            raise ProcessLookupError()

    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", fake_kill)
    result = _read_unity_port()
    assert result == 9801        # only live candidate
    assert not dead_file.exists()


# ---------------------------------------------------------------------------
# Malformed file: ValueError on int parse → skipped/removed
# ---------------------------------------------------------------------------

def test_malformed_port_file_skipped(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad = ports_dir / "notanint.port"
    bad.write_text("broken\n")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    # int(lines[0]) where lines[0]="broken" → ValueError → skip
    assert _read_unity_port() == 9500


def test_malformed_port_content_skipped(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    bad = ports_dir / "1234.port"
    bad.write_text("notaport\n")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    # int("notaport") → ValueError → skip
    assert _read_unity_port() == 9500


# ---------------------------------------------------------------------------
# project field: missing third line falls back to "?"
# (doesn't affect port selection, just verify no crash)
# ---------------------------------------------------------------------------

def test_lockfile_without_project_line(monkeypatch, tmp_path):
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = ports_dir / "5555.port"
    f.write_text("9505\n")  # only one line — no project
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    assert _read_unity_port() == 9505
