import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import prefab


async def test_prefab_save(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok: Assets/P.prefab"}
    result = await prefab(action="save", path="/Template", asset_path="Assets/P.prefab")
    assert "ok" in result
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "prefab"
    assert call_args[0][1]["action"] == "save"
    assert call_args[0][1]["path"] == "/Template"
    assert call_args[0][1]["asset_path"] == "Assets/P.prefab"


async def test_prefab_create_variant(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok: Assets/Red.prefab"}
    result = await prefab(action="create_variant", path="dummy",
                          base_path="Assets/Base.prefab", variant_path="Assets/Red.prefab")
    assert "ok" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "create_variant"
    assert args["base_path"] == "Assets/Base.prefab"
    assert args["variant_path"] == "Assets/Red.prefab"


async def test_prefab_apply(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Applied"}
    result = await prefab(action="apply", path="/Instance")
    assert "Applied" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "apply"
    assert args["path"] == "/Instance"


async def test_prefab_revert(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Reverted"}
    result = await prefab(action="revert", path="/Instance")
    assert "Reverted" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "revert"


async def test_prefab_get_overrides(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "pos: (0,1,0)"}
    result = await prefab(action="get_overrides", path="/Instance")
    assert "pos" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "get_overrides"


async def test_prefab_unpack(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Unpacked"}
    result = await prefab(action="unpack", path="/Instance")
    assert "Unpacked" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "unpack"
    assert "recursive" not in args


async def test_prefab_unpack_recursive(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "Unpacked recursively"}
    result = await prefab(action="unpack", path="/Instance", recursive=True)
    assert "Unpacked" in result
    args = mock_bridge.send.call_args[0][1]
    assert args["recursive"] == "true"


async def test_prefab_error(mock_bridge):
    mock_bridge.send.return_value = {"ok": False, "err": "Not a prefab instance"}
    with pytest.raises(ToolError, match="Not a prefab instance"):
        await prefab(action="apply", path="/SomeObject")
