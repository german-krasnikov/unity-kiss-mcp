"""Tests for backend_def.py — all 5 CLI backends + resolve_binary. No real processes."""
import json
import os
import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

from unity_mcp.backend_def import (
    BACKENDS,
    AgyDef,
    ClaudeDef,
    CodexDef,
    KimiDef,
    OpenCodeDef,
    _which_via_login_shell,
    _sanitize_extra_args,
    MCP_BLANKET,
    MCP_PERMISSION_TOOL,
)


# ─── resolve_binary (3 tests) ───────────────────────────────────────────────

async def test_resolve_binary_shutil_found(monkeypatch):
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: "/usr/bin/claude")
    assert await ClaudeDef().resolve_binary() == "/usr/bin/claude"


async def test_resolve_binary_shutil_miss_zsh_ok(monkeypatch):
    from unittest.mock import AsyncMock
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: None)
    monkeypatch.setattr(
        "unity_mcp.backend_def._which_via_login_shell",
        AsyncMock(return_value="/opt/homebrew/bin/claude"),
    )
    assert await ClaudeDef().resolve_binary() == "/opt/homebrew/bin/claude"


async def test_resolve_binary_both_miss(monkeypatch):
    from unittest.mock import AsyncMock
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: None)
    monkeypatch.setattr("unity_mcp.backend_def._which_via_login_shell", AsyncMock(return_value=None))
    assert await ClaudeDef().resolve_binary() is None


# ─── Claude (7 tests) ───────────────────────────────────────────────────────

def test_claude_build_args_ask_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9600,
                                        config_dir=str(tmp_path))
    assert "--permission-mode" in argv
    assert argv[argv.index("--permission-mode") + 1] == "plan"


def test_claude_build_args_agent_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="agent", model=None, mcp_port=9600,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "acceptEdits"


def test_claude_resume_includes_session_id(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9600,
                                        session_id="abc123",
                                        config_dir=str(tmp_path))
    assert "--resume" in argv
    assert argv[argv.index("--resume") + 1] == "abc123"


def test_claude_env_strip_keys(tmp_path):
    _, _, strip = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9600,
                                         config_dir=str(tmp_path))
    assert "ANTHROPIC_API_KEY" in strip
    assert "CLAUDECODE"        in strip
    assert "UNITY_MCP_PORT"   in strip
    assert "UNITY_MCP_CHAT"   in strip


def test_claude_agent_name_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9600,
                                        agent_name="my-agent",
                                        config_dir=str(tmp_path))
    assert "--agent" in argv
    assert argv[argv.index("--agent") + 1] == "my-agent"


def test_claude_model_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model="claude-opus-4",
                                        mcp_port=9600, config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "claude-opus-4"


def test_claude_no_resume_when_none(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9600,
                                        session_id=None, config_dir=str(tmp_path))
    assert "--resume" not in argv


# ─── Codex (5 tests) ────────────────────────────────────────────────────────

def test_codex_first_turn_argv_structure():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=9600,
                                       prompt="hello")
    assert argv[0] == "exec"
    assert "--json" in argv
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "danger-full-access"


def test_codex_resume_argv_structure():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=9600,
                                       prompt="hello", session_id="sess-42")
    assert argv[:2] == ["exec", "resume"]
    assert argv[2] == "sess-42"
    assert "--dangerously-bypass-approvals-and-sandbox" in argv


def test_codex_prompt_is_last_arg():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=9600,
                                       prompt="do the thing")
    assert argv[-1] == "do the thing"


def test_codex_toml_mcp_flags_present():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=9600,
                                       prompt="x")
    # Collect all -c values
    c_values = [argv[i + 1] for i, v in enumerate(argv) if v == "-c"]
    assert any("mcp_servers.unity.command=" in v for v in c_values)
    assert any("mcp_servers.unity.args=" in v for v in c_values)
    assert any("mcp_servers.unity.startup_timeout_sec=30" in v for v in c_values)


def test_codex_env_strip():
    _, _, strip = CodexDef().build_args(mode="agent", model=None, mcp_port=9600,
                                        prompt="x")
    assert "OPENAI_API_KEY" in strip


# ─── Kimi (5 tests) ─────────────────────────────────────────────────────────

def test_kimi_argv_structure(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=9600,
                                      prompt="hi", config_dir=str(tmp_path))
    assert argv[0] == "-p"
    assert argv[1] == "hi"
    assert "--output-format" in argv
    assert argv[argv.index("--output-format") + 1] == "stream-json"


def test_kimi_model_flag(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model="kimi-k2", mcp_port=9600,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "kimi-k2"


def test_kimi_no_env_strip(tmp_path):
    _, _, strip = KimiDef().build_args(mode="ask", model=None, mcp_port=9600,
                                       prompt="x", config_dir=str(tmp_path))
    assert strip == []


def test_kimi_writes_mcp_config(tmp_path):
    KimiDef().build_args(mode="ask", model=None, mcp_port=9601,
                         prompt="x", config_dir=str(tmp_path))
    mcp_path = tmp_path / "mcp.json"
    assert mcp_path.exists()
    data = json.loads(mcp_path.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-mcp"]["env"]["UNITY_MCP_PORT"] == "9601"


def test_kimi_no_resume():
    assert KimiDef().has_resume is False


# ─── Agy (4 tests) ──────────────────────────────────────────────────────────

def test_agy_ask_no_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=9600,
                                     prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" not in argv


def test_agy_agent_adds_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="agent", model=None, mcp_port=9600,
                                     prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_agy_env_strip(tmp_path):
    _, _, strip = AgyDef().build_args(mode="ask", model=None, mcp_port=9600,
                                      prompt="x", config_dir=str(tmp_path))
    assert "GEMINI_API_KEY" in strip


def test_agy_writes_settings_json(tmp_path):
    AgyDef().build_args(mode="ask", model=None, mcp_port=9602,
                        prompt="x", config_dir=str(tmp_path))
    settings_path = tmp_path / "settings.json"
    assert settings_path.exists()
    data = json.loads(settings_path.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-mcp"]["env"]["UNITY_MCP_PORT"] == "9602"


# ─── OpenCode (4 tests) ─────────────────────────────────────────────────────

def test_opencode_argv_structure(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=9600,
                                          prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "run"
    assert "--format" in argv
    assert argv[argv.index("--format") + 1] == "json"
    assert "--dangerously-skip-permissions" in argv


def test_opencode_resume_flag(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=9600,
                                          prompt="x", session_id="oc-99",
                                          config_dir=str(tmp_path))
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "oc-99"


def test_opencode_env_set_opencode_config(tmp_path):
    _, env_set, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=9600,
                                              prompt="x", config_dir=str(tmp_path))
    assert "OPENCODE_CONFIG" in env_set
    assert "opencode-unity-mcp-9600.json" in env_set["OPENCODE_CONFIG"]


def test_opencode_writes_config_file(tmp_path):
    OpenCodeDef().build_args(mode="agent", model=None, mcp_port=9603,
                             prompt="x", config_dir=str(tmp_path))
    config_path = tmp_path / "opencode-unity-mcp-9603.json"
    assert config_path.exists()
    data = json.loads(config_path.read_text(encoding="utf-8"))
    assert data["mcp"]["unity-mcp"]["environment"]["UNITY_MCP_PORT"] == "9603"


# ─── M3: _sanitize_extra_args (7 tests) ─────────────────────────────────────

def test_sanitize_passes_safe_flag():
    assert _sanitize_extra_args("--verbose") == ["--verbose"]


def test_sanitize_blocks_permission_mode_and_value():
    result = _sanitize_extra_args("--permission-mode acceptEdits --verbose")
    assert "--permission-mode" not in result
    assert "acceptEdits" not in result
    assert "--verbose" in result


def test_sanitize_blocks_output_format_and_value():
    result = _sanitize_extra_args("--output-format text")
    assert result == []


def test_sanitize_blocks_mcp_config_and_value():
    result = _sanitize_extra_args("--mcp-config /evil/path --model sonnet")
    assert "--mcp-config" not in result
    assert "/evil/path" not in result
    assert "--model" in result
    assert "sonnet" in result


def test_sanitize_empty_string_returns_empty():
    assert _sanitize_extra_args("") == []
    assert _sanitize_extra_args(None) == []


def test_claude_extra_args_cannot_inject_permission_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=9500,
        config_dir=str(tmp_path),
        extra_args="--permission-mode acceptEdits",
    )
    idx = argv.index("--permission-mode")
    assert argv[idx + 1] == "plan"
    assert argv.count("--permission-mode") == 1


def test_claude_extra_args_cannot_inject_output_format(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=9500,
        config_dir=str(tmp_path),
        extra_args="--output-format text",
    )
    idx = argv.index("--output-format")
    assert argv[idx + 1] == "stream-json"
    assert argv.count("--output-format") == 1
