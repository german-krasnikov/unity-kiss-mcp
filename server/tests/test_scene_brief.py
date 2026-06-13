"""Tests for SceneBrief — P2 feature."""
import os
import pytest
from unittest.mock import AsyncMock, patch


# ── Helpers ──────────────────────────────────────────────────────────────────

def _make_brief():
    from unity_mcp.scene_brief import SceneBrief
    return SceneBrief()


# ── Tests ────────────────────────────────────────────────────────────────────

def test_scene_brief_disabled_by_default():
    brief = _make_brief()
    assert not brief.enabled


def test_scene_brief_enabled_by_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_SCENE_BRIEF", "1")
    brief = _make_brief()
    assert brief.enabled


def test_scene_brief_should_inject_first_call():
    brief = _make_brief()
    brief.brief = "scene has 5 objects"
    assert brief.should_inject("get_hierarchy")


def test_scene_brief_not_inject_second_call():
    brief = _make_brief()
    brief.brief = "scene has 5 objects"
    brief.mark_injected()
    assert not brief.should_inject("get_hierarchy")


def test_scene_brief_not_inject_meta_commands():
    brief = _make_brief()
    brief.brief = "scene has 5 objects"
    assert not brief.should_inject("list_connections")
    assert not brief.should_inject("discover_tools")


def test_scene_brief_not_inject_when_no_brief():
    brief = _make_brief()
    assert not brief.should_inject("get_hierarchy")


def test_scene_brief_reset_clears_cache():
    brief = _make_brief()
    brief.brief = "scene has 5 objects"
    brief.mark_injected()
    brief.reset()
    assert brief.brief is None
    assert brief.should_inject("get_hierarchy") is False  # no brief to inject


@pytest.mark.asyncio
async def test_scene_brief_ensure_calls_send_raw(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_SCENE_BRIEF", "1")
    brief = _make_brief()
    call_log = []

    async def fake_send(cmd, args, **kw):
        call_log.append(cmd)
        return f"data:{cmd}"

    with patch("unity_mcp.scene_brief.SamplingService") as MockSvc:
        instance = MockSvc.return_value
        instance.enabled = True
        instance.summarize = AsyncMock(return_value="5 objects, no errors, stopped")
        result = await brief.ensure(fake_send)

    assert "get_hierarchy" in call_log
    assert "get_console" in call_log
    assert "editor" in call_log
    assert result == "5 objects, no errors, stopped"
    assert brief.brief == "5 objects, no errors, stopped"


@pytest.mark.asyncio
async def test_scene_brief_ensure_caches_result(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_SCENE_BRIEF", "1")
    brief = _make_brief()
    brief.brief = "cached"

    call_log = []

    async def fake_send(cmd, args, **kw):
        call_log.append(cmd)
        return "data"

    result = await brief.ensure(fake_send)
    assert result == "cached"
    assert not call_log  # no calls made


@pytest.mark.asyncio
async def test_scene_brief_ensure_returns_none_when_disabled():
    brief = _make_brief()  # UNITY_MCP_SCENE_BRIEF not set

    async def fake_send(cmd, args, **kw):
        return "data"

    result = await brief.ensure(fake_send)
    assert result is None


@pytest.mark.asyncio
async def test_scene_brief_ensure_send_raw_raises(monkeypatch):
    """ensure() must return None (not propagate) when send_raw raises."""
    monkeypatch.setenv("UNITY_MCP_SCENE_BRIEF", "1")
    brief = _make_brief()

    async def failing_send(cmd, args, **kw):
        raise RuntimeError("TCP down")

    result = await brief.ensure(failing_send)
    assert result is None


@pytest.mark.asyncio
async def test_scene_brief_ensure_sampling_disabled(monkeypatch):
    """ensure() returns None when SamplingService is not enabled."""
    monkeypatch.setenv("UNITY_MCP_SCENE_BRIEF", "1")
    brief = _make_brief()

    async def fake_send(cmd, args, **kw):
        return "data"

    with patch("unity_mcp.scene_brief.SamplingService") as MockSvc:
        instance = MockSvc.return_value
        instance.enabled = False
        result = await brief.ensure(fake_send)

    assert result is None
