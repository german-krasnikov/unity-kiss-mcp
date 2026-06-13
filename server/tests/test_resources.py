"""Tests for MCP resources (Part B)."""
import pytest
from unittest.mock import AsyncMock


@pytest.mark.asyncio
async def test_hierarchy_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="Root\n  Player")
    result = await resources.scene_hierarchy()
    resources._send.assert_called_once_with("get_hierarchy", {"summary": "true"})
    assert "Root" in result


@pytest.mark.asyncio
async def test_console_errors_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="[Error] NullRef")
    result = await resources.console_errors()
    resources._send.assert_called_once_with("get_console", {"count": 20, "level": "Error"})
    assert "Error" in result


@pytest.mark.asyncio
async def test_editor_state_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="playing: false")
    result = await resources.editor_state()
    resources._send.assert_called_once_with("editor", {"action": "state"})
    assert "playing" in result


def test_tool_categories_no_bridge():
    """Pure Python — no bridge needed."""
    import asyncio
    from unity_mcp import resources
    result = asyncio.get_event_loop().run_until_complete(resources.tool_categories())
    assert "animation" in result
    assert "runtime" in result


def test_resources_registered():
    """register() calls mcp.resource for each URI."""
    from unittest.mock import MagicMock, AsyncMock
    from unity_mcp import resources

    mcp = MagicMock()
    registered_uris = []

    def fake_resource(uri):
        registered_uris.append(uri)
        return lambda fn: fn  # decorator returns fn unchanged

    mcp.resource = fake_resource
    send = AsyncMock()
    resources.register(mcp, send, lambda **kw: kw)

    assert "unity://scene/hierarchy" in registered_uris
    assert "unity://console/errors" in registered_uris
    assert "unity://editor/state" in registered_uris
    assert "unity://tools/categories" in registered_uris


# ---------------------------------------------------------------------------
# PY2.test.5: _safe_send exception-swallowing returns '[disconnected: ...]'
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_safe_send_returns_disconnected_on_exception():
    """_send raising RuntimeError → scene_hierarchy() returns '[disconnected: ...]'."""
    from unity_mcp import resources
    resources._send = AsyncMock(side_effect=RuntimeError("gone"))
    result = await resources.scene_hierarchy()
    assert result.startswith("[disconnected:"), f"Expected '[disconnected:', got: {result!r}"
