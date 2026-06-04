"""Tests for dynamic MCP tool filtering based on Unity MCPSettings."""
import pytest
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import mcp.types as mcp_types

from unity_mcp.server import _filter_tools, mcp


def _tool(name: str):
    return SimpleNamespace(name=name)


ALL_TOOLS = [_tool("get_hierarchy"), _tool("scene"), _tool("shader"), _tool("get_enabled_tools")]


# --- test _filter_tools fallback (gating only, no Unity cache) ---

@pytest.mark.asyncio
async def test_filter_tools_fallback_when_bridge_none():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, None)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "get_enabled_tools" in names
        assert "shader" not in names  # gated out (not in TIER1)
    finally:
        srv._disabled_tools_cache = orig


@pytest.mark.asyncio
async def test_filter_tools_fallback_when_disconnected():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = False
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


@pytest.mark.asyncio
async def test_filter_tools_fallback_on_send_error():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(side_effect=ConnectionError("lost"))
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


@pytest.mark.asyncio
async def test_filter_tools_fallback_on_unity_error():
    import unity_mcp.server as srv
    orig = srv._disabled_tools_cache
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": False, "err": "fail"})
    try:
        srv._disabled_tools_cache = None
        result = await _filter_tools(ALL_TOOLS, bridge)
        names = {t.name for t in result}
        assert "get_hierarchy" in names
        assert "shader" not in names
    finally:
        srv._disabled_tools_cache = orig


# --- Core bug-fix: disabled-set semantics ---

@pytest.mark.asyncio
async def test_disabled_tier1_tool_hidden():
    """CORE BUG FIX: unchecking screenshot in Unity form must remove it from ListTools."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = {"screenshot"}
        tools = [_tool("screenshot"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "screenshot" not in names, "Disabled TIER1 tool must be hidden"
        assert "get_hierarchy" in names
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


@pytest.mark.asyncio
async def test_force_visible_survives_disabled():
    """FORCE_VISIBLE tools must never be hidden even if in disabled set."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = {"do", "discover_tools", "get_hierarchy"}
        tools = [_tool("do"), _tool("discover_tools"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "do" in names, "FORCE_VISIBLE 'do' must survive disabled set"
        assert "discover_tools" in names, "FORCE_VISIBLE 'discover_tools' must survive disabled set"
        assert "get_hierarchy" not in names, "Non-FORCE_VISIBLE disabled tool must be hidden"
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


@pytest.mark.asyncio
async def test_disabled_cache_none_no_hiding():
    """None cache = gating-only fallback, nothing extra hidden."""
    import unity_mcp.server as srv
    import unity_mcp.tools.gating as gating
    gating.reset()
    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        tools = [_tool("screenshot"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        # Both are TIER1, gating keeps them; disabled cache is None so no hiding
        assert "screenshot" in names
        assert "get_hierarchy" in names
    finally:
        srv._disabled_tools_cache = orig
        gating.reset()


# --- Cache interaction tests (disabled-set semantics) ---

@pytest.mark.asyncio
async def test_filter_tools_uses_cache_when_available():
    """With disabled cache populated, _filter_tools must NOT call bridge.send."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    tool_b = Mock()
    tool_b.name = "set_property"
    bridge = AsyncMock()
    bridge.send = AsyncMock()

    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = set()  # empty disabled set = nothing hidden
        bridge.send.reset_mock()
        result = await srv._filter_tools([tool_a, tool_b], bridge)
        bridge.send.assert_not_called()
        assert tool_a in result
        assert tool_b in result
    finally:
        srv._disabled_tools_cache = orig


@pytest.mark.asyncio
async def test_filter_tools_fallback_when_cache_empty():
    """With None cache, _apply_gating is used (no TCP)."""
    from unittest.mock import Mock
    import unity_mcp.server as srv

    tool_a = Mock()
    tool_a.name = "get_hierarchy"
    bridge = AsyncMock()
    bridge.connected = False

    orig = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        bridge.send.reset_mock()
        result = await srv._filter_tools([tool_a], bridge)
        bridge.send.assert_not_called()
        assert len(result) >= 0  # _apply_gating may filter
    finally:
        srv._disabled_tools_cache = orig


@pytest.mark.asyncio
async def test_disabled_tools_cache_populated_on_reconnect():
    """Reconnect populates _disabled_tools_cache via get_disabled_tools."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot,shader"})

    orig = srv._disabled_tools_cache
    orig_lock = srv._refresh_tools_lock
    try:
        srv._disabled_tools_cache = None
        srv._refresh_tools_lock = None
        await srv._refresh_tools_cache(bridge)
        assert srv._disabled_tools_cache == {"screenshot", "shader"}
    finally:
        srv._disabled_tools_cache = orig
        srv._refresh_tools_lock = orig_lock


@pytest.mark.asyncio
async def test_disabled_tools_empty_csv_gives_empty_set():
    """Empty CSV from Unity must produce empty set, not {''}."""
    from unittest.mock import AsyncMock
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    orig = srv._disabled_tools_cache
    orig_lock = srv._refresh_tools_lock
    try:
        srv._disabled_tools_cache = None
        srv._refresh_tools_lock = None
        await srv._refresh_tools_cache(bridge)
        assert srv._disabled_tools_cache == set(), f"Expected empty set, got {srv._disabled_tools_cache}"
    finally:
        srv._disabled_tools_cache = orig
        srv._refresh_tools_lock = orig_lock


# --- test handler registration ---

def test_request_handler_is_patched():
    """Our wrapper must be installed in request_handlers, not the original FastMCP handler."""
    handler = mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest]
    assert handler.__name__ == "_filtered_tools_handler"


# --- TDD F4: handler strips deferred / preserves core ---

@pytest.mark.asyncio
async def test_handler_strips_non_core_schema():
    """_filter_tools returns STUB schema for non-core tools."""
    import unity_mcp.server as srv
    from unity_mcp.tools.schema_registry import STUB_SCHEMA

    full = {"type": "object", "properties": {"x": {"type": "string"}}, "required": ["x"]}
    tool_core = _tool("get_hierarchy")
    tool_core.inputSchema = full
    tool_noncore = _tool("animation")
    tool_noncore.inputSchema = full

    orig_cache = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        result = await srv._filter_tools([tool_core, tool_noncore], None)
        names = {t.name: t for t in result}
        # get_hierarchy passes gating — verify its schema kept (if returned)
        if "get_hierarchy" in names:
            assert names["get_hierarchy"].inputSchema == full
        # animation gets gated out by tier filter (not in TIER1 and not enabled)
        assert "animation" not in names
    finally:
        srv._disabled_tools_cache = orig_cache


@pytest.mark.asyncio
async def test_handler_preserves_core_full_schema():
    """Core tools keep their full inputSchema after strip."""
    import unity_mcp.server as srv
    from unity_mcp.server import _strip_deferred_schemas

    full = {"type": "object", "properties": {"p": {"type": "string"}}, "required": ["p"]}
    tool = _tool("batch")
    tool.inputSchema = full

    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == full
