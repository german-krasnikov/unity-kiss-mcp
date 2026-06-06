import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import batch
from unity_mcp.tools.batch import validate_references


@pytest.mark.asyncio
async def test_batch_text_forwarded(mock_bridge, bridge_response):
    """Text commands sent directly to bridge without JSON parsing."""
    bridge_response(data="[0] ok: /A\n[1] ok")
    commands = "create_object name=A primitive=Cube\nset_material path=/A color=#FF0000"
    result = await batch(commands=commands)
    mock_bridge.send.assert_called_once_with(
        "batch",
        {"commands": commands},
        timeout=30.0,
    )
    assert result == "[0] ok: /A\n[1] ok"


@pytest.mark.asyncio
async def test_batch_on_error_stop(mock_bridge, bridge_response):
    """on_error=stop forwarded to bridge."""
    bridge_response(data="[0] ok: /A\n[1] err: Not found\n[2] skip")
    result = await batch(commands="create_object name=A", on_error="stop")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["on_error"] == "stop"


@pytest.mark.asyncio
async def test_batch_non_default_timeout_sent(mock_bridge, bridge_response):
    """Non-default timeout is included in args."""
    bridge_response(data="[0] ok: /A")
    await batch(commands="create_object name=A", timeout=60.0)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["timeout_ms"] == 55000  # (60-5)*1000


@pytest.mark.asyncio
async def test_batch_default_timeout_omitted(mock_bridge, bridge_response):
    """Default timeout=30.0 → timeout_ms absent from args."""
    bridge_response(data="[0] ok: /A")
    await batch(commands="create_object name=A")
    call_args = mock_bridge.send.call_args[0]
    assert "timeout_ms" not in call_args[1]


@pytest.mark.asyncio
async def test_batch_on_error_continue(mock_bridge, bridge_response):
    """Default on_error=continue is omitted from args (token economy)."""
    bridge_response(data="[0] ok: /A")
    result = await batch(commands="create_object name=A")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert "on_error" not in call_args[1]


@pytest.mark.asyncio
async def test_batch_empty_commands(mock_bridge):
    """Empty string handled."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": ""})
    result = await batch(commands="")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["commands"] == ""


@pytest.mark.asyncio
async def test_batch_error_raises_tool_error(mock_bridge):
    """Bridge error raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Connection lost"})
    with pytest.raises(ToolError, match="Connection lost"):
        await batch(commands="create_object name=A")


@pytest.mark.asyncio
async def test_batch_vector_with_spaces_forwarded(mock_bridge):
    """Vector3 with spaces in parens forwarded as-is to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:2"})
    commands = "create_object name=A primitive=Cube\nset_property path=/A component=Transform prop=m_LocalPosition value=(0, 6.8, 0)"
    result = await batch(commands=commands)
    sent_commands = mock_bridge.send.call_args[0][1]["commands"]
    assert "(0, 6.8, 0)" in sent_commands


@pytest.mark.asyncio
async def test_batch_single_command(mock_bridge):
    """Single line command works."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[0] ok: /A"})
    result = await batch(commands="create_object name=A primitive=Cube")
    mock_bridge.send.assert_called_once_with(
        "batch",
        {"commands": "create_object name=A primitive=Cube"},
        timeout=30.0,
    )
    assert result == "[0] ok: /A"


@pytest.mark.asyncio
async def test_batch_rejects_dsl_tools_python_side(mock_bridge):
    """batch() rejects DSL tools registered via register_dsl_tools."""
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.add("test_dsl_cmd")
    try:
        with pytest.raises(ToolError, match="requires typed MCP tool"):
            await batch(commands="test_dsl_cmd path=/NPC")
    finally:
        _dsl_tools.discard("test_dsl_cmd")


# F27: atomic mode tests

@pytest.mark.asyncio
async def test_batch_atomic_true_forwarded(mock_bridge, bridge_response):
    """atomic=True is forwarded as 'true' string in command dict."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A", atomic=True)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1].get("atomic") == "true"


@pytest.mark.asyncio
async def test_batch_atomic_false_not_sent(mock_bridge, bridge_response):
    """atomic=False (default) means atomic key is absent from command dict."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A")
    call_args = mock_bridge.send.call_args[0]
    assert "atomic" not in call_args[1]


@pytest.mark.asyncio
async def test_batch_atomic_with_on_error(mock_bridge, bridge_response):
    """atomic=True is forwarded; on_error omitted when default (not sent to C#)."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A", atomic=True, on_error="continue")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1].get("atomic") == "true"
    assert "on_error" not in call_args[1]  # default value omitted


@pytest.mark.asyncio
async def test_batch_empty_commands_list(mock_bridge, bridge_response):
    """batch with only whitespace/newlines sends empty commands string."""
    bridge_response(data="ok:0")
    result = await batch(commands="\n\n  \n")
    call_args = mock_bridge.send.call_args[0]
    # commands forwarded as-is (whitespace only), no dsl rejection triggered
    assert call_args[0] == "batch"


@pytest.mark.asyncio
async def test_validate_references_ignore_optional_sent(mock_bridge):
    """validate_references with ignore_optional=True sends ignore_optional flag."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 3 OK"})
    await validate_references(path="/Root", ignore_optional=True)
    mock_bridge.send.assert_called_once_with(
        "validate_references",
        {"path": "/Root", "depth": 3, "ignore_optional": "true"},
        timeout=30.0,
    )


@pytest.mark.asyncio
async def test_validate_references_ignore_optional_false_omitted(mock_bridge):
    """validate_references with ignore_optional=False (default) omits the key."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 3 OK"})
    await validate_references(path="/Root")
    call_args = mock_bridge.send.call_args[0][1]
    assert "ignore_optional" not in call_args
