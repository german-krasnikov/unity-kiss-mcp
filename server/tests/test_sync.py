"""Tests for sync_unity tool: poll loop, epoch, reconnect, fail path.

Protocol:
  sync → "sync_ack|epoch=N|will_compile=bool"
  sync_status → "epoch=N|state=ready|compiling|failed|idle"
  get_compile_errors → "" or error text
  editor_log.corroborate() — both-signals gate (MAJOR-3)
"""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock

from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.sync as _sync
from unity_mcp.bridge import DomainReloadError


# ── Helpers ──────────────────────────────────────────────────────────────────

def _make_send(ack_response: str, status_seq, errors_response: str = ""):
    """Route sync / sync_status / get_compile_errors commands."""
    status_iter = iter(status_seq)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "sync":
            return ack_response
        if cmd == "sync_status":
            val = next(status_iter)
            if isinstance(val, Exception):
                raise val
            return val
        if cmd == "get_compile_errors":
            return errors_response
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _sync._send
    yield
    _sync._send = original


@pytest.fixture(autouse=True)
def _patch_corroborate():
    """Default: corroborate is a pass-through (no Unity running in unit tests)."""
    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.corroborate = lambda s: s  # pass-through by default
        mock_el.init_corroboration = MagicMock()
        yield mock_el


# ── Tests #21–#30 ─────────────────────────────────────────────────────────────

# #21: will_compile=false → fast path, no poll
@pytest.mark.asyncio
async def test_idempotent_noop_fast_path():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=false",
        status_seq=[],  # never called
    )
    result = await _sync.sync_unity()
    assert "sync clean" in result or "no compile needed" in result


# #22: both signals required — corroborate returns errors even when C# reports clean
@pytest.mark.asyncio
async def test_both_signals_required_for_clean(_patch_corroborate):
    """state=ready+epoch match, but editor_log.corroborate adds stale warning → not clean."""
    stale_warning = "[warn: UnityMCP.Editor.dll may be stale - consider recompiling]"
    # corroborate appends a stale warning to the (empty) C# response
    _patch_corroborate.corroborate = lambda s: s + "\n" + stale_warning if not s else s

    _sync._send = _make_send(
        "sync_ack|epoch=2|will_compile=true",
        status_seq=["epoch=2|state=ready"],
        errors_response="",  # C# reports clean
    )
    result = await _sync.sync_unity(timeout=60.0)
    # The stale warning from editor_log must surface through the both-signals gate
    assert stale_warning in result


# #23: epoch race — sync_status returns wrong epoch → keep polling
@pytest.mark.asyncio
async def test_epoch_race_no_premature_idle():
    _sync._send = _make_send(
        "sync_ack|epoch=3|will_compile=true",
        status_seq=[
            "epoch=2|state=ready",   # stale epoch — ignore
            "epoch=3|state=ready",   # correct epoch — accept
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# #24: reconnect after domain reload — DomainReloadError then success
@pytest.mark.asyncio
async def test_reconnect_after_domain_reload():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            DomainReloadError("going_away"),
            "epoch=1|state=ready",
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""


# #25: compile failed → return errors immediately, no reconnect wait
@pytest.mark.asyncio
async def test_compile_failed_no_reload_wait():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=failed|err=CS0103: bad"],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "CS0103" in result or "failed" in result


# #26: compile errors verbatim — state=ready+epoch match, get_compile_errors returns errors
@pytest.mark.asyncio
async def test_compile_errors_verbatim():
    err = "Assets/Bar.cs(5,3): error CS0246: type not found"
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
        errors_response=err,
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert err in result


# #27: stale dll surfaced via corroborate — editor_log.corroborate returns stale message
@pytest.mark.asyncio
async def test_stale_dll_blocks_false_clean(_patch_corroborate):
    """corroborate() returns a stale-dll message — this must surface in sync_unity result."""
    stale_msg = "[editor.log - dll stale]\nAssets/Foo.cs(1,1): error CS0001: stale"
    _patch_corroborate.corroborate = lambda s: stale_msg  # override with real stale response

    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=["epoch=1|state=ready"],
        errors_response="",  # C# says clean
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert stale_msg in result


# #28: timeout → return partial message
@pytest.mark.asyncio
async def test_timeout_returns_partial():
    # With sleep mocked and timeout=0, deadline is already past before first poll
    call_count = 0

    async def _stuck(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling"
        return ""

    _sync._send = _stuck
    result = await _sync.sync_unity(timeout=0.0)
    assert "timeout" in result.lower()


# #29: Unity dead — ConnectionError on all calls → fast error
@pytest.mark.asyncio
async def test_unity_dead_fails_fast():
    async def _dead(cmd, args=None, **kwargs):
        raise ConnectionError("Unity not running")

    _sync._send = _dead
    with pytest.raises((ConnectionError, ToolError)):
        await _sync.sync_unity(timeout=60.0)


# #30: no bridge → ToolError
@pytest.mark.asyncio
async def test_standalone_server_degrades():
    _sync._send = None
    with pytest.raises(ToolError):
        await _sync.sync_unity(timeout=60.0)


# #31: backgrounded editor — compile never starts (dur stays 0.0) → focus hint,
# not a blind 120s timeout (macOS/Unity 6 defers compilation while unfocused)
@pytest.mark.asyncio
async def test_focus_hint_when_compile_never_starts(monkeypatch):
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)

    async def _backgrounded(cmd, args=None, **kwargs):
        if cmd == "sync":
            return "sync_ack|epoch=1|will_compile=true"
        if cmd == "sync_status":
            return "epoch=1|state=compiling|dur=0.0"
        return ""

    _sync._send = _backgrounded
    result = await _sync.sync_unity(timeout=60.0)
    assert "backgrounded" in result.lower()
    assert "click" in result.lower()


# #32: real compile in progress (dur > 0) must NOT trigger the focus hint
@pytest.mark.asyncio
async def test_no_focus_hint_when_compile_running(monkeypatch):
    monkeypatch.setattr(_sync, "_FOCUS_HINT_AFTER", 0.0)
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            "epoch=1|state=compiling|dur=2.3",
            "epoch=1|state=ready",
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result
