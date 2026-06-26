"""Tests for Watch System tools."""
import pytest
from unity_mcp.server import (
    watch_add, get_watches, watch_remove, watch_clear, watch_reset,
)


async def test_watch_add_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    result = await watch_add("/Player", "Health", "hp")
    mock_bridge.send.assert_called_once_with(
        "watch_add",
        {"path": "/Player", "component": "Health", "field": "hp"},
        timeout=30.0,
    )
    assert result == "w1"


async def test_watch_add_with_all_params(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w2"}
    await watch_add("/Player", "Health", "hp", condition="< 10", action="pause", interval_ms=250)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["condition"] == "< 10"
    assert sent["action"] == "pause"
    assert sent["interval_ms"] == "250"


async def test_watch_add_omits_optional_defaults(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch_add("/Player", "Health", "hp")
    sent = mock_bridge.send.call_args[0][1]
    assert "condition" not in sent
    assert "action" not in sent
    assert "interval_ms" not in sent


async def test_get_watches_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "watches: 0"}
    result = await get_watches()
    mock_bridge.send.assert_called_once_with("get_watches", {}, timeout=30.0)
    assert result == "watches: 0"


async def test_watch_remove_sends_id(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "removed"}
    result = await watch_remove("w1")
    mock_bridge.send.assert_called_once_with(
        "watch_remove", {"id": "w1"}, timeout=30.0
    )
    assert result == "removed"


async def test_watch_clear_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "cleared"}
    result = await watch_clear()
    mock_bridge.send.assert_called_once_with("watch_clear", {}, timeout=30.0)
    assert result == "cleared"


async def test_watch_reset_sends_id(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "reset"}
    result = await watch_reset("w1")
    mock_bridge.send.assert_called_once_with(
        "watch_reset", {"id": "w1"}, timeout=30.0
    )
    assert result == "reset"


async def test_watch_add_interval_ms_as_string(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch_add("/Go", "Comp", "field", interval_ms=1000)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["interval_ms"] == "1000"
    assert isinstance(sent["interval_ms"], str)


async def test_watch_add_action_log_omitted_as_default(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "w1"}
    await watch_add("/Go", "Comp", "field", action="log")
    sent = mock_bridge.send.call_args[0][1]
    # "log" is default — should be omitted
    assert "action" not in sent
