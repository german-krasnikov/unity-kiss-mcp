"""Phase 2 tests — bridge compile-state integration (tests #15-35).

Updated for simplified architecture (Cycle 15): circuit breaker removed,
heartbeat is the reconnect mechanism. Tests focus on probe integration,
metrics, and error messages.
"""
import asyncio
import json
import struct
import time
from typing import Optional
from unittest.mock import AsyncMock, Mock, patch, MagicMock

import pytest

import unity_mcp.bridge as _bridge_mod
from unity_mcp.bridge import UnityBridge
from unity_mcp.compile_state import CompileStateProbe
from unity_mcp.metrics import METRICS
from helpers import make_writer, ping_response

_ORIG_CONNECT = _bridge_mod.CONNECT_TIMEOUT
_ORIG_SESSION = _bridge_mod.SESSION_TIMEOUT


@pytest.fixture(autouse=True)
def _fast_timeouts():
    _bridge_mod.CONNECT_TIMEOUT = 0.01
    _bridge_mod.SESSION_TIMEOUT = 0.3
    yield
    _bridge_mod.CONNECT_TIMEOUT = _ORIG_CONNECT
    _bridge_mod.SESSION_TIMEOUT = _ORIG_SESSION


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_ok_response(msg_id="0001"):
    r = {"id": msg_id, "ok": True, "data": "x"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def _make_probe(busy: bool = False, remaining: float = 5.0,
                 strong_busy: Optional[bool] = None, has_project: bool = True):
    probe = Mock(spec=CompileStateProbe)
    probe.is_unity_busy.return_value = busy
    probe.estimated_remaining_s.return_value = remaining
    probe.has_strong_busy_signal.return_value = (
        strong_busy if strong_busy is not None else busy
    )
    probe.has_project = has_project
    probe.mark_recompile_issued = Mock()
    probe.is_process_dead = Mock(return_value=False)
    return probe


def _make_failing_connection():
    reader = AsyncMock()
    writer = make_writer()
    writer.write = Mock(side_effect=ConnectionError("Connection lost"))
    return reader, writer


# ---------------------------------------------------------------------------
# 15. send() raises ConnectionError when connection fails
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_send_raises_on_connection_failure():
    probe = _make_probe(busy=False)
    r, w = _make_failing_connection()

    with patch("unity_mcp.bridge.asyncio.open_connection", return_value=(r, w)):
        with patch("unity_mcp.bridge.asyncio.sleep", new_callable=AsyncMock):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, TimeoutError)):
                await bridge.send("test", {})


# ---------------------------------------------------------------------------
# 20. Error message when compiling
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_bridge_error_message_compiling():
    async def open_conn(*a, **kw):
        r, w = _make_failing_connection()
        return r, w

    probe = _make_probe(busy=True, remaining=0.1)

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_conn):
        bridge = UnityBridge(probe=probe)
        await bridge.connect()
        with pytest.raises((TimeoutError, ConnectionError)) as exc_info:
            await bridge.send("test", {})

    msg = str(exc_info.value)
    assert "C# compilation" in msg or "Session deadline" in msg


# ---------------------------------------------------------------------------
# 21. Error message when process dead
# ---------------------------------------------------------------------------

def test_bridge_error_message_dead():
    """When PID is dead, _describe_failure says 'process dead' and includes port."""
    probe = _make_probe(busy=False)
    probe.is_process_dead.return_value = True

    bridge = UnityBridge(port=9500, probe=probe)
    msg = bridge._describe_failure("ping", ConnectionError("timeout"))
    assert "process dead" in msg
    assert ":9500" in msg


# ---------------------------------------------------------------------------
# 22. No misleading "save dialog" in any error message
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_bridge_error_no_misleading_save_dialog():
    async def open_conn(*a, **kw):
        r, w = _make_failing_connection()
        return r, w

    for busy in (True, False):
        probe = _make_probe(busy=busy)
        with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=open_conn):
            bridge = UnityBridge(probe=probe)
            await bridge.connect()
            with pytest.raises((ConnectionError, TimeoutError)) as exc_info:
                await bridge.send("test", {})
        assert "save dialog" not in str(exc_info.value)


# ---------------------------------------------------------------------------
# 24. recompile.duration_ms observed on fail-then-succeed
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_metrics_recompile_duration_observed():
    probe = _make_probe(busy=False)

    hdr, payload = _make_ok_response()
    new_reader = AsyncMock()
    new_reader.readexactly.side_effect = [hdr, payload]
    new_writer = make_writer()

    bridge = UnityBridge(probe=probe)
    bridge._reader = new_reader
    bridge._writer = new_writer
    # Simulate previous failure
    bridge._first_failure_ts = time.monotonic() - 0.1

    result = await bridge.send("test", {})
    assert result["ok"] is True

    assert len(METRICS._observations.get("recompile.duration_ms", [])) >= 1


# ---------------------------------------------------------------------------
# 25. Probe injected / constructed
# ---------------------------------------------------------------------------

def test_bridge_default_probe_constructed_lazily(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    bridge = UnityBridge()
    assert isinstance(bridge._probe, CompileStateProbe)


def test_bridge_accepts_injected_probe():
    sentinel = object()
    bridge = UnityBridge(probe=sentinel)
    assert bridge._probe is sentinel


def test_bridge_metrics_isolated_per_test():
    METRICS.inc("recompile.detected")
    assert METRICS._counters["recompile.detected"] == 1


# ---------------------------------------------------------------------------
# 26. _reconnect resets first_failure_ts and fires callbacks
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_reconnect_resets_failure_ts():
    mock_reader = MagicMock()
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.drain = AsyncMock()

    ping_hdr, ping_pay = ping_response()
    mock_reader.readexactly = AsyncMock(side_effect=[ping_hdr, ping_pay])

    async def mock_open(host, port):
        return mock_reader, mock_writer

    with patch("unity_mcp.bridge.asyncio.open_connection", side_effect=mock_open):
        bridge = _bridge_mod.UnityBridge("127.0.0.1", 9999)
        bridge._first_failure_ts = time.monotonic() - 1.0
        await bridge._reconnect()
        assert bridge._first_failure_ts is None


# ---------------------------------------------------------------------------
# A3. Compilation gate: _send_raw passes through to bridge when probe busy
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_compilation_gate_passes_through_when_busy():
    """_send_raw passes through to bridge even when probe says busy — bridge handles retries."""
    from unity_mcp.server import _send_raw
    import unity_mcp.server as server_mod

    probe = _make_probe(busy=True, strong_busy=True, remaining=12.0)

    class FakeBridge:
        connected = True
        _probe = probe
        send_called = False

        async def send(self, cmd, args, timeout=30.0):
            self.send_called = True
            return {"ok": True, "data": "x"}

    fake_bridge = FakeBridge()

    class FakeSlot:
        bridge = fake_bridge
        def get(self, name): return None
        def list(self): return {}

    old_slot = server_mod.slot
    server_mod.slot = FakeSlot()
    try:
        result = await _send_raw("get_hierarchy", {})
        assert result == "x"
    finally:
        server_mod.slot = old_slot

    assert fake_bridge.send_called, "bridge.send should be called — no pre-check block"
