"""Tests for register() in tool modules — Pattern B audit.

Guards against silent argument-order breakage. Each test:
  1. Calls register(mcp, send, args)
  2. Confirms _send and _args module globals are set
  3. Confirms mcp.tool was called (tools actually wired)
"""
import pytest
from unittest.mock import AsyncMock, MagicMock
import importlib


@pytest.fixture(autouse=True)
def _restore_tool_globals():
    """Restore module-level _send/_args after each test to avoid cross-test pollution.

    scene.register() also calls scene_session.register(), so both must be restored.
    """
    import unity_mcp.tools.objects as obj_mod
    import unity_mcp.tools.runtime as rt_mod
    import unity_mcp.tools.scene as sc_mod
    import unity_mcp.tools.scene_session as ss_mod
    saved = {
        "obj": (obj_mod._send, obj_mod._args),
        "rt": (rt_mod._send, rt_mod._args),
        "sc": (sc_mod._send, sc_mod._args),
        "ss": (ss_mod._send, ss_mod._args),
    }
    yield
    obj_mod._send, obj_mod._args = saved["obj"]
    rt_mod._send, rt_mod._args = saved["rt"]
    sc_mod._send, sc_mod._args = saved["sc"]
    ss_mod._send, ss_mod._args = saved["ss"]


def _make_mcp():
    """Minimal mcp stub: mcp.tool(annotations=X)(fn) must not raise."""
    mcp = MagicMock()
    # mcp.tool(annotations=...) returns a decorator that accepts any callable
    mcp.tool.return_value = lambda fn: fn
    return mcp


# ── Part 1: objects.py ────────────────────────────────────────────────────────

def test_objects_register_sets_send():
    import unity_mcp.tools.objects as mod
    # Reset state so test is isolated
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_objects_register_wires_tools():
    import unity_mcp.tools.objects as mod
    send = AsyncMock()
    mcp = _make_mcp()

    mod.register(mcp, send, MagicMock())

    # At least get_component and set_property must be registered
    assert mcp.tool.call_count >= 8


def test_objects_register_arg_order_guard():
    """Swapping send/args positions would leave _send pointing at args callable."""
    import unity_mcp.tools.objects as mod
    mod._send = None

    sentinel_send = AsyncMock(name="the_send")
    mod.register(_make_mcp(), sentinel_send, MagicMock())

    assert mod._send is sentinel_send


# ── Part 2: scene.py ──────────────────────────────────────────────────────────

def test_scene_register_sets_send(monkeypatch):
    import unity_mcp.tools.scene as mod
    # register() calls editor_log.init_corroboration — stub it
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)

    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_scene_register_wires_tools(monkeypatch):
    import unity_mcp.tools.scene as mod
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)

    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # get_hierarchy, get_console, get_compile_errors, screenshot, recompile,
    # run_tests, get_test_results, scene, search_scene, editor = 10 tools
    assert mcp.tool.call_count >= 10


def test_scene_register_arg_order_guard(monkeypatch):
    import unity_mcp.tools.scene as mod
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)
    mod._send = None

    sentinel = AsyncMock(name="scene_send")
    mod.register(_make_mcp(), sentinel, MagicMock())

    assert mod._send is sentinel


# ── Part 3: runtime.py ────────────────────────────────────────────────────────

def test_runtime_register_sets_send():
    import unity_mcp.tools.runtime as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_runtime_register_wires_tools():
    import unity_mcp.tools.runtime as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # invoke_method, set_runtime_property, wait_until, move_to,
    # query_state, test_step, run_playtest, fuzz_playtest = 8 tools
    assert mcp.tool.call_count >= 8


def test_runtime_register_arg_order_guard():
    import unity_mcp.tools.runtime as mod
    mod._send = None

    sentinel = AsyncMock(name="runtime_send")
    mod.register(_make_mcp(), sentinel, MagicMock())

    assert mod._send is sentinel
