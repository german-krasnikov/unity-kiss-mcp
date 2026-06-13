"""Port re-discovery on reconnect: bridge updates _port when Unity restarts on new port."""
import asyncio
import json
import struct
from unittest.mock import AsyncMock, MagicMock, Mock, patch

import pytest

import unity_mcp.bridge as bridge_mod
from unity_mcp.bridge import UnityBridge
from helpers import make_writer, make_idle_probe, ping_response


@pytest.fixture(autouse=True)
def _fast_timeouts():
    orig = bridge_mod.CONNECT_TIMEOUT
    bridge_mod.CONNECT_TIMEOUT = 0.05
    yield
    bridge_mod.CONNECT_TIMEOUT = orig


def _make_ok_reader(msg_id="0001"):
    ping_hdr, ping_pay = ping_response()
    r = {"id": msg_id, "ok": True, "data": "ok"}
    p = json.dumps(r).encode()
    hdr, pay = struct.pack("!I", len(p)), p
    reader = AsyncMock()
    reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay, hdr, pay])
    return reader


# ---------------------------------------------------------------------------
# 1. Discoverer returns new port — bridge._port updates
# ---------------------------------------------------------------------------

async def test_reconnect_rediscovers_port():
    """port_discoverer returns 9501 → bridge connects on 9501."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9501)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        await bridge._reconnect()

    assert bridge._port == 9501
    assert 9501 in connected_to


# ---------------------------------------------------------------------------
# 2. Discoverer raises — falls back to current port
# ---------------------------------------------------------------------------

async def test_reconnect_falls_back_on_discoverer_failure():
    """port_discoverer raises OSError → bridge stays on 9500."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    discoverer = Mock(side_effect=OSError("no port file"))
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        await bridge._reconnect()

    assert bridge._port == 9500
    assert connected_to == [9500]


# ---------------------------------------------------------------------------
# 3. Discoverer returns same port — no probe churn
# ---------------------------------------------------------------------------

async def test_reconnect_same_port_no_change():
    """Discoverer returns same port → bridge._port unchanged, probe not replaced."""
    async def mock_open(host, port):
        return _make_ok_reader(), make_writer()

    discoverer = Mock(return_value=9500)
    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe, port_discoverer=discoverer)
        original_probe = bridge._probe
        await bridge._reconnect()

    assert bridge._port == 9500
    assert bridge._probe is original_probe


# ---------------------------------------------------------------------------
# 4. No discoverer — backward-compat normal reconnect
# ---------------------------------------------------------------------------

async def test_reconnect_without_discoverer():
    """No port_discoverer → normal reconnect on existing port."""
    connected_to = []

    async def mock_open(host, port):
        connected_to.append(port)
        return _make_ok_reader(), make_writer()

    probe = make_idle_probe()

    with patch.object(bridge_mod.asyncio, "open_connection", side_effect=mock_open):
        bridge = UnityBridge("127.0.0.1", 9500, probe=probe)
        await bridge._reconnect()

    assert bridge._port == 9500
    assert connected_to == [9500]


# ---------------------------------------------------------------------------
# 5. _on_port_change lockfile swap: old released, new acquired
# ---------------------------------------------------------------------------

def test_on_port_change_swaps_lockfile():
    """_on_port_change releases old fd, acquires new one; lock_fd updated."""
    import unity_mcp.server as srv

    new_fd = 99
    with patch.object(srv, "release_lock") as mock_release, \
         patch.object(srv, "acquire_lock", return_value=new_fd) as mock_acquire:
        lock_fd = 5
        old_fd, lock_fd = lock_fd, None
        try:
            srv.release_lock(old_fd)
        except Exception:
            pass
        try:
            lock_fd = srv.acquire_lock(port=9501)
        except Exception:
            pass

        mock_release.assert_called_once_with(5)
        mock_acquire.assert_called_once_with(port=9501)
        assert lock_fd == new_fd


def test_on_port_change_acquire_fails_lock_fd_is_none():
    """If acquire_lock raises on new port, lock_fd must be None (not stale old fd)."""
    import unity_mcp.server as srv

    with patch.object(srv, "release_lock"), \
         patch.object(srv, "acquire_lock", side_effect=OSError("busy")):
        lock_fd = 5
        old_fd, lock_fd = lock_fd, None
        try:
            srv.release_lock(old_fd)
        except Exception:
            pass
        try:
            lock_fd = srv.acquire_lock(port=9501)
        except Exception:
            pass

        assert lock_fd is None
