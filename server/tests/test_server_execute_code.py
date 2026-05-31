"""Tests for execute_code tool."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.server import execute_code


@pytest.mark.asyncio
async def test_execute_code_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "2"}
    result = await execute_code("return (1+1).ToString();")
    call = mock_bridge.send.call_args
    assert call[0][0] == "execute_code"
    assert call[0][1] == {"code": "return (1+1).ToString();", "undo_label": "execute_code"}
    assert result == "2"


@pytest.mark.asyncio
async def test_execute_code_with_undo_label(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "done"}
    await execute_code("var go = new GameObject();", undo_label="create_test_object")
    call = mock_bridge.send.call_args
    assert call[0][1]["undo_label"] == "create_test_object"


@pytest.mark.asyncio
async def test_execute_code_registered(mock_bridge):
    """Tool must be importable and callable via MCP."""
    from unity_mcp import server as srv
    assert hasattr(srv, "execute_code"), "execute_code must be registered as MCP tool"
