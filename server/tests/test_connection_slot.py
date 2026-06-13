import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


def make_mock_bridge(connected: bool = True):
    b = MagicMock()
    b.connect = AsyncMock()
    b.close = AsyncMock()
    b.send = AsyncMock(return_value={"ok": True})
    b.connected = connected
    b.stop_heartbeat = MagicMock()
    return b


@pytest.mark.asyncio
async def test_initial_state():
    from unity_mcp.connection_slot import ConnectionSlot
    s = ConnectionSlot()
    assert s.bridge is None
    assert s.connected is False
    assert s.port == 9500


@pytest.mark.asyncio
async def test_connect_creates_bridge():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        result = await s.connect(9500)
    assert s.bridge is b
    assert s.port == 9500
    assert "Connected" in result
    b.connect.assert_awaited_once()


@pytest.mark.asyncio
async def test_connect_stores_port():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9501)
    assert s.port == 9501


@pytest.mark.asyncio
async def test_connected_reflects_bridge_state():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge(connected=True)
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    assert s.connected is True


@pytest.mark.asyncio
async def test_connect_unavailable_returns_registered():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.connect = AsyncMock(side_effect=OSError("refused"))
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        result = await s.connect(9500)
    assert "9500" in result
    assert s.bridge is b  # bridge still stored


@pytest.mark.asyncio
async def test_reconnect_closes_previous():
    from unity_mcp.connection_slot import ConnectionSlot
    b1 = make_mock_bridge()
    b2 = make_mock_bridge()
    bridges = iter([b1, b2])
    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=lambda h, p, **_: next(bridges)):
        s = ConnectionSlot()
        await s.connect(9500)
        await s.connect(9501)
    b1.stop_heartbeat.assert_called_once()
    b1.close.assert_awaited_once()
    assert s.bridge is b2


@pytest.mark.asyncio
async def test_close_clears_bridge():
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
        await s.close()
    assert s.bridge is None
    b.stop_heartbeat.assert_called()
    b.close.assert_awaited()


@pytest.mark.asyncio
async def test_close_noop_when_empty():
    from unity_mcp.connection_slot import ConnectionSlot
    s = ConnectionSlot()
    await s.close()  # must not raise


@pytest.mark.asyncio
async def test_slot_syncs_port_from_bridge():
    """_sync_port reconnect callback updates slot.port to match bridge._port."""
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.add_reconnect_callback = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    # simulate bridge changing port then the _sync_port callback firing
    b._port = 9501
    # grab the _sync_port callback (last registered)
    sync_cb = b.add_reconnect_callback.call_args_list[-1][0][0]
    sync_cb()
    assert s.port == 9501


@pytest.mark.asyncio
async def test_slot_fires_on_port_change():
    """on_port_change(old, new) fires when bridge._port differs from slot._port."""
    from unity_mcp.connection_slot import ConnectionSlot
    changes = []
    b = make_mock_bridge()
    b.add_reconnect_callback = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot(on_port_change=lambda old, new: changes.append((old, new)))
        await s.connect(9500)
    b._port = 9501
    sync_cb = b.add_reconnect_callback.call_args_list[-1][0][0]
    sync_cb()
    assert changes == [(9500, 9501)]


# ── PY1.test.4: connect() must start heartbeat ───────────────────────────────

@pytest.mark.asyncio
async def test_connect_starts_heartbeat():
    """connect() must call start_heartbeat() on the bridge after successful connect."""
    from unity_mcp.connection_slot import ConnectionSlot
    b = make_mock_bridge()
    b.start_heartbeat = MagicMock()
    with patch("unity_mcp.connection_slot.UnityBridge", return_value=b):
        s = ConnectionSlot()
        await s.connect(9500)
    b.start_heartbeat.assert_called_once()
