"""Tests for install.py new subcommands: configure (tool mode), uninstall."""
import argparse
import importlib.util
import json
import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

# Load install.py directly (avoids conflict with install/ package)
REPO_ROOT = Path(__file__).parent.parent.parent
_spec = importlib.util.spec_from_file_location("install_script", REPO_ROOT / "install.py")
inst = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(inst)

# Lazy references — functions don't exist until Green phase
cmd_configure = lambda *a, **kw: inst.cmd_configure(*a, **kw)
cmd_uninstall = lambda *a, **kw: inst.cmd_uninstall(*a, **kw)

MOD = "install_script"  # module name for patch()


# ── helpers ──────────────────────────────────────────────────────────────────

def _args(**kwargs) -> argparse.Namespace:
    defaults = {"project": None, "tool": None, "port": 0, "force": False}
    defaults.update(kwargs)
    return argparse.Namespace(**defaults)


def _fake_registry(config_path: Path) -> dict:
    client = MagicMock()
    client.name = "Fake Tool"
    client.config_path = config_path
    client.stdout_only = False
    return {"fake-tool": client}


# ── configure: tool mode ─────────────────────────────────────────────────────

def test_configure_creates_config(tmp_path):
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": ["run", "unity-mcp"]}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-mcp"] == entry


def test_configure_preserves_other_servers(tmp_path):
    cfg = tmp_path / "mcp.json"
    cfg.write_text(json.dumps({"mcpServers": {"other-tool": {"command": "x"}}}), encoding="utf-8")
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "other-tool" in data["mcpServers"]
    assert "unity-mcp" in data["mcpServers"]


def test_configure_with_tool_flag_only_configures_that_tool(tmp_path):
    cfg_target = tmp_path / "only.json"
    cfg_other = tmp_path / "other.json"
    registry = {
        "fake-tool": _fake_registry(cfg_target)["fake-tool"],
        "other": _fake_registry(cfg_other)["fake-tool"],
    }
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    assert cfg_target.exists()
    assert not cfg_other.exists()


def test_configure_port_passed_to_entry(tmp_path):
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    captured = {}

    def fake_entry(port=0):
        captured["port"] = port
        return {"command": "x", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", side_effect=fake_entry):
        cmd_configure(_args(tool="fake-tool", port=9999))

    assert captured["port"] == 9999


def test_configure_auto_detect_prompts(tmp_path):
    """No --tool → auto-detect installed tools and prompt for each."""
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "prompt_yn", return_value=True), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool=None))

    assert cfg.exists()


def test_configure_auto_detect_user_skips(tmp_path):
    """No --tool, user answers 'n' → nothing configured."""
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "prompt_yn", return_value=False), \
         patch.object(inst, "build_server_entry", return_value={"command": "x", "args": []}):
        cmd_configure(_args(tool=None))

    assert not cfg.exists()


# ── uninstall ─────────────────────────────────────────────────────────────────

def test_uninstall_removes_venv(tmp_path):
    venv = tmp_path / "server" / ".venv"
    venv.mkdir(parents=True)
    (venv / "bin").mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "prompt_yn", return_value=False):
        cmd_uninstall(_args())

    assert not venv.exists()


def test_uninstall_removes_unity_mcp_dir(tmp_path):
    data_dir = tmp_path / ".unity-mcp"
    data_dir.mkdir()
    (data_dir / "ports").mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "_UNITY_MCP_DATA_DIR", data_dir), \
         patch.object(inst, "prompt_yn", return_value=True):
        cmd_uninstall(_args())

    assert not data_dir.exists()


def test_uninstall_skips_data_dir_if_user_declines(tmp_path):
    data_dir = tmp_path / ".unity-mcp"
    data_dir.mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "_UNITY_MCP_DATA_DIR", data_dir), \
         patch.object(inst, "prompt_yn", return_value=False):
        cmd_uninstall(_args())

    assert data_dir.exists()


# ── setup uses ui ─────────────────────────────────────────────────────────────

def test_setup_calls_ui_ok(capsys):
    with patch.object(inst, "_setup_env", lambda *a, **kw: None):
        inst.cmd_setup(_args())
    out = capsys.readouterr().out
    # ui.ok outputs ✓ or [OK]
    assert "✓" in out or "[OK]" in out or "OK" in out
