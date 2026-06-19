"""Tests for config auto-generation module (clients, merger, backup, validator, resolver)."""
import json
import pathlib
import sys
from unittest.mock import patch

import pytest


# ─── clients.py ─────────────────────────────────────────────────────────────

def test_detect_finds_claude_desktop_when_dir_exists(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c
    cfg = tmp_path / "claude_desktop_config.json"
    cfg.touch()
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    found = c.detect_installed()
    assert "claude-desktop" in found


def test_detect_returns_empty_when_nothing_installed(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c
    # Point to a nested path whose parent also doesn't exist
    missing = tmp_path / "no_such_dir" / "nonexistent.json"
    for info in c.CLIENT_REGISTRY.values():
        monkeypatch.setattr(info, "config_path", missing)
    result = c.detect_installed()
    assert result == []


def test_client_paths_are_platform_specific():
    from unity_mcp.config import clients as c
    info = c.CLIENT_REGISTRY["claude-desktop"]
    if sys.platform == "darwin":
        assert "Library" in str(info.config_path)
    elif sys.platform == "win32":
        assert "AppData" in str(info.config_path) or "APPDATA" in str(info.config_path)
    else:
        assert ".config" in str(info.config_path)


def test_all_expected_clients_registered():
    from unity_mcp.config import clients as c
    for key in ("claude-desktop", "claude-code", "cursor", "windsurf"):
        assert key in c.CLIENT_REGISTRY


# ─── merger.py ──────────────────────────────────────────────────────────────

def test_merge_creates_new_config_file(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    entry = {"command": "uvx", "args": ["unity-mcp"]}
    merger.merge_mcp_config(cfg, entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-mcp"] == entry


def test_merge_preserves_other_servers(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    existing = {"mcpServers": {"filesystem": {"command": "fs", "args": []}}}
    cfg.write_text(json.dumps(existing), encoding="utf-8")
    entry = {"command": "uvx", "args": ["unity-mcp"]}
    merger.merge_mcp_config(cfg, entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "filesystem" in data["mcpServers"]
    assert data["mcpServers"]["unity-mcp"] == entry


def test_merge_updates_existing_unity_mcp(tmp_path):
    from unity_mcp.config import merger
    cfg = tmp_path / "config.json"
    old = {"mcpServers": {"unity-mcp": {"command": "python", "args": ["-m", "old"]}}}
    cfg.write_text(json.dumps(old), encoding="utf-8")
    new_entry = {"command": "uvx", "args": ["unity-mcp"]}
    merger.merge_mcp_config(cfg, new_entry)
    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-mcp"] == new_entry
    assert len(data["mcpServers"]) == 1  # not duplicated


# ─── backup.py ──────────────────────────────────────────────────────────────

def test_backup_creates_timestamped_copy(tmp_path):
    from unity_mcp.config import backup
    orig = tmp_path / "config.json"
    orig.write_text('{"key": "value"}', encoding="utf-8")
    bak = backup.backup(orig)
    assert bak is not None
    assert bak.exists()
    assert bak.suffix == ".bak"
    assert orig.exists()  # original preserved
    assert bak.read_text(encoding="utf-8") == orig.read_text(encoding="utf-8")


def test_backup_nonexistent_file_returns_none(tmp_path):
    from unity_mcp.config import backup
    result = backup.backup(tmp_path / "missing.json")
    assert result is None


def test_backup_filename_contains_timestamp(tmp_path):
    from unity_mcp.config import backup
    orig = tmp_path / "config.json"
    orig.write_text("{}", encoding="utf-8")
    bak = backup.backup(orig)
    # should contain a date-like substring e.g. 2026-06-19
    name = bak.name
    assert "config.json" in name
    assert ".bak" in name


# ─── validator.py ────────────────────────────────────────────────────────────

def test_validate_missing_config(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    missing = tmp_path / "nofile.json"
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", missing)
    report = validator.validate_config("claude-desktop")
    assert "not found" in report.lower()


def test_validate_invalid_json(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    bad = tmp_path / "bad.json"
    bad.write_text("not json {{", encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", bad)
    report = validator.validate_config("claude-desktop")
    assert "invalid json" in report.lower()


def test_validate_missing_unity_mcp_entry(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    report = validator.validate_config("claude-desktop")
    assert "not configured" in report.lower()


def test_validate_ok_report(tmp_path, monkeypatch):
    from unity_mcp.config import clients as c, validator
    cfg = tmp_path / "cfg.json"
    cfg.write_text(json.dumps({"mcpServers": {"unity-mcp": {"command": "uvx", "args": ["unity-mcp"]}}}), encoding="utf-8")
    monkeypatch.setattr(c.CLIENT_REGISTRY["claude-desktop"], "config_path", cfg)
    # patch port check to skip network
    with patch("unity_mcp.config.validator._port_reachable", return_value=False):
        report = validator.validate_config("claude-desktop")
    assert "unity-mcp" in report.lower()


# ─── resolver.py ─────────────────────────────────────────────────────────────

def test_find_server_command_prefers_uvx(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/local/bin/uvx" if name == "uvx" else None)
    cmd = resolver.find_server_command()
    assert cmd[0] == "uvx"
    assert "unity-mcp" in cmd


def test_find_server_command_falls_back_to_python(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: None)
    cmd = resolver.find_server_command()
    assert cmd[0] == sys.executable
    assert "-m" in cmd


def test_find_port_from_port_file(tmp_path, monkeypatch):
    from unity_mcp.config import resolver
    ports_dir = tmp_path / ".unity-mcp" / "ports"
    ports_dir.mkdir(parents=True)
    (ports_dir / "test.port").write_text("9501\n", encoding="utf-8")
    monkeypatch.setattr(resolver, "_ports_dir", lambda: ports_dir)
    assert resolver.find_port() == 9501


def test_find_port_default_when_no_files(tmp_path, monkeypatch):
    from unity_mcp.config import resolver
    empty_dir = tmp_path / "ports"
    empty_dir.mkdir()
    monkeypatch.setattr(resolver, "_ports_dir", lambda: empty_dir)
    assert resolver.find_port() == 9500


# ─── build_server_entry ──────────────────────────────────────────────────────

def test_build_server_entry_no_port(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/bin/uvx" if name == "uvx" else None)
    entry = resolver.build_server_entry()
    assert "command" in entry
    assert "args" in entry
    assert "env" not in entry


def test_build_server_entry_with_port(monkeypatch):
    from unity_mcp.config import resolver
    monkeypatch.setattr(resolver, "_which", lambda name: "/usr/bin/uvx" if name == "uvx" else None)
    entry = resolver.build_server_entry(port=9501)
    assert entry["env"]["UNITY_MCP_PORT"] == "9501"
