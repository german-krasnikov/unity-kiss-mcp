"""Tests for await_compile: polls compile_status until idle, handles disconnects.

compile_status: "compiling|<s>" | "idle|<s>" | "idle|0"
Returns: "compile clean (Xs)" | errors string | "timeout after Xs..." on timeout
"""
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError

import unity_mcp.tools.code_intel as _ci
from unity_mcp.bridge import DomainReloadError


def _make_send(status_seq, errors_response=""):
    """Route compile_status / get_compile_errors; errors_response may be an Exception.
    sync_status raises ConnectionError (simulate not-available) to trigger compile_status fallback."""
    status_iter = iter(status_seq)

    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            return next(status_iter)
        if cmd == "get_compile_errors":
            if isinstance(errors_response, Exception):
                raise errors_response
            return errors_response
        if cmd == "sync_status":
            # Simulate sync_status unavailable → triggers compile_status fallback in await_compile
            raise ConnectionError("Command not registered: sync_status")
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _ci._send
    yield
    _ci._send = original


@pytest.mark.asyncio
async def test_already_idle_returns_errors_immediately():
    errors = "Assets/Scripts/Foo.cs(12,5): error CS0103: 'bar' does not exist"
    _ci._send = _make_send(["idle|8.2"], errors_response=errors)
    result = await _ci.await_compile(timeout=60.0)
    assert errors in result


@pytest.mark.asyncio
async def test_already_idle_clean_returns_clean_message():
    _ci._send = _make_send(["idle|5.2"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (5.2s)"


@pytest.mark.asyncio
async def test_compiling_then_idle_polls_until_done():
    _ci._send = _make_send(["compiling|1.0", "compiling|2.0", "idle|3.1"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (3.1s)"


@pytest.mark.asyncio
async def test_domain_reload_disconnect_waits_and_retries():
    """ConnectionError during compile_status → retry loop."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count == 1:
                raise ConnectionError("disconnected")
            return "idle|4.0"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (4.0s)"
    assert call_count == 2


@pytest.mark.asyncio
async def test_domain_reload_error_waits_and_retries():
    """DomainReloadError (is-a ConnectionError) → retry loop."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count == 1:
                raise DomainReloadError("going_away")
            return "idle|6.5"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (6.5s)"


@pytest.mark.asyncio
async def test_timeout_returns_best_effort():
    """Timeout while compiling → timeout message + best-effort errors."""
    async def _send(cmd, args=None, **kwargs):
        if cmd == "compile_status":
            return "compiling|1.0"
        return "CS0001: Something broke"

    _ci._send = _send
    result = await _ci.await_compile(timeout=0.001)
    assert "timeout after" in result
    assert "compile still in progress" in result
    assert "CS0001" in result


@pytest.mark.asyncio
async def test_multiple_disconnects_within_timeout():
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        if cmd == "compile_status":
            call_count += 1
            if call_count < 3:
                raise ConnectionError("still reloading")
            return "idle|2.0"
        return ""

    _ci._send = _send
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (2.0s)"
    assert call_count == 3


@pytest.mark.asyncio
async def test_timeout_zero_idle_returns_errors():
    """timeout=0, status idle → fetches errors (exactly 2 calls: status + errors)."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        if cmd == "compile_status":
            return "idle|3.0"
        return "some error"

    _ci._send = _send
    result = await _ci.await_compile(timeout=0)
    assert call_count == 2
    assert "some error" in result


@pytest.mark.asyncio
async def test_timeout_zero_compiling_returns_still_compiling():
    """timeout=0, status compiling → 'still compiling', no errors call."""
    call_count = 0

    async def _send(cmd, args=None, **kwargs):
        nonlocal call_count
        call_count += 1
        return "compiling|3.0"

    _ci._send = _send
    result = await _ci.await_compile(timeout=0)
    assert result == "still compiling"
    assert call_count == 1


def test_registered_as_readonly_tool():
    from unity_mcp.tools.gating import TIER1
    assert "await_compile" in TIER1


@pytest.mark.asyncio
async def test_malformed_status_treated_as_idle():
    """Status not matching 'state|number' → treated as idle."""
    _ci._send = _make_send(["unexpected_garbage_response"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result


@pytest.mark.asyncio
async def test_get_errors_connection_failure_returns_clean():
    """compile_status idle, get_compile_errors raises ConnectionError → 'compile clean'."""
    _ci._send = _make_send(["idle|2.0"], errors_response=ConnectionError("tcp gone"))
    result = await _ci.await_compile(timeout=60.0)
    assert "compile clean" in result


@pytest.mark.asyncio
async def test_get_errors_tool_error_propagates():
    """compile_status idle, get_compile_errors raises ToolError → must propagate."""
    _ci._send = _make_send(["idle|2.0"], errors_response=ToolError("malformed response"))
    with pytest.raises(ToolError):
        await _ci.await_compile(timeout=60.0)


# #34: await_compile uses sync_status epoch-aware wait when available (MAJOR-1 spec)
@pytest.mark.asyncio
async def test_await_compile_uses_sync_status_epoch():
    """When sync_status available and returns compiling epoch, await_compile waits for that epoch."""
    call_log = []

    async def _epoch_send(cmd, args=None, **kwargs):
        call_log.append(cmd)
        if cmd == "sync_status":
            # First call: epoch=5, state=compiling → triggers epoch-aware path
            # Subsequent calls: epoch=5, state=ready → done
            if call_log.count("sync_status") == 1:
                return "epoch=5|state=compiling|dur=1.2"
            return "epoch=5|state=ready"
        if cmd == "get_compile_errors":
            return ""
        raise AssertionError(f"Unexpected: {cmd}")

    _ci._send = _epoch_send
    result = await _ci.await_compile(timeout=60.0)
    # Must have used sync_status (epoch-aware path) and returned clean
    assert "sync_status" in call_log, "await_compile must call sync_status for epoch-aware wait"
    assert "compile clean" in result


# Fallback: sync_status unavailable → compile_status fallback still works
@pytest.mark.asyncio
async def test_await_compile_falls_back_to_compile_status_when_no_sync_status():
    """When sync_status is not available, await_compile still works via compile_status fallback."""
    # compile_status returns idle → await_compile must complete cleanly
    _ci._send = _make_send(["idle|3.0"], errors_response="")
    result = await _ci.await_compile(timeout=60.0)
    assert result == "compile clean (3.0s)"
