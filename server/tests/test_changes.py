"""Tests for get_changes tool (Feature 4)."""
import pytest
from unity_mcp.server import get_changes


@pytest.mark.asyncio
async def test_get_changes_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "12:00:00 HIERARCHY_CHANGED"}
    result = await get_changes()
    mock_bridge.send.assert_called_once_with(
        "get_changes", {"clear": "true"}, timeout=30.0
    )
    assert result == "12:00:00 HIERARCHY_CHANGED"


@pytest.mark.asyncio
async def test_get_changes_clear_false(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "12:00:00 UNDO_REDO"}
    result = await get_changes(clear=False)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["clear"] == "false"
    assert result == "12:00:00 UNDO_REDO"
