"""Tests for errors.py — structured error classification (Tier 4c/4d)."""
import asyncio

import pytest

from unity_mcp.errors import classify_failure, UnityError
from unity_mcp.bridge import DomainReloadError


# D5. IncompleteReadError + busy → reloading, transient
def test_classify_incomplete_read_busy():
    exc = asyncio.IncompleteReadError(b"", 4)
    ue = classify_failure(exc, probe_busy=True, remaining=8.0)
    assert ue.unity_state == "reloading"
    assert ue.is_transient is True


# D6. IncompleteReadError + idle → crashed, not transient
def test_classify_incomplete_read_idle():
    exc = asyncio.IncompleteReadError(b"", 4)
    ue = classify_failure(exc, probe_busy=False, remaining=0.0)
    assert ue.unity_state == "crashed"
    assert ue.is_transient is False


# D7. ConnectionRefusedError + busy → compiling, transient
def test_classify_connection_refused_busy():
    exc = ConnectionRefusedError("port closed")
    ue = classify_failure(exc, probe_busy=True, remaining=15.0)
    assert ue.unity_state == "compiling"
    assert ue.is_transient is True


# D8. ConnectionRefusedError + idle → disconnected, not transient
def test_classify_connection_refused_idle():
    exc = ConnectionRefusedError("port closed")
    ue = classify_failure(exc, probe_busy=False, remaining=0.0)
    assert ue.unity_state == "disconnected"
    assert ue.is_transient is False


# D9. TimeoutError + busy → frozen, transient
def test_classify_timeout_busy():
    exc = asyncio.TimeoutError()
    ue = classify_failure(exc, probe_busy=True, remaining=20.0)
    assert ue.unity_state == "frozen"
    assert ue.is_transient is True


# D10. TimeoutError + idle → frozen, not transient
def test_classify_timeout_idle():
    exc = asyncio.TimeoutError()
    ue = classify_failure(exc, probe_busy=False, remaining=0.0)
    assert ue.unity_state == "frozen"
    assert ue.is_transient is False


# D11. DomainReloadError → reloading, transient
def test_classify_domain_reload():
    exc = DomainReloadError("going_away")
    ue = classify_failure(exc, probe_busy=False, remaining=5.0)
    assert ue.unity_state == "reloading"
    assert ue.is_transient is True


# D12. classify_failure output formats correctly for _send_raw
def test_classify_produces_fields_used_by_send_raw():
    ue = classify_failure(ConnectionRefusedError("refused"), True, 10.0)
    fmt = (
        f"[UNITY_UNAVAILABLE] state={ue.unity_state} transient={ue.is_transient} "
        f"retry_after={ue.retry_after_seconds}s | {ue.message}"
    )
    assert "[UNITY_UNAVAILABLE]" in fmt
    assert "state=compiling" in fmt
    assert "transient=True" in fmt
    assert "retry_after=10s" in fmt
