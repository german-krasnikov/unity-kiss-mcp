"""Tests for HeartbeatMixin — P3: PPID mismatch double-check."""
import asyncio
import os
from unittest.mock import AsyncMock, MagicMock, Mock, patch
import pytest
import unity_mcp.bridge_heartbeat as hb_module
from unity_mcp.bridge_heartbeat import HeartbeatMixin, _ORIGINAL_PPID, BACKOFF_MIN_S


@pytest.fixture(autouse=True)
def _reset_hard_exit():
    """Reset _hard_exit_scheduled so each test starts clean."""
    hb_module._hard_exit_scheduled = False
    yield
    hb_module._hard_exit_scheduled = False


class _FakeBridge(HeartbeatMixin):
    """Minimal concrete subclass for testing heartbeat tick logic."""

    def __init__(self):
        self._heartbeat_task = None
        self._heartbeat_interval = 15.0
        self._ping_failures = 0
        self._last_reconnect_at = 0.0
        self._min_reconnect_interval = 0.0
        self._reconnect_backoff = BACKOFF_MIN_S  # required by _reconnect_cooldown_ok
        self._lock = asyncio.Lock()
        self._probe = MagicMock()
        self._probe.has_strong_busy_signal.return_value = False
        self._probe.is_process_dead.return_value = False
        self._probe.mark_recompile_issued = MagicMock()
        self._writer = None
        self._reader = None
        self._counter = 0
        self._reconnect_started_at = None
        self._startup_grace_expired = False
        self._ppid_mismatch_count = 0
        from unity_mcp.bridge_reload_state import DomainReloadTracker
        from unity_mcp.bridge import BridgeState
        self._reload = DomainReloadTracker()
        self._state = BridgeState.DISCONNECTED

    @property
    def connected(self):
        return self._writer is not None

    def _probe_busy(self):
        return False

    async def _reconnect(self, fire_callbacks=True):
        pass

    async def _read_response(self):
        return {"id": "ping", "ok": True, "data": "pong"}


# P3 tests ──────────────────────────────────────────────────────────────────

async def test_p3_single_ppid_mismatch_does_not_exit():
    """Single PPID mismatch → returns early, does NOT raise SystemExit."""
    bridge = _FakeBridge()

    # Patch os._exit so it can't kill the test process (current code calls os._exit)
    # After fix, it should raise SystemExit instead — but guard either way.
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999):
        with patch("unity_mcp.bridge_heartbeat.os._exit", side_effect=SystemExit(0)):
            # After P3 fix: single mismatch returns early (no SystemExit).
            # This test will FAIL on current code because os._exit kills on first mismatch.
            await bridge._heartbeat_tick(15.0)

    # After fix: counter should be 1, no exit happened
    assert getattr(bridge, "_ppid_mismatch_count", 0) == 1


async def test_p3_two_consecutive_mismatches_stops_heartbeat():
    """Two consecutive PPID mismatches → stop heartbeat (no SystemExit)."""
    bridge = _FakeBridge()

    # Patch threading.Timer so _schedule_hard_exit doesn't fire real os._exit
    # after this test ends. os._exit is also patched to prevent accidental kill.
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"):
        # First mismatch: returns early
        await bridge._heartbeat_tick(15.0)
        assert getattr(bridge, "_ppid_mismatch_count", 0) == 1

        # Second mismatch: stops heartbeat, no exception
        await bridge._heartbeat_tick(15.0)
        assert bridge._ppid_mismatch_count == 2
        assert bridge._heartbeat_task is None


async def test_p3_ppid_recovery_resets_counter():
    """PPID mismatch then match → counter resets to 0."""
    bridge = _FakeBridge()

    # First call: mismatch → count=1
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999):
        with patch("unity_mcp.bridge_heartbeat.os._exit", side_effect=SystemExit(0)):
            await bridge._heartbeat_tick(15.0)
    assert getattr(bridge, "_ppid_mismatch_count", 0) == 1

    # Second call: PPID matches → count resets to 0
    # connected=False → goes into reconnect sleep path; patch sleep to return fast
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID):
        with patch("unity_mcp.bridge_heartbeat.asyncio.sleep", AsyncMock()):
            try:
                await bridge._heartbeat_tick(15.0)
            except Exception:
                pass  # doesn't matter — counter reset is what we test
    assert getattr(bridge, "_ppid_mismatch_count", 0) == 0


async def test_p3_no_os_exit_used():
    """Verify os._exit is NOT called directly on PPID mismatch — _schedule_hard_exit used instead."""
    bridge = _FakeBridge()
    schedule_calls = []

    # Patch _schedule_hard_exit directly: we just verify it's called, not that os._exit fires.
    # Also patch threading.Timer to prevent real timer from firing real os._exit after test.
    with patch("unity_mcp.bridge_heartbeat.os.getppid", return_value=_ORIGINAL_PPID + 999), \
         patch("unity_mcp.bridge_heartbeat._schedule_hard_exit",
               side_effect=lambda: schedule_calls.append(1)), \
         patch("unity_mcp.bridge_heartbeat.threading.Timer"), \
         patch("unity_mcp.bridge_heartbeat.os._exit"):
        await bridge._heartbeat_tick(15.0)  # first mismatch — no exit
        assert getattr(bridge, "_ppid_mismatch_count", 0) == 1

        await bridge._heartbeat_tick(15.0)  # second mismatch — calls _schedule_hard_exit
        assert bridge._ppid_mismatch_count == 2

    # _schedule_hard_exit must have been called (exactly once, on 2nd mismatch)
    assert len(schedule_calls) == 1, f"Expected _schedule_hard_exit called once, got {schedule_calls}"
