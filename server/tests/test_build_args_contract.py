"""Contract tests for BackendDef.build_args — assert exact flag presence in argv.

Regression anchor: if any mandatory flag is removed from build_args, the
corresponding test fails immediately before any subprocess runs.
"""
import pytest

from unity_mcp.backend_def import (
    AgyDef, ClaudeDef, CodexDef, KimiDef, OpenCodeDef,
    MCP_BLANKET, MCP_PERMISSION_TOOL, MCP_TOOL_PREFIX,
)


# ── Claude: mandatory streaming flags ────────────────────────────────────────

def test_claude_has_verbose(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert "--verbose" in argv


def test_claude_has_output_format_stream_json(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    idx = argv.index("--output-format")
    assert argv[idx + 1] == "stream-json"


def test_claude_has_input_format_stream_json(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert "--input-format" in argv
    assert argv[argv.index("--input-format") + 1] == "stream-json"


def test_claude_dash_p_is_first(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_claude_has_include_partial_messages(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert "--include-partial-messages" in argv


# ── Claude: permission flags ─────────────────────────────────────────────────

def test_claude_permission_prompt_tool_value(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    idx = argv.index("--permission-prompt-tool")
    assert argv[idx + 1] == MCP_PERMISSION_TOOL


def test_claude_allowed_tools_default(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    idx = argv.index("--allowedTools")
    assert argv[idx + 1] == MCP_BLANKET


def test_claude_allowed_tools_subset_prefixed(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path),
                                        allowed_mcp_tools=["get_hierarchy", "batch"])
    idx = argv.index("--allowedTools")
    assert argv[idx + 1] == f"{MCP_TOOL_PREFIX}get_hierarchy,{MCP_TOOL_PREFIX}batch"


# ── Claude: env_strip ────────────────────────────────────────────────────────

def test_claude_env_strip_all_keys(tmp_path):
    _, _, strip = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                         config_dir=str(tmp_path))
    for key in ("ANTHROPIC_API_KEY", "CLAUDECODE", "UNITY_MCP_PORT", "UNITY_MCP_CHAT"):
        assert key in strip, f"expected {key!r} in env_strip"


def test_claude_no_env_set(tmp_path):
    _, env_set, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                            config_dir=str(tmp_path))
    assert env_set == {}


# ── Claude: mode + resume + model ───────────────────────────────────────────

def test_claude_ask_mode_plan(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "plan"


def test_claude_agent_mode_accept_edits(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="agent", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "acceptEdits"


def test_claude_resume_adds_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        session_id="sess-123", config_dir=str(tmp_path))
    assert "--resume" in argv
    assert argv[argv.index("--resume") + 1] == "sess-123"


def test_claude_model_adds_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model="haiku", mcp_port=9500,
                                        config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "haiku"


def test_claude_mcp_config_path(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        config_dir=str(tmp_path))
    idx = argv.index("--mcp-config")
    assert idx >= 0
    assert argv[idx + 1].endswith(".json")


def test_claude_uses_stream_json_flag():
    assert ClaudeDef().uses_stream_json is True


# ── Codex ────────────────────────────────────────────────────────────────────

def test_codex_exec_first():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="x")
    assert argv[0] == "exec"


def test_codex_json_flag():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="x")
    assert "--json" in argv


def test_codex_skip_git_repo_check():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="x")
    assert "--skip-git-repo-check" in argv


def test_codex_resume():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        prompt="x", session_id="s-42")
    assert "resume" in argv
    assert "s-42" in argv


def test_codex_model():
    argv, _, _ = CodexDef().build_args(mode="ask", model="gpt-4", mcp_port=9500, prompt="x")
    assert argv[argv.index("--model") + 1] == "gpt-4"


def test_codex_mcp_server_config():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="x")
    toml_flags = [a for a in argv if "mcp_servers.unity" in a]
    assert len(toml_flags) >= 3


def test_codex_env_strip():
    _, _, strip = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="x")
    assert "OPENAI_API_KEY" in strip


def test_codex_prompt_appended():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9500, prompt="hello")
    assert argv[-1] == "hello"


def test_codex_uses_stream_json_false():
    assert CodexDef().uses_stream_json is False


# ── Kimi ─────────────────────────────────────────────────────────────────────

def test_kimi_p_first(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=9500,
                                       prompt="hello", config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_kimi_output_format(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=9500,
                                       prompt="hello", config_dir=str(tmp_path))
    assert argv[argv.index("--output-format") + 1] == "stream-json"


def test_kimi_prompt_is_second(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=9500,
                                       prompt="test-prompt", config_dir=str(tmp_path))
    assert argv[1] == "test-prompt"


def test_kimi_model(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model="k1-32k", mcp_port=9500,
                                       prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "k1-32k"


def test_kimi_no_env_strip(tmp_path):
    _, _, strip = KimiDef().build_args(mode="ask", model=None, mcp_port=9500,
                                        prompt="x", config_dir=str(tmp_path))
    assert strip == []


def test_kimi_uses_stream_json_flag():
    assert KimiDef().uses_stream_json is True


# ── Agy ──────────────────────────────────────────────────────────────────────

def test_agy_p_flag(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=9500,
                                      prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_agy_prompt(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=9500,
                                      prompt="test", config_dir=str(tmp_path))
    assert argv[1] == "test"


def test_agy_ask_no_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=9500,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" not in argv


def test_agy_agent_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="agent", model=None, mcp_port=9500,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_agy_model(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model="gemini-2", mcp_port=9500,
                                      prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "gemini-2"


def test_agy_env_strip(tmp_path):
    _, _, strip = AgyDef().build_args(mode="ask", model=None, mcp_port=9500,
                                       prompt="x", config_dir=str(tmp_path))
    assert "GEMINI_API_KEY" in strip


def test_agy_uses_stream_json_false():
    assert AgyDef().uses_stream_json is False


# ── OpenCode ─────────────────────────────────────────────────────────────────

def test_opencode_run_first(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "run"


def test_opencode_format_json(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--format") + 1] == "json"


def test_opencode_skip_permissions(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                           prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_opencode_resume(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                           prompt="x", session_id="s-99",
                                           config_dir=str(tmp_path))
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "s-99"


def test_opencode_model(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model="o3", mcp_port=9500,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "o3"


def test_opencode_config_in_env(tmp_path):
    _, env_set, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                              prompt="x", config_dir=str(tmp_path))
    assert "OPENCODE_CONFIG" in env_set


def test_opencode_prompt_last(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                           prompt="my-prompt", config_dir=str(tmp_path))
    assert argv[-1] == "my-prompt"


def test_opencode_no_env_strip(tmp_path):
    _, _, strip = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=9500,
                                            prompt="x", config_dir=str(tmp_path))
    assert strip == []


def test_opencode_uses_stream_json_false():
    assert OpenCodeDef().uses_stream_json is False


# ── _sanitize_extra_args: equals-style blocked flags ────────────────────────

def test_sanitize_blocks_equals_style_permission_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=9500,
        config_dir=str(tmp_path),
        extra_args="--permission-mode=acceptEdits --verbose")
    vals = [a for a in argv if "acceptEdits" in a]
    assert len(vals) == 0  # blocked


def test_sanitize_blocks_equals_style_output_format(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=9500,
        config_dir=str(tmp_path),
        extra_args="--output-format=text")
    vals = [a for a in argv if "output-format=text" in a]
    assert len(vals) == 0
