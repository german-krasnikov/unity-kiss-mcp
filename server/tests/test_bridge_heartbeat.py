"""Tests for HeartbeatMixin — hard deadline timer behavior."""
import asyncio
import time
import pytest
from unittest.mock import MagicMock, patch, AsyncMock

from unity_mcp.bridge import UnityBridge
from unity_mcp.bridge_heartbeat import HARD_DEADLINE_S


def _make_bridge_disconnected(busy: bool = False) -> UnityBridge:
    """Return a disconnected UnityBridge with a mocked probe."""
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = busy
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    bridge = UnityBridge("127.0.0.1", 9999, probe=probe)
    # Leave _reader/_writer = None so connected == False
    return bridge


# ── Item 8: hard deadline uses separate clock, unaffected by busy resets ────


async def test_hard_deadline_started_at_not_reset_on_busy():
    """_hard_deadline_started_at must keep its initial value when busy=True.

    Fix: separate _hard_deadline_started_at variable that is set once and never
    reset by the busy-state logic (which resets _reconnect_started_at for STARTUP_GRACE).
    """
    bridge = _make_bridge_disconnected(busy=True)

    # Simulate: hard deadline clock was set 10s ago
    initial = time.monotonic() - 10.0
    bridge._hard_deadline_started_at = initial

    with patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    # Hard deadline clock must NOT have been reset
    assert bridge._hard_deadline_started_at == initial, (
        f"_hard_deadline_started_at was reset from {initial} to {bridge._hard_deadline_started_at}"
    )


async def test_hard_deadline_fires_even_when_busy():
    """Hard deadline must trigger even if Unity is permanently busy.

    Bug: old code used _reconnect_started_at for hard deadline — resetting it
    on every busy tick made elapsed always ~0, so HARD_DEADLINE_S never fired.
    Fix: _hard_deadline_started_at is set once and never reset while busy.
    """
    bridge = _make_bridge_disconnected(busy=True)

    # Set hard deadline clock to past the threshold
    bridge._hard_deadline_started_at = time.monotonic() - (HARD_DEADLINE_S + 5.0)

    with patch.object(asyncio, "sleep", new=AsyncMock()):
        await bridge._heartbeat_tick(interval=15.0)

    assert bridge._startup_grace_expired is True, (
        "Hard deadline did not fire despite hard_elapsed > HARD_DEADLINE_S while busy"
    )
