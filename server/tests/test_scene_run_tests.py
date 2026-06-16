"""Tests for run_tests — immediate return (no inline polling)."""
import asyncio
import unity_mcp.tools.scene as scene_mod


async def test_run_tests_connection_error_returns_started(monkeypatch):
    """TCP dies → returns tests-started immediately, no polling."""
    async def fake_send(cmd, args={}, **kw):
        raise ConnectionError("going_away")

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "tests-started" in result
    assert "EditMode" in result


async def test_run_tests_timeout_returns_started(monkeypatch):
    """Timeout → returns tests-started immediately."""
    async def fake_send(cmd, args={}, **kw):
        raise asyncio.TimeoutError()

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("PlayMode")
    assert "tests-started" in result
    assert "PlayMode" in result


async def test_run_tests_full_result_returned_directly(monkeypatch):
    """Unity returns full result (no domain reload) → returned as-is."""
    async def fake_send(cmd, args={}, **kw):
        return "passed: 5 failed: 0"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "passed: 5" in result


async def test_run_tests_pending_returns_started(monkeypatch):
    """Unity returns 'pending' → treated as no result."""
    async def fake_send(cmd, args={}, **kw):
        return "pending"

    monkeypatch.setattr(scene_mod, "_send", fake_send)
    result = await scene_mod.run_tests("EditMode")
    assert "tests-started" in result
