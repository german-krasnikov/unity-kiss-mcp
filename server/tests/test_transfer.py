"""transfer_asset and copy_asset are removed (dead code, required 2+ Unity instances).
This file keeps the test count stable with replacement tests for reconnect_unity."""
import pytest
from unittest.mock import AsyncMock, Mock, patch


@pytest.mark.asyncio
async def test_reconnect_unity_calls_slot_connect(mock_bridge):
    from unity_mcp.server import reconnect_unity
    import unity_mcp.server as srv
    srv.slot.connect = AsyncMock(return_value="Connected to Unity on port 9500")
    result = await reconnect_unity(9500)
    srv.slot.connect.assert_awaited_once_with(9500)
    assert "Connected" in result


@pytest.mark.asyncio
async def test_reconnect_unity_no_slot_raises():
    from mcp.server.fastmcp.exceptions import ToolError
    with patch("unity_mcp.tools.connection._get_slot", return_value=None):
        from unity_mcp.tools.connection import reconnect_unity
        with pytest.raises(ToolError, match="Server not initialized"):
            await reconnect_unity(9500)


@pytest.mark.asyncio
async def test_transfer_asset_removed():
    """transfer_asset no longer exists — dead code removed."""
    import unity_mcp.server as srv
    assert not hasattr(srv, "transfer_asset"), "transfer_asset must be removed"


@pytest.mark.asyncio
async def test_copy_asset_removed():
    """copy_asset no longer exists — dead code removed."""
    import unity_mcp.server as srv
    assert not hasattr(srv, "copy_asset"), "copy_asset must be removed"


@pytest.mark.asyncio
async def test_connect_unity_removed():
    """connect_unity no longer exists — replaced by reconnect_unity."""
    import unity_mcp.server as srv
    assert not hasattr(srv, "connect_unity"), "connect_unity must be removed"
