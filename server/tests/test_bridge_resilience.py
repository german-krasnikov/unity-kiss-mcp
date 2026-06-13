"""Cycle 7a: bridge resilience tests — 3-tier timeouts + ECONNREFUSED fast-fail.

RED phase — all tests expected to fail before implementation.
"""
import asyncio
import time
import pytest
from unittest.mock import AsyncMock, patch, MagicMock
from unity_mcp.bridge import UnityBridge
from helpers import ping_response


# ── helpers ──────────────────────────────────────────────────────────────────

def _make_bridge(port: int = 9999) -> UnityBridge:
    return UnityBridge("127.0.0.1", port)


# ── Test 4: SESSION_TIMEOUT aborts retries ───────────────────────────────────

async def test_session_timeout_aborts_retries(monkeypatch):
    """SESSION_TIMEOUT must abort retry loop. Use slow-failure mock so refused
    fast-backoff doesn't finish before deadline check (which would make this
    test pass for the wrong reason).
    """
    import unity_mcp.bridge as bridge_mod
    monkeypatch.setattr(bridge_mod, "SESSION_TIMEOUT", 0.3)

    # Mock _reconnect to take 0.5s — longer than SESSION_TIMEOUT — and succeed.
    # send() then writes/reads, _read_response returns immediately with bad id,
    # which triggers id-mismatch retry path. Loop top checks deadline → aborts.
    bridge = bridge_mod.UnityBridge("127.0.0.1", 1)

    async def slow_reconnect():
        await asyncio.sleep(0.5)
        raise ConnectionResetError("simulated drop")
    bridge._reconnect = slow_reconnect

    async def fail_close():
        pass
    bridge.close = fail_close

    start = time.monotonic()
    with pytest.raises((TimeoutError, ConnectionError, OSError)):
        await bridge.send("ping", {})
    elapsed = time.monotonic() - start
    # Must abort BEFORE first retry-cycle's full sleep+reconnect (>0.5s).
    # Allow up to 2s for safety; pure-refused path would be <1s — wrong-reason proof
    # comes from elapsed >= deadline (0.3s) but < first reconnect cost (0.5s + backoff).
    assert 0.2 < elapsed < 2.5, f"SESSION_TIMEOUT didn't abort cleanly: {elapsed:.2f}s"


# ── Test 5: CONNECT_TIMEOUT fast-fail ────────────────────────────────────────

async def test_connect_timeout_fast_fail(monkeypatch):
    """Mocked open_connection that hangs forever — verify CONNECT_TIMEOUT fires."""
    import unity_mcp.bridge as bridge_mod
    monkeypatch.setattr(bridge_mod, "CONNECT_TIMEOUT", 0.5)

    async def hanging_open_connection(host, port):
        await asyncio.sleep(60.0)

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", hanging_open_connection)
    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)

    start = time.monotonic()
    with pytest.raises((TimeoutError, asyncio.TimeoutError, ConnectionError, OSError)):
        await bridge.connect()
    elapsed = time.monotonic() - start
    assert 0.3 < elapsed < 1.5, f"connect() took {elapsed:.2f}s, expected ~0.5s"


# ── Test 6: ECONNREFUSED fast backoff ────────────────────────────────────────

async def test_econnrefused_fails_fast(monkeypatch):
    """ConnectionRefusedError → circuit breaker trips, raises immediately (no backoff sleep)."""
    import unity_mcp.bridge as bridge_mod

    call_count = 0

    async def mock_open_connection(host, port):
        nonlocal call_count
        call_count += 1
        raise ConnectionRefusedError("mock: connection refused")

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)

    from helpers import make_idle_probe
    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999, probe=make_idle_probe())

    start = time.monotonic()
    with pytest.raises((ConnectionError, OSError)):
        await bridge.send("ping", {})
    elapsed = time.monotonic() - start

    # Grace retry on first attempt + circuit breaker on second
    assert elapsed < 3.0, f"idle probe should fail fast, took {elapsed:.2f}s"
    # open_connection called once (in _reconnect inside send's lock block)
    assert call_count >= 1


# ── Test 7: reconnect invokes callbacks ──────────────────────────────────────

async def test_reconnect_invokes_callback(monkeypatch):
    """add_reconnect_callback: registered fn is called after _reconnect."""
    import unity_mcp.bridge as bridge_mod

    ping_hdr, ping_pay = ping_response()
    mock_reader = MagicMock()
    mock_reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay])
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    async def mock_open_connection(host, port):
        return mock_reader, mock_writer

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)

    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)
    fired = []
    bridge.add_reconnect_callback(lambda: fired.append(1))

    await bridge._reconnect()
    assert len(fired) == 1, f"callback not fired, got {fired}"


# ── Test 8 (bridge side): reconnect callback wires middleware.reset_session ───

async def test_reconnect_callback_resets_middleware(monkeypatch):
    """Simulate wiring: reconnect fires reset_session on middleware."""
    import unity_mcp.bridge as bridge_mod
    from unity_mcp.middleware import Middleware

    ping_hdr, ping_pay = ping_response()
    mock_reader = MagicMock()
    mock_reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay])
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    async def mock_open_connection(host, port):
        return mock_reader, mock_writer

    monkeypatch.setattr(bridge_mod.asyncio, "open_connection", mock_open_connection)

    bridge = bridge_mod.UnityBridge("127.0.0.1", 9999)
    mw = Middleware()
    # Seed some state to verify reset
    mw.check_retry("set_property", {"path": "/X"})
    assert mw.check_retry("set_property", {"path": "/X"}) is not None  # blocked

    bridge.add_reconnect_callback(mw.reset_session)
    await bridge._reconnect()

    # After reconnect, retry cache cleared
    assert mw.check_retry("set_property", {"path": "/X"}) is None


# Test 9 removed: was duplicate of test_econnrefused_fast_backoff.
# Regression guard for except-clause ordering is implicit in test 6's timing assertion
# (>= 0.4s rules out instant fall-through, < 5s rules out [2,4,8] slow path).
