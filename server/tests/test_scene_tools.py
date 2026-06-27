"""Unit tests for scene.py tool functions — keyword filter and undo_last."""
import pytest
from unittest.mock import AsyncMock, MagicMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.scene as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def scene_mod():
    import unity_mcp.tools.scene as mod
    return mod


# ── T4: get_console keyword / count_only ─────────────────────────────────────

@pytest.mark.asyncio
async def test_get_console_keyword_passed_to_send(scene_mod, _patch_send):
    await scene_mod.get_console(count=5, keyword="NullRef")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_console"
    assert call_args[0][1].get("keyword") == "NullRef"


@pytest.mark.asyncio
async def test_get_console_count_only_passed_to_send(scene_mod, _patch_send):
    await scene_mod.get_console(count=20, count_only=True)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("count_only") == "true"


@pytest.mark.asyncio
async def test_get_console_count_only_false_omitted(scene_mod, _patch_send):
    """count_only=False should not appear in args (filtered by _args)."""
    await scene_mod.get_console(count=5, count_only=False)

    call_args = _patch_send.call_args
    assert "count_only" not in call_args[0][1]


@pytest.mark.asyncio
async def test_get_console_keyword_none_omitted(scene_mod, _patch_send):
    await scene_mod.get_console(count=5)

    call_args = _patch_send.call_args
    assert "keyword" not in call_args[0][1]


# ── T5: undo_last ─────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_undo_last_sends_correct_command(scene_mod, _patch_send):
    await scene_mod.undo_last()

    call_args = _patch_send.call_args
    assert call_args[0][0] == "undo_last"
    assert call_args[0][1].get("turns") == 1


@pytest.mark.asyncio
async def test_undo_last_passes_turns_param(scene_mod, _patch_send):
    await scene_mod.undo_last(turns=3)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("turns") == 3
