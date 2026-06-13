"""Lifespan invariant: no task/resource leak after teardown.

Regression: watchdog.cancel() in finally was memorial-fixed. This pins the contract.
"""
import asyncio

import pytest


def _import_lifespan():
    try:
        from unity_mcp.server import lifespan
        return lifespan
    except ImportError:
        pytest.skip("lifespan not importable — skipping (TODO: adjust path)")


@pytest.mark.asyncio
async def test_lifespan_cancels_watchdog(monkeypatch):
    """Regression: lifespan finally MUST call watchdog.cancel().

    Memorial fix in Tier 1 added watchdog.cancel() to lifespan finally block.
    This test fails if that call is removed.
    """
    lifespan = _import_lifespan()
    monkeypatch.setenv("UNITY_MCP_WATCHDOG", "1")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")

    from unity_mcp import server as srv

    cancel_called = []

    class FakeWatchdog:
        def __init__(self, *a, **kw):
            pass

        async def cancel(self):
            cancel_called.append(True)

        def maybe_trigger(self, *a, **kw):
            pass

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity (fake)"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda **_: FakeSlot())
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)
    # ProactiveWatchdog is locally imported inside lifespan() from .watchdog
    monkeypatch.setattr("unity_mcp.watchdog.ProactiveWatchdog", FakeWatchdog)

    class FakeApp: pass
    async with lifespan(FakeApp()):
        await asyncio.sleep(0.01)

    assert cancel_called, "watchdog.cancel() was not called in lifespan finally — regression!"


@pytest.mark.asyncio
async def test_lifespan_no_leak_when_features_disabled(monkeypatch):
    lifespan = _import_lifespan()
    for k in ("UNITY_MCP_WATCHDOG", "UNITY_MCP_HINTS", "UNITY_MCP_MIDDLEWARE",
              "UNITY_MCP_LESSONS", "UNITY_MCP_INFERENCE", "UNITY_MCP_SPECULATION",
              "UNITY_MCP_SCENE_BRIEF"):
        monkeypatch.delenv(k, raising=False)
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")

    from unity_mcp import server as srv

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity (fake)"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda **_: FakeSlot())
    # Restore server globals after test — lifespan writes them
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)

    tasks_before = {t for t in asyncio.all_tasks() if not t.done()}

    class FakeApp: pass
    async with lifespan(FakeApp()):
        pass

    await asyncio.sleep(0.01)
    tasks_after = {t for t in asyncio.all_tasks() if not t.done()}
    leaked = tasks_after - tasks_before - {asyncio.current_task()}
    assert not leaked, f"Baseline leak: {[t.get_name() for t in leaked]}"


# ---------------------------------------------------------------------------
# X4.cross.1: UNITY_MCP_MIDDLEWARE=1 alone does not enable visual-verify;
# both UNITY_MCP_MIDDLEWARE=1 AND UNITY_MCP_VISUAL_VERIFY=1 are required.
# ---------------------------------------------------------------------------

def test_sampling_service_enabled_requires_visual_verify(monkeypatch):
    """SamplingService.enabled is False with only MIDDLEWARE=1 set."""
    monkeypatch.setenv("UNITY_MCP_MIDDLEWARE", "1")
    monkeypatch.delenv("UNITY_MCP_VISUAL_VERIFY", raising=False)
    from unity_mcp.sampling import SamplingService
    svc = SamplingService()
    assert not svc.enabled, "MIDDLEWARE alone must not activate visual-verify"


def test_sampling_service_enabled_with_both_envs(monkeypatch):
    """SamplingService.enabled is True when both MIDDLEWARE=1 and VISUAL_VERIFY=1 are set."""
    monkeypatch.setenv("UNITY_MCP_MIDDLEWARE", "1")
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService
    svc = SamplingService()
    assert svc.enabled, "MIDDLEWARE + VISUAL_VERIFY must enable visual-verify"
