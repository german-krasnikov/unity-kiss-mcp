"""Cycle 7a: sticky-cache retry tests for Middleware.

RED phase — all tests expected to fail before implementation.
"""
import time
import pytest
from unity_mcp.middleware import Middleware, READ_CMDS


@pytest.fixture
def mw():
    return Middleware()


# ── Test 1: sentinel ─────────────────────────────────────────────────────────

def test_retry_cache_expires_after_ttl(mw):
    """Block within TTL, pass after TTL expires."""
    mw._RETRY_TTL = 0.1
    # First call — allowed
    assert mw.check_retry("set_property", {"path": "/A", "prop": "x"}) is None
    # Second call within TTL — blocked
    r2 = mw.check_retry("set_property", {"path": "/A", "prop": "x"})
    assert r2 is not None and "RETRY" in r2
    # Wait for TTL to expire
    time.sleep(0.15)
    # Third call after TTL — allowed
    assert mw.check_retry("set_property", {"path": "/A", "prop": "x"}) is None


# ── Test 2: read commands never blocked ───────────────────────────────────────

def test_read_commands_never_blocked(mw):
    """READ cmds always return None regardless of repeat count."""
    for cmd in ("get_hierarchy", "get_console", "get_compile_errors",
                "validate_references", "screenshot"):
        for _ in range(20):
            assert mw.check_retry(cmd, {"path": "/same"}) is None


# ── Test 3: reset_session clears state ────────────────────────────────────────

def test_reset_session_clears_state(mw):
    """After reset_session, previously blocked call passes."""
    mw.check_retry("set_property", {"path": "/X"})
    blocked = mw.check_retry("set_property", {"path": "/X"})
    assert blocked is not None  # confirm it was blocked
    mw.reset_session()
    assert mw.check_retry("set_property", {"path": "/X"}) is None


# ── Test 8: same as 3 but phrased per spec ────────────────────────────────────

def test_check_retry_does_not_block_after_reset(mw):
    """check_retry: call A, call A (blocked), reset_session, call A → passes."""
    args = {"path": "/Foo", "component": "Transform", "prop": "x", "value": "1"}
    assert mw.check_retry("set_property", args) is None
    assert mw.check_retry("set_property", args) is not None  # blocked
    mw.reset_session()
    assert mw.check_retry("set_property", args) is None  # passes after reset


# ── Test: RETRY_CACHE max size eviction ──────────────────────────────────────

def test_retry_cache_max_size_eviction(mw):
    """Cache should not grow beyond _RETRY_MAX entries."""
    for i in range(mw._RETRY_MAX + 10):
        mw.check_retry("set_property", {"path": f"/Obj{i}"})
    assert len(mw._retry_cache) <= mw._RETRY_MAX


# ── Test: write commands ARE blocked on repeat ────────────────────────────────

def test_write_commands_blocked_on_repeat(mw):
    """Write commands (not in READ_CMDS) block on identical repeat."""
    mw._RETRY_TTL = 999.0  # keep TTL long so it doesn't expire
    mw.check_retry("create_object", {"name": "Cube"})
    result = mw.check_retry("create_object", {"name": "Cube"})
    assert result is not None and "RETRY" in result


# ── Test: _hashes dead field removed ─────────────────────────────────────────

def test_no_hashes_field(mw):
    """_hashes was write-only dead code — must no longer exist."""
    assert not hasattr(mw, "_hashes")


# ── Test: get_console and others from extended set never blocked ──────────────

def test_extended_read_cmds_in_READ_CMDS_set():
    """Verify spec-required cmds are in READ_CMDS."""
    required = {"get_console", "get_compile_errors", "validate_references",
                "screenshot"}
    missing = required - READ_CMDS
    assert not missing, f"Missing from READ_CMDS: {missing}"


# ── Test: reset_session clears both response_hashes and last_writes ───────────

def test_reset_session_clears_response_hashes_and_last_writes(mw):
    """reset_session() must clear both _response_hashes and _last_writes."""
    # populate _response_hashes via check_duplicate_response
    mw._response_hashes.append("abc123")
    # populate _last_writes via check_duplicate_write
    mw._last_writes["k"] = "v"

    mw.reset_session()

    assert len(mw._response_hashes) == 0
    assert len(mw._last_writes) == 0
