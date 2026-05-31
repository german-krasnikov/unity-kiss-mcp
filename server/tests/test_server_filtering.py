"""Tests for dynamic MCP tool filtering based on Unity MCPSettings."""
import pytest
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import mcp.types as mcp_types

from unity_mcp.server import _filter_tools, mcp


def _tool(name: str):
    return SimpleNamespace(name=name)


ALL_TOOLS = [_tool("get_hierarchy"), _tool("scene"), _tool("shader"), _tool("get_enabled_tools")]


# --- test _filter_tools pure function ---

@pytest.mark.asyncio
async def test_filter_tools_returns_enabled_subset():
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": "get_hierarchy,scene"})
    result = await _filter_tools(ALL_TOOLS, bridge)
    assert {t.name for t in result} == {"get_hierarchy", "scene", "get_enabled_tools"}


@pytest.mark.asyncio
async def test_filter_tools_fallback_when_bridge_none():
    result = await _filter_tools(ALL_TOOLS, None)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "get_enabled_tools" in names
    assert "shader" not in names  # gated out (not in TIER1)


@pytest.mark.asyncio
async def test_filter_tools_fallback_when_disconnected():
    bridge = AsyncMock()
    bridge.connected = False
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


@pytest.mark.asyncio
async def test_filter_tools_fallback_on_send_error():
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(side_effect=ConnectionError("lost"))
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


@pytest.mark.asyncio
async def test_filter_tools_fallback_on_unity_error():
    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": False, "err": "fail"})
    result = await _filter_tools(ALL_TOOLS, bridge)
    names = {t.name for t in result}
    assert "get_hierarchy" in names
    assert "shader" not in names


# --- TDD: session_enabled + Unity cache interaction ---

@pytest.mark.asyncio
async def test_tier1_tool_survives_unity_cache_filter():
    """TIER1 tool passes even without discover_tools or Unity cache."""
    import unity_mcp.server as srv_mod
    import unity_mcp.tools.gating as gating
    gating.reset()
    srv_mod._enabled_tools_cache = {"get_hierarchy", "get_enabled_tools"}
    try:
        tools = [_tool("batch"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "batch" in names, "TIER1 tool dropped by _filter_tools"
    finally:
        srv_mod._enabled_tools_cache = None
        gating.reset()


@pytest.mark.asyncio
async def test_session_enabled_not_in_unity_cache_survives_filter():
    """session-enabled non-TIER1 tool NOT in Unity cache passes _filter_tools."""
    import unity_mcp.server as srv_mod
    import unity_mcp.tools.gating as gating
    gating.reset()
    gating.enable_category("animation")
    srv_mod._enabled_tools_cache = {"get_hierarchy", "get_enabled_tools"}
    try:
        tools = [_tool("animation"), _tool("get_hierarchy")]
        result = await _filter_tools(tools, None)
        names = {t.name for t in result}
        assert "animation" in names, "session-enabled tool was dropped by _filter_tools"
    finally:
        srv_mod._enabled_tools_cache = None
        gating.reset()


# --- test handler registration ---

def test_request_handler_is_patched():
    """Our wrapper must be installed in request_handlers, not the original FastMCP handler."""
    handler = mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest]
    # The installed handler should be our _filtered_tools_handler, not the original.
    # We verify by checking the function name.
    assert handler.__name__ == "_filtered_tools_handler"
