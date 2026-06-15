"""TDD tests for ask_user_tool — interactive question card via Unity UI."""
import pytest
from unittest.mock import AsyncMock, MagicMock


# ---------------------------------------------------------------------------
# 1. ask_user sends correct cmd/args/timeout
# ---------------------------------------------------------------------------

async def test_ask_user_sends_cmd_and_args():
    import unity_mcp.tools.ask_user_tool as mod
    questions = '[{"question":"Color?","options":[{"label":"Red"}]}]'
    send = AsyncMock(return_value='{"Color?":"Red"}')
    mod._send = send
    result = await mod.ask_user(questions)
    send.assert_awaited_once_with("ask_user", {"questions": questions}, timeout=300.0)
    assert result == '{"Color?":"Red"}'


# ---------------------------------------------------------------------------
# 2. ask_user passes through result unchanged
# ---------------------------------------------------------------------------

async def test_ask_user_returns_result_unchanged():
    import unity_mcp.tools.ask_user_tool as mod
    expected = '{"Confirm?":"Yes"}'
    send = AsyncMock(return_value=expected)
    mod._send = send
    result = await mod.ask_user("[]")
    assert result == expected


# ---------------------------------------------------------------------------
# 3. ask_user propagates exception from _send
# ---------------------------------------------------------------------------

async def test_ask_user_propagates_exception():
    import unity_mcp.tools.ask_user_tool as mod
    from mcp.server.fastmcp.exceptions import ToolError
    send = AsyncMock(side_effect=ToolError("timeout"))
    mod._send = send
    with pytest.raises(ToolError):
        await mod.ask_user("[]")


# ---------------------------------------------------------------------------
# 4. register wires _send and registers tool
# ---------------------------------------------------------------------------

def test_register_wires_send():
    import unity_mcp.tools.ask_user_tool as mod
    mcp = MagicMock()
    mcp.tool = MagicMock(return_value=lambda fn: fn)
    send = AsyncMock()
    args = MagicMock()
    mod.register(mcp, send, args)
    assert mod._send is send
    mcp.tool.assert_called_once()
