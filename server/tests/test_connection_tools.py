import pytest
from unittest.mock import AsyncMock, Mock, patch


@pytest.mark.asyncio
async def test_list_connections(mock_bridge):
    from unity_mcp.server import list_connections
    result = await list_connections()
    assert "9500" in result
    assert "connected" in result


@pytest.mark.asyncio
async def test_reconnect_unity(mock_bridge):
    from unity_mcp.server import reconnect_unity
    result = await reconnect_unity(9500)
    assert "Connected" in result


@pytest.mark.asyncio
async def test_reconnect_unity_auto_discovers(mock_bridge):
    """reconnect_unity(0) auto-discovers port via read_unity_port."""
    from unity_mcp.tools.connection import reconnect_unity
    with patch("unity_mcp.tools.connection.read_unity_port", return_value=9501) as mock_disc:
        result = await reconnect_unity(0)
    mock_disc.assert_called_once()
    assert result is not None


@pytest.mark.asyncio
async def test_send_routes_to_active(mock_bridge):
    """Verify _send() uses slot.bridge (which is mock_bridge)."""
    from unity_mcp.server import get_hierarchy
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Root\n  Child"})
    result = await get_hierarchy()
    mock_bridge.send.assert_awaited_once()
    assert "Root" in result


@pytest.mark.asyncio
async def test_send_no_slot_raises():
    """Verify _send() raises ToolError when slot is None."""
    from mcp.server.fastmcp.exceptions import ToolError
    with patch("unity_mcp.server.slot", None):
        from unity_mcp.server import get_hierarchy
        with pytest.raises(ToolError, match="Server not initialized"):
            await get_hierarchy()


@pytest.mark.asyncio
async def test_send_no_bridge_raises():
    """Verify _send() raises ToolError when slot.bridge is None."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_slot = Mock()
    mock_slot.bridge = None
    with patch("unity_mcp.server.slot", mock_slot):
        from unity_mcp.server import get_hierarchy
        with pytest.raises(ToolError, match="No Unity connection configured"):
            await get_hierarchy()
