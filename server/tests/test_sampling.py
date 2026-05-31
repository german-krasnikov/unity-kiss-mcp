"""Tests for MCP sampling tools: auto_fix and smart_build."""
import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from unity_mcp.tools.codegen import auto_fix, smart_build


def _make_ctx(sampling_result=None, sampling_error=None):
    """Build a mock Context with create_message behavior."""
    ctx = MagicMock()
    if sampling_error:
        ctx.session.create_message = AsyncMock(side_effect=sampling_error)
    else:
        msg = MagicMock()
        msg.content = [MagicMock(text=sampling_result or "Fix the code")]
        ctx.session.create_message = AsyncMock(return_value=msg)
    return ctx


@pytest.mark.asyncio
async def test_auto_fix_no_errors(mock_bridge):
    mock_bridge.send.side_effect = [
        {"ok": True, "data": ""},                         # get_console
        {"ok": True, "data": "No compilation errors"},    # get_compile_errors
    ]
    ctx = _make_ctx()
    result = await auto_fix(ctx)
    assert "No errors" in result


@pytest.mark.asyncio
async def test_auto_fix_with_errors_no_sampling(mock_bridge):
    mock_bridge.send.side_effect = [
        {"ok": True, "data": "[Error] NullRef in Player.cs:42"},  # get_console
        {"ok": True, "data": "Assets/Player.cs(5,3): error CS0001"},  # get_compile_errors
    ]
    ctx = _make_ctx(sampling_error=NotImplementedError("sampling not supported"))
    result = await auto_fix(ctx)
    assert "ERRORS" in result
    assert "Auto-fix unavailable" in result


@pytest.mark.asyncio
async def test_smart_build_no_sampling(mock_bridge):
    ctx = _make_ctx(sampling_error=NotImplementedError("no sampling"))
    result = await smart_build("create a red cube", ctx)
    assert "Sampling unavailable" in result
    assert "execute_code" in result
