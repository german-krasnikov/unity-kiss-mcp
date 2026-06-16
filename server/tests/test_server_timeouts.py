"""Tests for per-tool timeout overrides (Tier 1a)."""
from unity_mcp.server import COMMAND_TIMEOUTS


def test_command_timeouts_dict_has_slow_commands():
    assert COMMAND_TIMEOUTS["run_tests"] == 10.0
    assert COMMAND_TIMEOUTS["run_playtest"] == 120.0
    assert COMMAND_TIMEOUTS["fuzz_playtest"] == 120.0
    assert COMMAND_TIMEOUTS["compile_preflight"] == 60.0
    assert COMMAND_TIMEOUTS["batch"] == 60.0


def test_default_timeout_is_30s():
    assert COMMAND_TIMEOUTS.get("get_hierarchy", 30.0) == 30.0
    assert COMMAND_TIMEOUTS.get("inspect", 30.0) == 30.0
