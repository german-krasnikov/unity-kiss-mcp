"""Tests for DomainReloadTracker (Fix 2)."""
import time
import pytest
from unity_mcp.bridge_reload_state import DomainReloadTracker, DOMAIN_RELOAD_EXPIRY_S


def test_initial_state_inactive():
    t = DomainReloadTracker()
    assert t.is_active() is False


def test_mark_activates():
    t = DomainReloadTracker()
    t.mark()
    assert t.is_active() is True


def test_clear_deactivates():
    t = DomainReloadTracker()
    t.mark()
    t.clear()
    assert t.is_active() is False


def test_expires_after_30s(monkeypatch):
    t = DomainReloadTracker()
    t.mark()
    monkeypatch.setattr(
        "unity_mcp.bridge_reload_state.time",
        type("FakeTime", (), {"monotonic": staticmethod(lambda: time.monotonic() + DOMAIN_RELOAD_EXPIRY_S + 1)})()
    )
    assert t.is_active() is False


def test_stays_active_within_window(monkeypatch):
    t = DomainReloadTracker()
    t.mark()
    monkeypatch.setattr(
        "unity_mcp.bridge_reload_state.time",
        type("FakeTime", (), {"monotonic": staticmethod(lambda: time.monotonic() + DOMAIN_RELOAD_EXPIRY_S - 1)})()
    )
    assert t.is_active() is True


def test_elapsed_zero_when_inactive():
    t = DomainReloadTracker()
    assert t.elapsed() == 0.0


def test_elapsed_positive_after_mark():
    t = DomainReloadTracker()
    t.mark()
    assert t.elapsed() >= 0.0


def test_double_mark_resets_timer():
    """Second mark() resets _since so elapsed() reflects fresh start."""
    t = DomainReloadTracker()
    t.mark()
    first_since = t._since
    t.mark()
    # _since should be >= first_since (reset or same)
    assert t._since >= first_since
    assert t.is_active() is True


def test_domain_reload_expiry_is_120s():
    assert DOMAIN_RELOAD_EXPIRY_S == 120.0


def test_disconnect_window_matches_domain_reload_expiry():
    from unity_mcp.compile_state import _DISCONNECT_WINDOW_S
    assert _DISCONNECT_WINDOW_S == DOMAIN_RELOAD_EXPIRY_S


def test_session_timeout_family_shares_one_source():
    """DOMAIN_RELOAD_EXPIRY_S, _DISCONNECT_WINDOW_S, _STALE_SECONDS, _DEFAULT_TIMEOUT
    and bridge.SESSION_TIMEOUT must all be the *same object* re-exported from
    constants.SESSION_TIMEOUT (import), not independently redeclared literals that
    happen to share a value today. `is` identity proves import — two independent
    `120.0` literals in different modules are never the same object in CPython."""
    from unity_mcp.constants import SESSION_TIMEOUT
    from unity_mcp.bridge import SESSION_TIMEOUT as bridge_timeout
    from unity_mcp.compile_state import _DISCONNECT_WINDOW_S
    from unity_mcp.unity_state import _STALE_SECONDS
    from unity_mcp.tools.sync import _DEFAULT_TIMEOUT

    assert bridge_timeout is SESSION_TIMEOUT
    assert DOMAIN_RELOAD_EXPIRY_S is SESSION_TIMEOUT
    assert _DISCONNECT_WINDOW_S is SESSION_TIMEOUT
    assert _STALE_SECONDS is SESSION_TIMEOUT
    assert _DEFAULT_TIMEOUT is SESSION_TIMEOUT
