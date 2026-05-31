"""Tests for batch conflict detection in middleware."""
import pytest
from unity_mcp.middleware import Middleware


@pytest.fixture
def mw():
    return Middleware()


# ─── scan_batch_conflicts ─────────────────────────────────────────────────────

def test_no_conflict_clean_batch(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/B component=T prop=x value=2"
    assert mw.scan_batch_conflicts(commands) is None


def test_duplicate_write_detected(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/A component=T prop=x value=2"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "duplicate write" in warn.lower()
    assert "x" in warn


def test_create_then_delete_detected(mw):
    commands = "create_object name=Foo\ndelete_object path=/Foo"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "no-op" in warn.lower() and "create+delete" in warn.lower(), warn


def test_delete_then_reference_detected(mw):
    commands = "delete_object path=/Enemy\nset_property path=/Enemy component=T prop=x value=1"
    warn = mw.scan_batch_conflicts(commands)
    assert warn is not None
    assert "deleted" in warn.lower() and "referencing" in warn.lower(), warn


def test_empty_commands_no_warn(mw):
    assert mw.scan_batch_conflicts("") is None


def test_single_command_no_warn(mw):
    assert mw.scan_batch_conflicts("set_property path=/A component=T prop=x value=1") is None


def test_duplicate_different_props_no_warn(mw):
    commands = "set_property path=/A component=T prop=x value=1\nset_property path=/A component=T prop=y value=2"
    assert mw.scan_batch_conflicts(commands) is None
