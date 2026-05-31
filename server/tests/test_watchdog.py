"""Tests for ProactiveWatchdog."""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock
from unity_mcp.watchdog import ProactiveWatchdog


# ── maybe_trigger() ─────────────────────────────────────────────────────────

def test_maybe_trigger_ignores_read_cmds():
    wd = ProactiveWatchdog(AsyncMock(), interval=3)
    wd.maybe_trigger("get_hierarchy")
    assert wd._counter == 0


def test_maybe_trigger_increments_counter():
    wd = ProactiveWatchdog(AsyncMock(), interval=5)
    wd.maybe_trigger("set_property")
    assert wd._counter == 1


def test_maybe_trigger_high_blast_immediate():
    """delete_object / batch / scene trigger scan immediately (threshold=1)."""
    send = AsyncMock(return_value="ok")
    wd = ProactiveWatchdog(send, interval=10)
    wd.maybe_trigger("delete_object")
    assert wd._counter == 0  # reset after triggering


def test_maybe_trigger_normal_cmd_reaches_threshold():
    send = AsyncMock(return_value="ok")
    wd = ProactiveWatchdog(send, interval=3)
    for _ in range(3):
        wd.maybe_trigger("set_property")
    assert wd._counter == 0  # reset after trigger


# ── consume_alert() ──────────────────────────────────────────────────────────

def test_consume_alert_returns_none_when_empty():
    wd = ProactiveWatchdog(AsyncMock())
    assert wd.consume_alert() is None


def test_consume_alert_returns_pending():
    wd = ProactiveWatchdog(AsyncMock())
    wd._pending_alert = "ISSUES: broken ref"
    assert wd.consume_alert() == "ISSUES: broken ref"


def test_consume_alert_clears_after_consume():
    wd = ProactiveWatchdog(AsyncMock())
    wd._pending_alert = "alert"
    wd.consume_alert()
    assert wd.consume_alert() is None


# ── _scan() ─────────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_scan_collects_issues():
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return "ERROR: broken ref /Player"
        return ""  # empty console
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is not None
    assert "ISSUES" in wd._pending_alert


@pytest.mark.asyncio
async def test_scan_no_issues_no_alert():
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is None


@pytest.mark.asyncio
async def test_watchdog_cancel_stops_task():
    """cancel() must cancel the running task and mark it done within 2s."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    # Manually set a long-running task
    wd._task = asyncio.get_event_loop().create_task(asyncio.sleep(10))
    assert not wd._task.done()
    await wd.cancel()
    assert wd._task.done()


@pytest.mark.asyncio
async def test_watchdog_cancel_no_task_is_noop():
    """cancel() with no task should not raise."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    await wd.cancel()  # must not raise


@pytest.mark.asyncio
async def test_watchdog_cancel_already_done_is_noop():
    """cancel() on an already-done task should be safe."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    wd._task = asyncio.get_event_loop().create_task(asyncio.sleep(0))
    await asyncio.sleep(0)  # schedule
    await asyncio.sleep(0)  # run
    assert wd._task.done()
    await wd.cancel()  # must not raise


@pytest.mark.asyncio
async def test_dedup_within_60s_returns_no_alert():
    """Same issue within 60s should not set a new alert."""
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return "ERROR: broken ref"
        return ""
    wd = ProactiveWatchdog(send)
    await wd._scan()
    first_alert = wd.consume_alert()
    assert first_alert is not None
    # Second scan immediately — same hash, within 60s
    await wd._scan()
    assert wd._pending_alert is None
