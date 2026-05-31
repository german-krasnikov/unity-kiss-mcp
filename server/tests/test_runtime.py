"""Tests for runtime Play Mode tools."""
import pytest
from unittest.mock import AsyncMock, patch
from unity_mcp.server import invoke_method, set_runtime_property, wait_until, query_state


@pytest.mark.asyncio
async def test_invoke_method_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    result = await invoke_method("/Player", "PlayerController", "Jump", "5.0")
    mock_bridge.send.assert_called_once_with(
        "invoke_method",
        {"path": "/Player", "component": "PlayerController", "method": "Jump", "args": "5.0"},
        timeout=30.0,
    )
    assert result == "void"


@pytest.mark.asyncio
async def test_invoke_method_no_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    await invoke_method("/Player", "PlayerController", "Jump")
    call_args = mock_bridge.send.call_args[0][1]
    assert "args" not in call_args


@pytest.mark.asyncio
async def test_set_runtime_property_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "speed=10"}
    result = await set_runtime_property("/Player", "PlayerController", "speed", "10")
    mock_bridge.send.assert_called_once_with(
        "set_runtime_property",
        {"path": "/Player", "component": "PlayerController", "field": "speed", "value": "10"},
        timeout=30.0,
    )
    assert result == "speed=10"


@pytest.mark.asyncio
async def test_wait_until_default_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "field=true after 1.2s"}
    result = await wait_until("/Player", "PlayerController", "isAlive", "true")
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "wait_until"
    sent = call_args[0][1]
    assert sent["timeout"] == "5.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 10.0


@pytest.mark.asyncio
async def test_wait_until_with_negate(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Enemy", "EnemyAI", "isAlive", "true", negate=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["negate"] == "true"


@pytest.mark.asyncio
async def test_wait_until_custom_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Player", "PlayerController", "hp", "0", timeout=15.0)
    call_args = mock_bridge.send.call_args
    sent = call_args[0][1]
    assert sent["timeout"] == "15.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 20.0


@pytest.mark.asyncio
async def test_query_state_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "GridPlayer.Score=5\nGridPlayer.PosX=3"}
    result = await query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
    mock_bridge.send.assert_called_once_with(
        "query_state",
        {"queries": "/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX"},
        timeout=10.0,
    )
    assert "GridPlayer.Score=5" in result


@pytest.mark.asyncio
async def test_query_state_multiple_objects(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Health.hp=80\nAmmo.count=5"}
    result = await query_state("/Player|Health|hp,/Player|Ammo|count")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["queries"] == "/Player|Health|hp,/Player|Ammo|count"
    assert result == "Health.hp=80\nAmmo.count=5"
