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


def _make_port_file_with_path(ports_dir, pid: int, port: int, project_path: str,
                               project: str = "MyProject", mtime: float = 1000.0):
    """Write a <pid>.port file with a real project path on line 1 (C# format)."""
    f = ports_dir / f"{pid}.port"
    f.write_text(f"{port}\n{project_path}\n{project}\n")
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


def test_env_override_non_numeric_falls_through(monkeypatch, tmp_path):
    """Non-numeric UNITY_MCP_PORT doesn't crash — falls through to default."""
    monkeypatch.setenv("UNITY_MCP_PORT", "notaport")
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    assert _read_unity_port() == 9500


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


def test_permission_error_keeps_file_as_live_candidate(monkeypatch, tmp_path):
    """PermissionError from os.kill means process is alive (different user) — keep file."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    f = _make_port_file(ports_dir, pid=9998, port=9502)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: (_ for _ in ()).throw(PermissionError()))
    result = _read_unity_port()
    assert result == 9502  # process alive — port is valid candidate
    assert f.exists()      # file NOT deleted


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


# ---------------------------------------------------------------------------
# CWD-based port discovery
# ---------------------------------------------------------------------------

def test_cwd_exact_match_project_root(monkeypatch, tmp_path):
    """CWD is exactly the project root (not a subdir) — still matches."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    monkeypatch.setattr("os.getcwd", lambda: project_a)
    assert _read_unity_port() == 9500


def test_cwd_match_returns_matching_project_port(monkeypatch, tmp_path):
    """CWD inside project dir → returns that port even if another has newer mtime."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    # Project B has newer mtime — would win without CWD matching
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    # CWD is inside project A
    monkeypatch.setattr("os.getcwd", lambda: project_a + "/Assets/Scripts")
    assert _read_unity_port() == 9500


def test_cwd_no_match_falls_back_to_mtime(monkeypatch, tmp_path):
    """CWD doesn't match any project → falls back to newest mtime."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_a = str(tmp_path / "ProjectA")
    project_b = str(tmp_path / "ProjectB")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_a, mtime=500.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_b, mtime=2000.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    # CWD is somewhere unrelated
    monkeypatch.setattr("os.getcwd", lambda: str(tmp_path / "OtherDir"))
    assert _read_unity_port() == 9501  # newest mtime wins


def test_cwd_nested_projects_prefers_longest_match(monkeypatch, tmp_path):
    """Two overlapping project paths → prefer the longer (more specific) one."""
    monkeypatch.delenv("UNITY_MCP_PORT", raising=False)
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    project_root = str(tmp_path / "Workspace")
    project_sub  = str(tmp_path / "Workspace" / "SubProject")
    _make_port_file_with_path(ports_dir, pid=100, port=9500, project_path=project_root, mtime=2000.0)
    _make_port_file_with_path(ports_dir, pid=200, port=9501, project_path=project_sub,  mtime=500.0)
    monkeypatch.setattr("pathlib.Path.home", lambda: tmp_path)
    monkeypatch.setattr("os.kill", lambda pid, sig: None)
    # CWD is inside the sub-project (both match, longest wins)
    monkeypatch.setattr("os.getcwd", lambda: project_sub + "/Assets")
    assert _read_unity_port() == 9501
