"""TDD tests for parent-death detection in bridge_heartbeat.py."""
import asyncio
import os
from unittest.mock import patch, MagicMock, AsyncMock, PropertyMock

import pytest

import unity_mcp.bridge_heartbeat as hb_module


# ---------------------------------------------------------------------------
# _ORIGINAL_PPID — captured at module level
# ---------------------------------------------------------------------------

def test_original_ppid_is_module_level_int():
    """_ORIGINAL_PPID must be an int captured at module import time."""
    assert isinstance(hb_module._ORIGINAL_PPID, int)
    assert hb_module._ORIGINAL_PPID > 0


def test_original_ppid_matches_real_ppid_at_import():
    """_ORIGINAL_PPID should match the actual ppid (we haven't reparented)."""
    assert hb_module._ORIGINAL_PPID == os.getppid()


# ---------------------------------------------------------------------------
# _heartbeat_tick parent-death guard
# ---------------------------------------------------------------------------

def _make_bridge_mixin():
    """Return a minimal HeartbeatMixin instance with mocked internals."""
    from unity_mcp.bridge_heartbeat import HeartbeatMixin

    class _Stub(HeartbeatMixin):
        def __init__(self):
            self._heartbeat_task = None
            self._heartbeat_interval = 15.0
            self._ping_failures = 0
            self._last_reconnect_at = 0.0
            self._min_reconnect_interval = 2.0
            self._lock = asyncio.Lock()
            self._counter = 0
            self._reconnect_started_at = None
            self._startup_grace_expired = False

        @property
        def connected(self):
            return self._connected

        def _probe_busy(self):
            return False

        def _reconnect_cooldown_ok(self):
            return False

        async def _reconnect(self):
            pass

    return _Stub()


@pytest.mark.asyncio
async def test_parent_death_detection_calls_exit():
    """When os.getppid() != _ORIGINAL_PPID, heartbeat calls os._exit(0)."""
    bridge = _make_bridge_mixin()
    bridge._connected = True  # so tick reaches ppid check

    # Make _ORIGINAL_PPID differ from current ppid
    fake_original = os.getppid() + 9999
    with patch.object(hb_module, "_ORIGINAL_PPID", fake_original), \
         patch("os.getppid", return_value=os.getppid()), \
         patch("os._exit") as mock_exit:
        await bridge._heartbeat_tick(15.0)
        mock_exit.assert_called_once_with(0)


@pytest.mark.asyncio
async def test_parent_alive_no_exit():
    """When ppid unchanged, heartbeat does NOT call os._exit(0)."""
    bridge = _make_bridge_mixin()
    bridge._connected = False  # disconnected → short sleep path

    real_ppid = os.getppid()
    with patch.object(hb_module, "_ORIGINAL_PPID", real_ppid), \
         patch("os.getppid", return_value=real_ppid), \
         patch("os._exit") as mock_exit, \
         patch("asyncio.sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(15.0)
        mock_exit.assert_not_called()
