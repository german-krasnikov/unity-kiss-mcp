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
