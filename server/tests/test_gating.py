"""Tests for capability gating (Part A)."""
import pytest
from unittest.mock import MagicMock, AsyncMock


# --- helpers ---

def _make_tool(name: str):
    t = MagicMock()
    t.name = name
    return t


# --- gating module tests ---

def test_tier1_visible():
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("get_hierarchy"), _make_tool("batch")]
    assert filter_by_tier(tools) == tools


def test_tier2_hidden():
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("animation"), _make_tool("shader")]
    result = filter_by_tier(tools)
    assert result == []


def test_enable_category_returns_tool_names():
    from unity_mcp.tools.gating import enable_category, reset
    reset()
    names = enable_category("animation")
    assert "animation" in names
    assert "timeline" in names


def test_enable_category_makes_tools_visible():
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("animation")
    tools = [_make_tool("animation"), _make_tool("shader")]
    result = filter_by_tier(tools)
    names = [t.name for t in result]
    assert "animation" in names
    assert "shader" not in names


def test_unknown_category_error():
    from unity_mcp.tools.gating import enable_category, reset
    reset()
    with pytest.raises(ValueError, match="Unknown category"):
        enable_category("nonexistent")


def test_filter_preserves_unknown_tools():
    """Plugin tools not in any tier list pass through (opt-in)."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("custom_plugin_a"), _make_tool("custom_plugin_b")]
    result = filter_by_tier(tools)
    assert len(result) == 2


def test_reset_clears_session():
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("animation")
    reset()
    tools = [_make_tool("animation")]
    assert filter_by_tier(tools) == []


def test_is_visible_after_enable():
    from unity_mcp.tools.gating import enable_category, is_visible, reset
    reset()
    assert not is_visible("animation")
    enable_category("animation")
    assert is_visible("animation")


def test_is_visible_tier1():
    from unity_mcp.tools.gating import is_visible, reset
    reset()
    assert is_visible("get_hierarchy")
    assert is_visible("batch")


def test_get_categories():
    from unity_mcp.tools.gating import get_categories
    cats = get_categories()
    assert "animation" in cats
    assert "runtime" in cats
    assert isinstance(cats["animation"], (set, frozenset))


@pytest.mark.asyncio
async def test_discover_tools_lists_categories():
    from unity_mcp.tools.gating import discover_tools, reset
    reset()
    result = await discover_tools(enable=False)
    assert "animation" in result
    assert "runtime" in result


@pytest.mark.asyncio
async def test_discover_tools_enables():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()
    await discover_tools(category="animation")
    assert is_visible("animation")


@pytest.mark.asyncio
async def test_discover_tools_browse_only():
    from unity_mcp.tools.gating import discover_tools, is_visible, reset
    reset()
    result = await discover_tools(category="animation", enable=False)
    assert not is_visible("animation")
    assert "animation" in result


# --- TDD: runtime tools in TIER1 ---

def test_runtime_tools_in_tier1():
    from unity_mcp.tools.gating import TIER1
    for name in ("invoke_method", "run_playtest", "query_state", "wait_until", "move_to",
                 "set_runtime_property", "test_step", "fuzz_playtest"):
        assert name in TIER1, f"{name} missing from TIER1"


def test_batch_allows_invoke_method():
    """invoke_method is sync — must NOT be in BatchHelper async blocklist."""
    import re
    from pathlib import Path
    path = str(Path(__file__).parents[2] / "unity-plugin" / "Editor" / "BatchHelper.cs")
    src = open(path).read()
    # Find the async-blocklist condition line
    match = re.search(r'if \(cmd == "wait_until"[^)]+\)', src)
    assert match, "async blocklist line not found"
    assert "invoke_method" not in match.group(), "invoke_method should NOT be in async blocklist"


def test_batch_blocks_run_playtest():
    """run_playtest is async — must be in BatchHelper async blocklist."""
    import re
    from pathlib import Path
    path = str(Path(__file__).parents[2] / "unity-plugin" / "Editor" / "BatchHelper.cs")
    src = open(path).read()
    match = re.search(r'if \(cmd == "wait_until"[^)]+\)', src)
    assert match, "async blocklist line not found"
    assert "run_playtest" in match.group(), "run_playtest must be in async blocklist"


# --- TDD Phase 2: register_tools() self-registration ---

def test_register_tools_adds_to_category():
    """register_tools() adds tools to a named category."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat", {"tool_x", "tool_y"})
    try:
        assert "tool_x" in gating.CATEGORIES["test_cat"]
        assert "tool_y" in gating.CATEGORIES["test_cat"]
    finally:
        del gating.CATEGORIES["test_cat"]
        gating._ALL_KNOWN.discard("tool_x")
        gating._ALL_KNOWN.discard("tool_y")


def test_register_tools_adds_to_tier1():
    """register_tools(tier1=...) promotes tools to TIER1."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat2", {"tool_a", "tool_b"}, tier1={"tool_a"})
    try:
        assert "tool_a" in gating.TIER1
    finally:
        gating.TIER1.discard("tool_a")
        del gating.CATEGORIES["test_cat2"]
        gating._ALL_KNOWN.discard("tool_a")
        gating._ALL_KNOWN.discard("tool_b")


def test_register_tools_idempotent():
    """Calling register_tools twice does not duplicate entries."""
    from unity_mcp.tools import gating
    gating.register_tools("test_cat3", {"tool_z"}, tier1={"tool_z"})
    try:
        size_before = len(gating.TIER1)
        gating.register_tools("test_cat3", {"tool_z"}, tier1={"tool_z"})
        assert len(gating.TIER1) == size_before  # set.update is idempotent
    finally:
        gating.TIER1.discard("tool_z")
        del gating.CATEGORIES["test_cat3"]
        gating._ALL_KNOWN.discard("tool_z")


# --- Integration tests: plugin self-registration composability ---

def test_register_tools_makes_unknown_tool_gated():
    """unknown tool passes filter; after register_tools without tier1 it becomes known+gated."""
    from unity_mcp.tools import gating
    tool = _make_tool("new_shiny_tool")
    # Before registration: unknown → passes through
    assert gating.filter_by_tier([tool]) == [tool]
    gating.register_tools("test_new_cat", {"new_shiny_tool"})
    try:
        # Now known, not in tier1, not session-enabled → filtered out
        assert gating.filter_by_tier([tool]) == []
    finally:
        gating._ALL_KNOWN.discard("new_shiny_tool")
        del gating.CATEGORIES["test_new_cat"]


# --- TDD: audit fixes ---

def test_set_parent_in_tier1():
    """set_parent is a core mutation (like delete_object) and must be in TIER1."""
    from unity_mcp.tools.gating import TIER1
    assert "set_parent" in TIER1


def test_unwire_event_in_object_category():
    """unwire_event pairs with wire_event and must be in the object category."""
    from unity_mcp.tools.gating import CATEGORIES
    assert "unwire_event" in CATEGORIES["object"]


def test_set_parent_visible_without_enable():
    """set_parent must be visible by default (TIER1), no category unlock needed."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("set_parent")]
    assert filter_by_tier(tools) == tools


def test_unwire_event_visible_after_object_enable():
    """unwire_event becomes visible after enable_category('object')."""
    from unity_mcp.tools.gating import enable_category, filter_by_tier, reset
    reset()
    enable_category("object")
    tools = [_make_tool("unwire_event")]
    assert filter_by_tier(tools) == tools


def test_unwire_event_hidden_without_enable():
    """unwire_event is gated (not in TIER1), hidden by default."""
    from unity_mcp.tools.gating import filter_by_tier, reset
    reset()
    tools = [_make_tool("unwire_event")]
    assert filter_by_tier(tools) == []
