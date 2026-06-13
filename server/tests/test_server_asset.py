import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import asset


async def test_asset_find_by_type(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Mat.mat"})
    result = await asset(action="find", type="Material")
    mock_bridge.send.assert_called_once_with("asset", {"action": "find", "type": "Material"}, timeout=30.0)
    assert "Assets/Mat.mat" in result


async def test_asset_find_with_folder(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Prefabs/Enemy.prefab"})
    await asset(action="find", type="Prefab", folder="Assets/Prefabs")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "find", "type": "Prefab", "folder": "Assets/Prefabs"}


async def test_asset_find_with_name(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Textures/wood.png"})
    await asset(action="find", type="Texture2D", name="wood")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "find", "type": "Texture2D", "name": "wood"}


async def test_asset_get_info(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "type: Texture2D\nsize: 512x512"})
    result = await asset(action="get_info", path="Assets/Tex.png")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_info", "path": "Assets/Tex.png"}
    assert "type: Texture2D" in result


async def test_asset_create_folder(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/NewDir"})
    await asset(action="create", type="Folder", path="Assets/NewDir")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "create", "type": "Folder", "path": "Assets/NewDir"}


async def test_asset_create_material(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/Mat.mat"})
    await asset(action="create", type="Material", path="Assets/Mat.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "create", "type": "Material", "path": "Assets/Mat.mat"}


async def test_asset_move(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Moved to Assets/B.mat"})
    await asset(action="move", source="Assets/A.mat", dest="Assets/B.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "move", "source": "Assets/A.mat", "dest": "Assets/B.mat"}


async def test_asset_duplicate(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Duplicated to Assets/B.mat"})
    await asset(action="duplicate", source="Assets/A.mat", dest="Assets/B.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "duplicate", "source": "Assets/A.mat", "dest": "Assets/B.mat"}


async def test_asset_delete(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Deleted: Assets/Old.mat"})
    await asset(action="delete", path="Assets/Old.mat")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "delete", "path": "Assets/Old.mat"}


async def test_asset_get_dependencies(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Shader.shader"})
    await asset(action="get_dependencies", path="Assets/X.mat", recursive=True)
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_dependencies", "path": "Assets/X.mat", "recursive": "true"}


async def test_asset_import_settings(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "maxTextureSize=512"})
    await asset(action="import_settings", path="Assets/X.png", prop="maxTextureSize", value="512")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "import_settings", "path": "Assets/X.png", "prop": "maxTextureSize", "value": "512"}


async def test_asset_error_from_unity(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Asset not found: Assets/Missing.mat"})
    with pytest.raises(ToolError, match="Asset not found"):
        await asset(action="get_info", path="Assets/Missing.mat")


async def test_asset_create_error_raises_tool_error(mock_bridge):
    """asset create raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Folder already exists"})
    with pytest.raises(ToolError, match="Folder already exists"):
        await asset(action="create", type="Folder", path="Assets/Existing")


async def test_asset_move_error_raises_tool_error(mock_bridge):
    """asset move raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Source asset not found"})
    with pytest.raises(ToolError, match="Source asset not found"):
        await asset(action="move", source="Assets/Missing.mat", dest="Assets/B.mat")


async def test_asset_delete_error_raises_tool_error(mock_bridge):
    """asset delete raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Cannot delete read-only asset"})
    with pytest.raises(ToolError, match="Cannot delete read-only asset"):
        await asset(action="delete", path="Assets/ReadOnly.mat")
