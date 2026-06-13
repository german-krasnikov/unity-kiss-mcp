import errno
import pytest
from unittest.mock import AsyncMock, patch, MagicMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import (
    _send, get_hierarchy, get_component, get_components_list, find_objects,
    set_property, create_object, delete_object, recompile, get_object_detail,
    run_tests, get_test_results, scene, search_scene, set_material, editor, animation, timeline,
    animator, get_enabled_tools, checkpoint, validate_references, compress_hierarchy,
    set_active, wire_event, unwire_event, screenshot, object_diff,
)


# --- _send helper tests ---

@pytest.mark.asyncio
async def test_send_returns_data_on_success(mock_bridge, bridge_response):
    """_send returns result['data'] when ok=True."""
    bridge_response(data="hello")
    result = await _send("ping", {})
    assert result == "hello"


@pytest.mark.asyncio
async def test_send_raises_tool_error_on_failure(mock_bridge, bridge_response):
    """_send raises ToolError when ok=False."""
    bridge_response(ok=False, err="not found")
    with pytest.raises(ToolError, match="not found"):
        await _send("ping", {})


@pytest.mark.asyncio
async def test_send_passes_args_to_bridge(mock_bridge, bridge_response):
    """_send passes args dict to bridge.send."""
    bridge_response()
    await _send("get_component", {"path": "/A", "type": "Transform"})
    mock_bridge.send.assert_called_once_with("get_component", {"path": "/A", "type": "Transform"}, timeout=30.0)


@pytest.mark.asyncio
async def test_send_passes_timeout_to_bridge(mock_bridge, bridge_response):
    """_send passes timeout kwarg to bridge.send when specified."""
    bridge_response(data="done")
    await _send("recompile", {}, timeout=60.0)
    mock_bridge.send.assert_called_once_with("recompile", {}, timeout=60.0)


@pytest.mark.asyncio
async def test_send_default_timeout(mock_bridge, bridge_response):
    """_send without timeout uses default 30s."""
    bridge_response()
    await _send("ping", {})
    mock_bridge.send.assert_called_once_with("ping", {}, timeout=30.0)


@pytest.mark.asyncio
async def test_send_returns_file_path_when_file_field_present(mock_bridge, bridge_response):
    """_send returns file path message when response has 'file' field."""
    bridge_response(file="/tmp/MCP/screenshot.png")
    result = await _send("screenshot", {})
    assert result == "Data saved to: /tmp/MCP/screenshot.png"


@pytest.mark.asyncio
async def test_send_returns_data_when_no_file_field(mock_bridge, bridge_response):
    """_send returns data normally when no 'file' field."""
    bridge_response(data="pong")
    result = await _send("ping", {})
    assert result == "pong"


@pytest.mark.asyncio
async def test_get_hierarchy_calls_bridge(mock_bridge, bridge_response):
    """get_hierarchy tool delegates to bridge.send."""
    bridge_response(data="Scene/GameObject")
    result = await get_hierarchy(depth=2, root="/Scene")
    mock_bridge.send.assert_called_once_with("get_hierarchy", {"depth": 2, "root": "/Scene"}, timeout=30.0)
    assert result == "Scene/GameObject"


@pytest.mark.asyncio
async def test_get_hierarchy_default_omits_components(mock_bridge, bridge_response):
    """get_hierarchy without components=True does not send components arg."""
    bridge_response(data="tree")
    await get_hierarchy()
    args = mock_bridge.send.call_args[0][1]
    assert "components" not in args


@pytest.mark.asyncio
async def test_get_hierarchy_components_true_sends_arg(mock_bridge, bridge_response):
    """get_hierarchy with components=True sends components='true'."""
    bridge_response(data="tree")
    await get_hierarchy(components=True)
    args = mock_bridge.send.call_args[0][1]
    assert args["components"] == "true"


@pytest.mark.asyncio
async def test_get_hierarchy_incremental_sends_arg(mock_bridge):
    """get_hierarchy with incremental=True sends incremental='true' to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "tree"})
    await get_hierarchy(incremental=True)
    args = mock_bridge.send.call_args[0][1]
    assert args.get("incremental") == "true"


@pytest.mark.asyncio
async def test_get_component_calls_bridge(mock_bridge):
    """get_component tool calls bridge.send with correct args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "pos: 1,2,3"})
    await get_component(path="/Player", type="Transform")
    mock_bridge.send.assert_called_once_with(
        "get_component", {"path": "/Player", "type": "Transform"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_get_component_returns_data_on_success(mock_bridge):
    """ok response returns data."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "pos: 1,2,3"})
    result = await get_component(path="/Player", type="Transform")
    assert result == "pos: 1,2,3"


@pytest.mark.asyncio
async def test_get_component_raises_on_failure(mock_bridge):
    """ok=false raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "GameObject not found"})
    with pytest.raises(ToolError, match="GameObject not found"):
        await get_component(path="/Missing", type="Transform")


@pytest.mark.asyncio
async def test_find_objects_calls_bridge(mock_bridge):
    """find_objects tool calls bridge.send."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/Player\n/Enemy"})
    await find_objects(name="Player", tag="Player")
    mock_bridge.send.assert_called_once_with(
        "find_objects", {"name": "Player", "tag": "Player"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_set_property_calls_bridge(mock_bridge):
    """set_property tool calls bridge.send."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await set_property(path="/Player", component="Transform", prop="position.x", value="5.0")
    mock_bridge.send.assert_called_once_with(
        "set_property",
        {"path": "/Player", "component": "Transform", "prop": "position.x", "value": "5.0"},
        timeout=30.0,
    )


@pytest.mark.asyncio
async def test_create_object_calls_bridge(mock_bridge):
    """create_object tool calls bridge.send."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/NewObject"})
    await create_object(name="NewObject", parent="/Scene")
    mock_bridge.send.assert_called_once_with(
        "create_object", {"name": "NewObject", "parent": "/Scene"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_delete_object_accepts_path(mock_bridge, bridge_response):
    """delete_object(path=...) sends only path key, no id."""
    bridge_response(data="Deleted /Test")
    await delete_object(path="/Test")
    mock_bridge.send.assert_called_once_with("delete_object", {"path": "/Test"}, timeout=30.0)


@pytest.mark.asyncio
async def test_delete_object_accepts_id(mock_bridge, bridge_response):
    """delete_object(id=123) sends only id key, no path."""
    bridge_response(data="Deleted #123")
    await delete_object(id=123)
    mock_bridge.send.assert_called_once_with("delete_object", {"id": 123}, timeout=30.0)


def test_delete_object_signature_supports_both():
    """delete_object signature has id and path params both defaulting to None."""
    import inspect
    sig = inspect.signature(delete_object)
    assert "id" in sig.parameters
    assert "path" in sig.parameters
    assert sig.parameters["id"].default is None
    assert sig.parameters["path"].default is None


@pytest.mark.asyncio
async def test_delete_object_neither_arg_raises(mock_bridge):
    """delete_object() with no args raises ValueError and does NOT call bridge."""
    with pytest.raises(ValueError, match="id or path required"):
        await delete_object()
    mock_bridge.send.assert_not_called()


@pytest.mark.asyncio
async def test_recompile_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    result = await recompile()
    mock_bridge.send.assert_called_once_with("recompile", {}, timeout=60.0)
    assert result == "ok"


@pytest.mark.asyncio
async def test_recompile_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Compilation failed"})
    with pytest.raises(ToolError, match="Compilation failed"):
        await recompile()


@pytest.mark.asyncio
async def test_get_components_list_calls_bridge_with_id(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Rigidbody\nMeshRenderer"})
    result = await get_components_list(id=12345)
    mock_bridge.send.assert_called_once_with("get_components_list", {"id": 12345}, timeout=30.0)
    assert result == "Rigidbody\nMeshRenderer"


@pytest.mark.asyncio
async def test_get_object_detail_calls_bridge(mock_bridge):
    """Test get_object_detail sends correct command to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "GameObject: Cube #12345\nActive: true\n---\n[MeshFilter]\nmesh = Cube"})
    result = await get_object_detail(id=12345)
    mock_bridge.send.assert_called_once_with("get_object_detail", {"id": 12345}, timeout=30.0)
    assert "Cube" in result


@pytest.mark.asyncio
async def test_get_object_detail_raises_on_error(mock_bridge):
    """Test get_object_detail raises ToolError when object not found"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    with pytest.raises(ToolError, match="Object not found"):
        await get_object_detail(id=99999)


@pytest.mark.asyncio
async def test_run_tests_calls_bridge(mock_bridge):
    """Test run_tests sends correct command to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Tests: 5 passed, 0 failed\nTime: 1.2s"})
    result = await run_tests(mode="EditMode")
    mock_bridge.send.assert_called_once_with("run_tests", {"mode": "EditMode"}, timeout=120.0)
    assert "passed" in result


@pytest.mark.asyncio
async def test_run_tests_default_mode(mock_bridge):
    """Test run_tests defaults to EditMode"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Tests: 3 passed, 0 failed"})
    result = await run_tests()
    mock_bridge.send.assert_called_once_with("run_tests", {"mode": "EditMode"}, timeout=120.0)
    assert "passed" in result


@pytest.mark.asyncio
async def test_run_tests_raises_on_error(mock_bridge):
    """Test run_tests raises ToolError"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Test framework not available"})
    with pytest.raises(ToolError, match="Test framework not available"):
        await run_tests()


@pytest.mark.asyncio
async def test_get_test_results_calls_bridge(mock_bridge):
    """get_test_results sends get_test_results command with empty args."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "5 tests: 5 passed (1.2s)"})
    result = await get_test_results()
    mock_bridge.send.assert_called_once_with("get_test_results", {}, timeout=30.0)
    assert result == "5 tests: 5 passed (1.2s)"


@pytest.mark.asyncio
async def test_get_test_results_returns_pending(mock_bridge):
    """get_test_results returns 'pending' when tests still running."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "pending"})
    result = await get_test_results()
    assert result == "pending"


@pytest.mark.asyncio
async def test_get_test_results_returns_none_when_no_run(mock_bridge):
    """get_test_results returns 'none' when no test run has occurred."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "none"})
    result = await get_test_results()
    assert result == "none"


@pytest.mark.asyncio
async def test_run_tests_playmode_polls_after_disconnect(mock_bridge):
    """run_tests PlayMode: on disconnect, polls get_test_results until results available."""
    call_count = 0

    async def side_effect(cmd, args, timeout=30.0):
        nonlocal call_count
        call_count += 1
        if cmd == "run_tests":
            raise ToolError("Unity connection lost")
        if cmd == "get_test_results":
            if call_count < 4:
                return {"ok": True, "data": "pending"}
            return {"ok": True, "data": "3 tests: 3 passed (2.1s)"}

    mock_bridge.send = AsyncMock(side_effect=side_effect)

    import unity_mcp.tools.scene as _scene
    _scene._POLL_INTERVAL = 0
    _scene._POLL_ATTEMPTS = 10
    try:
        result = await run_tests(mode="PlayMode")
    finally:
        _scene._POLL_INTERVAL = 2.0
        _scene._POLL_ATTEMPTS = 30
    assert "passed" in result
    calls = [c for c in mock_bridge.send.call_args_list if c[0][0] == "get_test_results"]
    assert len(calls) >= 1


@pytest.mark.asyncio
async def test_run_tests_playmode_timeout_returns_error(mock_bridge):
    """run_tests PlayMode: returns error string if polling times out."""
    async def side_effect(cmd, args, timeout=30.0):
        if cmd == "run_tests":
            raise ToolError("Unity connection lost")
        return {"ok": True, "data": "pending"}

    mock_bridge.send = AsyncMock(side_effect=side_effect)

    import unity_mcp.tools.scene as _scene
    _scene._POLL_INTERVAL = 0
    _scene._POLL_ATTEMPTS = 3
    try:
        result = await run_tests(mode="PlayMode")
    finally:
        _scene._POLL_INTERVAL = 2.0
        _scene._POLL_ATTEMPTS = 30
    assert "Error" in result or "timeout" in result.lower()


@pytest.mark.asyncio
async def test_run_tests_editmode_does_not_catch_disconnect(mock_bridge):
    """run_tests EditMode: ToolError propagates without polling fallback."""
    mock_bridge.send = AsyncMock(side_effect=ToolError("Unity connection lost"))
    with pytest.raises(ToolError):
        await run_tests(mode="EditMode")


@pytest.mark.asyncio
async def test_run_tests_playmode_tool_error_on_poll_swallowed(mock_bridge):
    """run_tests PlayMode: ToolError from every get_test_results poll is swallowed; result is error string."""
    mock_bridge.send = AsyncMock(side_effect=ToolError("disconnected"))

    import unity_mcp.tools.scene as _scene
    _scene._POLL_INTERVAL = 0
    _scene._POLL_ATTEMPTS = 3
    try:
        result = await run_tests(mode="PlayMode")
    finally:
        _scene._POLL_INTERVAL = 2.0
        _scene._POLL_ATTEMPTS = 30
    assert result.startswith("Error:")


@pytest.mark.asyncio
async def test_scene_new_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Untitled"})
    result = await scene(action="new")
    mock_bridge.send.assert_called_once_with("scene", {"action": "new"}, timeout=30.0)
    assert result == "Untitled"


@pytest.mark.asyncio
async def test_scene_open_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "TestScene"})
    result = await scene(action="open", path="Assets/Scenes/TestScene.unity")
    mock_bridge.send.assert_called_once_with("scene", {"action": "open", "path": "Assets/Scenes/TestScene.unity"}, timeout=30.0)
    assert result == "TestScene"


@pytest.mark.asyncio
async def test_scene_open_raises_on_error(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene not found"})
    with pytest.raises(ToolError, match="Scene not found"):
        await scene(action="open", path="Assets/Missing.unity")


@pytest.mark.asyncio
async def test_scene_discard_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "reloaded"})
    result = await scene(action="discard")
    mock_bridge.send.assert_called_once_with("scene", {"action": "discard"}, timeout=30.0)
    assert result == "reloaded"


@pytest.mark.asyncio
async def test_scene_save_calls_bridge(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Assets/Scenes/Test.unity"})
    await scene(action="save", path="Assets/Scenes/Test.unity")
    mock_bridge.send.assert_called_once_with("scene", {"action": "save", "path": "Assets/Scenes/Test.unity"}, timeout=30.0)


@pytest.mark.asyncio
async def test_disabled_tool_raises_tool_error(mock_bridge):
    """Disabled tool raises ToolError from Unity side"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Tool 'screenshot' is disabled in settings"})
    from unity_mcp.server import screenshot
    with pytest.raises(ToolError, match="disabled"):
        await screenshot()


@pytest.mark.asyncio
async def test_animation_get_calls_bridge(mock_bridge):
    """animation get calls bridge with path only"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Animator: Idle, Walk\n---\nIdle | 1.0s | 3 curves"})
    result = await animation(action="get", path="/Player")
    mock_bridge.send.assert_called_once_with("animation", {"action": "get", "path": "/Player"}, timeout=30.0)
    assert "Idle" in result


@pytest.mark.asyncio
async def test_animation_get_with_clip(mock_bridge):
    """animation get with clip name sends both path and clip"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Idle | 1.0s | 3 curves\nlocalPosition.x: 0.0 -> 2.0"})
    result = await animation(action="get", path="/Player", clip="Idle")
    mock_bridge.send.assert_called_once_with("animation", {"action": "get", "path": "/Player", "clip": "Idle"}, timeout=30.0)
    assert "localPosition" in result


@pytest.mark.asyncio
async def test_animation_get_with_time(mock_bridge):
    """animation get with time parameter sends float value"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Position: (0.5, 1.0, 0.0)"})
    result = await animation(action="get", path="/Player", clip="Walk", time=0.5)
    mock_bridge.send.assert_called_once_with("animation", {"action": "get", "path": "/Player", "clip": "Walk", "time": 0.5}, timeout=30.0)
    assert "Position" in result


@pytest.mark.asyncio
async def test_animation_get_raises_on_error(mock_bridge):
    """animation get raises ToolError when animator not found"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Animator not found"})
    with pytest.raises(ToolError, match="Animator not found"):
        await animation(action="get", path="/Missing")


@pytest.mark.asyncio
async def test_animation_create_calls_bridge(mock_bridge):
    """animation create sends all args to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Jump.anim"})
    result = await animation(action="create", path="/Player", clip_name="Jump", property="localPosition", keys="t:0 v:(0,0,0); t:1 v:(0,2,0)")
    mock_bridge.send.assert_called_once_with("animation", {"action": "create", "path": "/Player", "clip_name": "Jump", "property": "localPosition", "keys": "t:0 v:(0,0,0); t:1 v:(0,2,0)"}, timeout=30.0)
    assert "Jump" in result


@pytest.mark.asyncio
async def test_animation_edit_calls_bridge(mock_bridge):
    """animation edit sends action, property, keys to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Key added"})
    result = await animation(action="edit", path="/Player", clip="Walk", keys="t:0.5 v:1.5", property="localPosition.y")
    mock_bridge.send.assert_called_once_with("animation", {"action": "edit", "path": "/Player", "clip": "Walk", "keys": "t:0.5 v:1.5", "property": "localPosition.y"}, timeout=30.0)
    assert "added" in result


@pytest.mark.asyncio
async def test_animation_preview_calls_bridge(mock_bridge):
    """animation preview sends path, clip, action, time to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Previewing Walk at 0.5s"})
    result = await animation(action="preview", path="/Player", clip="Walk", time=0.5)
    mock_bridge.send.assert_called_once_with("animation", {"action": "preview", "path": "/Player", "clip": "Walk", "time": 0.5}, timeout=30.0)
    assert "Walk" in result


@pytest.mark.asyncio
async def test_animation_preview_no_optional(mock_bridge):
    """animation preview with only required args"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Sampling Idle at 0.0s"})
    result = await animation(action="preview", path="/Player", clip="Idle")
    mock_bridge.send.assert_called_once_with("animation", {"action": "preview", "path": "/Player", "clip": "Idle"}, timeout=30.0)
    assert "Idle" in result


# Timeline tests
@pytest.mark.asyncio
async def test_timeline_get_calls_bridge(mock_bridge):
    """timeline get calls bridge with path only"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Timeline: Cutscene\nTracks: 2\nActivation | Character\nAnimation | Character"})
    result = await timeline(action="get", path="/CutsceneManager")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "get", "path": "/CutsceneManager"}, timeout=30.0)
    assert "Cutscene" in result


@pytest.mark.asyncio
async def test_timeline_get_with_track(mock_bridge):
    """timeline get with track name sends both path and track"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Animation | Character\nClips: 1\n0.0s-2.0s | Walk | bound: Character"})
    result = await timeline(action="get", path="/CutsceneManager", track="Character")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "get", "path": "/CutsceneManager", "track": "Character"}, timeout=30.0)
    assert "Character" in result


@pytest.mark.asyncio
async def test_timeline_get_raises_on_error(mock_bridge):
    """timeline get raises ToolError when director not found"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "PlayableDirector not found"})
    with pytest.raises(ToolError, match="PlayableDirector not found"):
        await timeline(action="get", path="/Missing")


@pytest.mark.asyncio
async def test_timeline_create_calls_bridge(mock_bridge):
    """timeline create sends all args to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/T.playable"})
    result = await timeline(action="create", path="/GO", asset_path="Assets/T.playable", director_path="/GO", tracks="Animation:Char")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "create", "path": "/GO", "asset_path": "Assets/T.playable", "director_path": "/GO", "tracks": "Animation:Char"}, timeout=30.0)
    assert "T.playable" in result


@pytest.mark.asyncio
async def test_timeline_create_minimal(mock_bridge):
    """timeline create with only asset_path sends minimal args"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created: Assets/T.playable"})
    result = await timeline(action="create", path="/GO", asset_path="Assets/T.playable")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "create", "path": "/GO", "asset_path": "Assets/T.playable"}, timeout=30.0)
    assert "T.playable" in result


@pytest.mark.asyncio
async def test_timeline_edit_calls_bridge(mock_bridge):
    """timeline edit sends action and optional args to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Track added: BGM"})
    result = await timeline(action="edit", path="/GO", track="BGM", track_type="Audio")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "edit", "path": "/GO", "track": "BGM", "track_type": "Audio"}, timeout=30.0)
    assert "BGM" in result


@pytest.mark.asyncio
async def test_timeline_preview_calls_bridge(mock_bridge):
    """timeline preview sends path, action, time to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Sampling at 5.0s"})
    result = await timeline(action="preview", path="/GO", time=5.0)
    mock_bridge.send.assert_called_once_with("timeline", {"action": "preview", "path": "/GO", "time": 5.0}, timeout=30.0)
    assert "5.0" in result


@pytest.mark.asyncio
async def test_timeline_preview_no_time(mock_bridge):
    """timeline preview with no time omits time from args"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Sampling at 0.0s"})
    result = await timeline(action="preview", path="/GO")
    mock_bridge.send.assert_called_once_with("timeline", {"action": "preview", "path": "/GO"}, timeout=30.0)
    assert "0.0" in result


@pytest.mark.asyncio
async def test_get_hierarchy_tool_description_exists():
    """Verify get_hierarchy tool has a docstring mentioning max nodes."""
    assert get_hierarchy.__doc__ is not None
    assert "3000" in get_hierarchy.__doc__
    assert "narrow" in get_hierarchy.__doc__.lower() or "filter" in get_hierarchy.__doc__.lower()


@pytest.mark.asyncio
async def test_get_enabled_tools_calls_bridge(mock_bridge):
    """get_enabled_tools calls bridge with correct command."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "get_hierarchy,get_component,screenshot"})
    result = await get_enabled_tools()
    mock_bridge.send.assert_called_once_with("get_enabled_tools", {}, timeout=30.0)
    assert "get_hierarchy" in result
    assert "screenshot" in result


@pytest.mark.asyncio
async def test_get_enabled_tools_raises_on_error(mock_bridge):
    """get_enabled_tools raises ToolError on failure."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Connection failed"})
    with pytest.raises(ToolError, match="Connection failed"):
        await get_enabled_tools()


@pytest.mark.asyncio
async def test_get_hierarchy_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Scene is empty"})
    with pytest.raises(ToolError, match="Scene is empty"):
        await get_hierarchy()


@pytest.mark.asyncio
async def test_search_scene_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Invalid query"})
    with pytest.raises(ToolError, match="Invalid query"):
        await search_scene(query="t:BadType")


@pytest.mark.asyncio
async def test_set_material_raises_on_failure(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Object not found"})
    with pytest.raises(ToolError, match="Object not found"):
        await set_material(path="/Missing", color="#FF0000")


@pytest.mark.asyncio
async def test_editor_state_default(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "playing:False\ncompiling:False"})
    result = await editor("state")
    mock_bridge.send.assert_called_with("editor", {"action": "state"}, timeout=30.0)
    assert result == "playing:False\ncompiling:False"


@pytest.mark.asyncio
async def test_editor_play(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    result = await editor("play")
    mock_bridge.send.assert_called_with("editor", {"action": "play"}, timeout=15.0)
    assert result == "ok"


@pytest.mark.asyncio
async def test_editor_select_with_path(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "selected:/Player"})
    result = await editor("select", path="/Player")
    mock_bridge.send.assert_called_with("editor", {"action": "select", "path": "/Player"}, timeout=30.0)
    assert result == "selected:/Player"


# --- Animator Controller tests ---

@pytest.mark.asyncio
async def test_animator_get_calls_bridge(mock_bridge):
    """animator get sends path to bridge"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "AnimatorController: Player | 1 layer | 3 params | 4 states"})
    result = await animator(action="get", path="/Player")
    mock_bridge.send.assert_called_once_with("animator", {"action": "get", "path": "/Player"}, timeout=30.0)
    assert "Player" in result


@pytest.mark.asyncio
async def test_animator_get_with_state(mock_bridge):
    """animator get with state name sends state param"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "state: Idle | Idle.anim | 1.0x"})
    result = await animator(action="get", path="/Player", state="Idle")
    mock_bridge.send.assert_called_once_with("animator", {"action": "get", "path": "/Player", "state": "Idle"}, timeout=30.0)
    assert "Idle" in result


@pytest.mark.asyncio
async def test_animator_add_param(mock_bridge):
    """animator add_param sends params string"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "added: Speed(float), Jump(trigger)"})
    result = await animator(action="add_param", path="/Player", params="Speed:float:0; Jump:trigger")
    mock_bridge.send.assert_called_once_with("animator", {"action": "add_param", "path": "/Player", "params": "Speed:float:0; Jump:trigger"}, timeout=30.0)
    assert "Speed" in result


@pytest.mark.asyncio
async def test_animator_add_state(mock_bridge):
    """animator add_state sends states string"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "added: Idle, Walk"})
    result = await animator(action="add_state", path="/Player", states="Idle:Idle.anim; Walk:Walk.anim")
    mock_bridge.send.assert_called_once_with("animator", {"action": "add_state", "path": "/Player", "states": "Idle:Idle.anim; Walk:Walk.anim"}, timeout=30.0)
    assert "Idle" in result


@pytest.mark.asyncio
async def test_animator_add_transition(mock_bridge):
    """animator add_transition sends source, target, conditions, duration"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "transition: Idle → Walk"})
    result = await animator(action="add_transition", path="/Player", source="Idle", target="Walk",
                           conditions="Speed>0.1", duration=0.15)
    mock_bridge.send.assert_called_once_with("animator", {
        "action": "add_transition", "path": "/Player",
        "source": "Idle", "target": "Walk",
        "conditions": "Speed>0.1", "duration": 0.15
    }, timeout=30.0)
    assert "Walk" in result


@pytest.mark.asyncio
async def test_animator_add_transition_anystate(mock_bridge):
    """animator add_transition with source=* for AnyState"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "transition: [Any] → Jump"})
    result = await animator(action="add_transition", path="/Player", source="*", target="Jump",
                           conditions="Jump; IsGrounded", duration=0.1)
    mock_bridge.send.assert_called_once_with("animator", {
        "action": "add_transition", "path": "/Player",
        "source": "*", "target": "Jump",
        "conditions": "Jump; IsGrounded", "duration": 0.1
    }, timeout=30.0)
    assert "Jump" in result


@pytest.mark.asyncio
async def test_animator_add_transition_with_exit_time(mock_bridge):
    """animator add_transition with exit_time and has_exit_time"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "transition: Jump → Idle | exit:0.9"})
    result = await animator(action="add_transition", path="/Player", source="Jump", target="Idle",
                           conditions="IsGrounded", exit_time=0.9, has_exit_time=True, duration=0.15)
    mock_bridge.send.assert_called_once_with("animator", {
        "action": "add_transition", "path": "/Player",
        "source": "Jump", "target": "Idle",
        "conditions": "IsGrounded", "exit_time": 0.9,
        "has_exit_time": True, "duration": 0.15
    }, timeout=30.0)
    assert "exit" in result


@pytest.mark.asyncio
async def test_animator_set_default(mock_bridge):
    """animator set_default sends state name"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "default: Idle"})
    result = await animator(action="set_default", path="/Player", state="Idle")
    mock_bridge.send.assert_called_once_with("animator", {"action": "set_default", "path": "/Player", "state": "Idle"}, timeout=30.0)
    assert "Idle" in result


@pytest.mark.asyncio
async def test_animator_remove_param(mock_bridge):
    """animator remove param sends type and name"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "removed: param Speed"})
    result = await animator(action="remove", path="/Player", type="param", name="Speed")
    mock_bridge.send.assert_called_once_with("animator", {"action": "remove", "path": "/Player", "type": "param", "name": "Speed"}, timeout=30.0)
    assert "Speed" in result


@pytest.mark.asyncio
async def test_animator_remove_state(mock_bridge):
    """animator remove state"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "removed: state Walk"})
    result = await animator(action="remove", path="/Player", type="state", name="Walk")
    mock_bridge.send.assert_called_once_with("animator", {"action": "remove", "path": "/Player", "type": "state", "name": "Walk"}, timeout=30.0)
    assert "Walk" in result


@pytest.mark.asyncio
async def test_animator_remove_transition(mock_bridge):
    """animator remove transition sends source and target"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "removed: transition Idle → Walk"})
    result = await animator(action="remove", path="/Player", type="transition", name="", source="Idle", target="Walk")
    mock_bridge.send.assert_called_once_with("animator", {
        "action": "remove", "path": "/Player",
        "type": "transition", "name": "",
        "source": "Idle", "target": "Walk"
    }, timeout=30.0)
    assert "Walk" in result


@pytest.mark.asyncio
async def test_animator_get_raises_on_error(mock_bridge):
    """animator get raises ToolError when animator not found"""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Animator not found"})
    with pytest.raises(ToolError, match="Animator not found"):
        await animator(action="get", path="/Missing")


@pytest.mark.asyncio
async def test_animator_none_params_excluded(mock_bridge):
    """animator excludes None params from args dict"""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await animator(action="get", path="/Player")
    call_args = mock_bridge.send.call_args[0][1]
    assert "state" not in call_args
    assert "states" not in call_args
    assert "conditions" not in call_args


@pytest.mark.asyncio
async def test_checkpoint_sends_label(mock_bridge):
    """checkpoint sends label to Unity."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Checkpoint: save"})
    result = await checkpoint(label="save")
    mock_bridge.send.assert_called_once_with("checkpoint", {"label": "save"}, timeout=30.0)


@pytest.mark.asyncio
async def test_checkpoint_default_label(mock_bridge):
    """checkpoint uses 'checkpoint' as default label."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Checkpoint: checkpoint"})
    result = await checkpoint()
    mock_bridge.send.assert_called_once_with("checkpoint", {"label": "checkpoint"}, timeout=30.0)


@pytest.mark.asyncio
async def test_get_hierarchy_compress_true(mock_bridge):
    """get_hierarchy with compress=True applies compress_hierarchy."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "  slot_0  []  #1\n  slot_1  []  #2"})
    result = await get_hierarchy(compress=True)
    assert "[2x slot]" in result
    assert "slot_0" not in result


@pytest.mark.asyncio
async def test_get_hierarchy_compress_false_default(mock_bridge):
    """get_hierarchy default compress=False returns raw data."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "  slot_0  []  #1\n  slot_1  []  #2"})
    result = await get_hierarchy()
    assert "slot_0" in result


@pytest.mark.asyncio
async def test_validate_references_sends_args(mock_bridge):
    """validate_references sends path and depth."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 5 OK"})
    await validate_references(path="/Root", depth=5)
    mock_bridge.send.assert_called_once_with(
        "validate_references",
        {"path": "/Root", "depth": 5},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_validate_references_defaults(mock_bridge):
    """validate_references default depth=3, verbose omitted (not sent)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 0 OK"})
    await validate_references(path="/A")
    mock_bridge.send.assert_called_once_with(
        "validate_references",
        {"path": "/A", "depth": 3},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_validate_references_verbose(mock_bridge):
    """validate_references verbose=True sends verbose flag."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[OK] ..."})
    await validate_references(path="/Root", verbose=True)
    mock_bridge.send.assert_called_once_with(
        "validate_references",
        {"path": "/Root", "depth": 3, "verbose": "true"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_set_active_true(mock_bridge):
    """set_active sends active='true' for True."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/A active=True"})
    result = await set_active(path="/A", active=True)
    mock_bridge.send.assert_called_once_with(
        "set_active", {"path": "/A", "active": "true"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_set_active_false(mock_bridge):
    """set_active sends active='false' for False."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "/A active=False"})
    result = await set_active(path="/A", active=False)
    mock_bridge.send.assert_called_once_with(
        "set_active", {"path": "/A", "active": "false"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_create_object_with_prefab_path(mock_bridge):
    """create_object passes prefab_path when provided."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /MyPrefab"})
    await create_object(name="MyPrefab", prefab_path="Assets/Prefabs/Enemy.prefab")
    mock_bridge.send.assert_called_once_with(
        "create_object",
        {"name": "MyPrefab", "prefab_path": "Assets/Prefabs/Enemy.prefab"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_create_object_without_prefab_unchanged(mock_bridge):
    """create_object without prefab_path works as before."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Created /Cube"})
    await create_object(name="Cube", primitive="Cube")
    mock_bridge.send.assert_called_once_with(
        "create_object",
        {"name": "Cube", "primitive": "Cube"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_wire_event_sends_all_args(mock_bridge):
    """wire_event sends all parameters to Unity."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Wired"})
    await wire_event(
        path="/Button", component="Button", event="onClick",
        target="/Panel", method="SetActive", arg_type="bool", arg_value="true"
    )
    mock_bridge.send.assert_called_once_with(
        "wire_event",
        {"path": "/Button", "component": "Button", "event": "onClick",
         "target": "/Panel", "method": "SetActive", "arg_type": "bool", "arg_value": "true"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_wire_event_default_arg_type(mock_bridge):
    """wire_event defaults arg_type to 'void'."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Wired"})
    await wire_event(
        path="/Btn", component="ClickHandler", event="_onComplete",
        target="/Door", method="Open"
    )
    mock_bridge.send.assert_called_once_with(
        "wire_event",
        {"path": "/Btn", "component": "ClickHandler", "event": "_onComplete",
         "target": "/Door", "method": "Open", "arg_type": "void"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_wire_event_optional_arg_value(mock_bridge):
    """wire_event omits arg_value when None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Wired"})
    await wire_event(
        path="/A", component="B", event="e",
        target="/C", method="M", arg_type="void"
    )
    args = mock_bridge.send.call_args[0][1]
    assert "arg_value" not in args


@pytest.mark.asyncio
async def test_wire_event_object_arg_type(mock_bridge):
    """wire_event passes object arg_type and arg_value to Unity."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Wired"})
    await wire_event(
        path="/Button", component="Button", event="onClick",
        target="/Panel", method="OnItemReceived", arg_type="object", arg_value="/Inventory"
    )
    mock_bridge.send.assert_called_once_with(
        "wire_event",
        {"path": "/Button", "component": "Button", "event": "onClick",
         "target": "/Panel", "method": "OnItemReceived", "arg_type": "object", "arg_value": "/Inventory"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_get_component_returns_unity_event_expanded(mock_bridge):
    """get_component passes through UnityEvent expanded format from Unity."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "[TestEventScript]\nonActivate: UnityEvent[1] -> Button.SetActive(bool=True)"
    })
    result = await get_component(path="/Obj", type="TestEventScript")
    assert "UnityEvent[1]" in result
    assert "Button.SetActive(bool=True)" in result


@pytest.mark.asyncio
async def test_summary_sends_correct_args(mock_bridge):
    """get_hierarchy with summary=True sends summary='true'."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Scene (0 nodes)"})
    await get_hierarchy(summary=True)
    mock_bridge.send.assert_called_once_with(
        "get_hierarchy", {"summary": "true"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_summary_with_root(mock_bridge):
    """get_hierarchy with summary=True and root forwards root."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Scene (5 nodes)"})
    await get_hierarchy(summary=True, root="Environment")
    mock_bridge.send.assert_called_once_with(
        "get_hierarchy", {"summary": "true", "root": "Environment"}, timeout=30.0
    )


@pytest.mark.asyncio
async def test_summary_ignores_other_params(mock_bridge):
    """get_hierarchy with summary=True does not send depth/filter/components."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Scene (10 nodes)"})
    await get_hierarchy(summary=True, depth=5, filter="Player", components=True)
    sent_args = mock_bridge.send.call_args[0][1]
    assert "depth" not in sent_args
    assert "filter" not in sent_args
    assert "components" not in sent_args


@pytest.mark.asyncio
async def test_get_component_returns_empty_unity_event(mock_bridge):
    """get_component passes through empty UnityEvent format."""
    mock_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "[TestEventScript]\nonActivate: UnityEvent[0]"
    })
    result = await get_component(path="/Obj", type="TestEventScript")
    assert "UnityEvent[0]" in result


@pytest.mark.asyncio
async def test_set_property_dry_run_passes_arg(mock_bridge):
    """set_property with dry_run=True passes dry_run='true' to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "DRY-RUN: mass would change 1 → 99"})
    result = await set_property(path="/Obj", component="Rigidbody", prop="mass", value="99", dry_run=True)
    called_args = mock_bridge.send.call_args[0][1]
    assert called_args.get("dry_run") == "true"
    assert "DRY-RUN" in result


@pytest.mark.asyncio
async def test_set_property_bool_python_native(mock_bridge):
    """set_property with value=True (Python bool) → bridge receives 'true' string."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await set_property(path="/Obj", component="Rigidbody", prop="m_UseGravity", value=True)
    called_args = mock_bridge.send.call_args[0][1]
    assert called_args["value"] == "true"


# --- unwire_event tests ---

@pytest.mark.asyncio
async def test_unwire_event_clear_all(mock_bridge):
    """unwire_event with no index clears all entries."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Cleared onClick (3 removed)"})
    await unwire_event(path="/Btn", component="Button", event="onClick")
    mock_bridge.send.assert_called_once_with(
        "unwire_event",
        {"path": "/Btn", "component": "Button", "event": "onClick"},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_unwire_event_by_index(mock_bridge):
    """unwire_event with index removes specific entry."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Removed onClick[1]"})
    await unwire_event(path="/Btn", component="Button", event="onClick", index=1)
    mock_bridge.send.assert_called_once_with(
        "unwire_event",
        {"path": "/Btn", "component": "Button", "event": "onClick", "index": 1},
        timeout=30.0
    )


@pytest.mark.asyncio
async def test_unwire_event_no_index_omitted(mock_bridge):
    """unwire_event omits index when None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Cleared"})
    await unwire_event(path="/A", component="B", event="e")
    args = mock_bridge.send.call_args[0][1]
    assert "index" not in args


# --- screenshot new params tests ---

@pytest.mark.asyncio
async def test_screenshot_show_colliders_passthrough(mock_bridge):
    """screenshot passes show_colliders=true when enabled."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "img"})
    await screenshot(camera="multi_view", path="/Obj", highlight="/Obj", show_colliders=True)
    args = mock_bridge.send.call_args[0][1]
    assert args["show_colliders"] == "true"


@pytest.mark.asyncio
async def test_screenshot_show_colliders_omitted_when_false(mock_bridge):
    """screenshot omits show_colliders when False/None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "img"})
    await screenshot(camera="multi_view", path="/Obj")
    args = mock_bridge.send.call_args[0][1]
    assert "show_colliders" not in args


@pytest.mark.asyncio
async def test_screenshot_single_view_angle(mock_bridge):
    """screenshot passes angle for single_view mode."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "img"})
    await screenshot(camera="single_view", path="/Obj", angle="left")
    args = mock_bridge.send.call_args[0][1]
    assert args["camera"] == "single_view"
    assert args["angle"] == "left"


@pytest.mark.asyncio
async def test_screenshot_angle_omitted_when_none(mock_bridge):
    """screenshot omits angle when None."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "img"})
    await screenshot(camera="multi_view", path="/Obj")
    args = mock_bridge.send.call_args[0][1]
    assert "angle" not in args


@pytest.mark.asyncio
async def test_screenshot_describe_early_return_when_no_file_path(mock_bridge):
    """screenshot with describe= returns verbatim when result has no 'Data saved to:'."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "screenshot failed: camera unavailable"})
    result = await screenshot(describe="auto")
    assert "[img:" not in result
    assert "camera unavailable" in result


# ── main() crash-logging tests ────────────────────────────────────────────────

def test_main_logs_base_exception(monkeypatch):
    """T7: main() calls log_crash for unexpected BaseException."""
    import unity_mcp.server as srv
    logged = []
    monkeypatch.setattr(srv, "_main_transport", "stdio", raising=False)

    with patch("unity_mcp.server.mcp.run", side_effect=RuntimeError("kaboom")):
        with patch("unity_mcp.crash_log.log_crash", side_effect=lambda exc, **kw: logged.append(exc)):
            with pytest.raises(RuntimeError):
                srv.main()
    assert len(logged) == 1
    assert isinstance(logged[0], RuntimeError)


def test_main_does_not_log_keyboard_interrupt():
    """T8: main() swallows KeyboardInterrupt without logging."""
    import unity_mcp.server as srv
    with patch("unity_mcp.server.mcp.run", side_effect=KeyboardInterrupt()):
        with patch("unity_mcp.crash_log.log_crash") as mock_log:
            srv.main()  # must not raise
    mock_log.assert_not_called()


def test_main_does_not_log_system_exit():
    """T9: main() swallows SystemExit without logging."""
    import unity_mcp.server as srv
    with patch("unity_mcp.server.mcp.run", side_effect=SystemExit(0)):
        with patch("unity_mcp.crash_log.log_crash") as mock_log:
            srv.main()  # must not raise
    mock_log.assert_not_called()


def test_main_does_not_log_epipe_oserror():
    """T10: main() swallows OSError(EPIPE) without logging."""
    import unity_mcp.server as srv
    epipe = OSError(errno.EPIPE, "Broken pipe")
    with patch("unity_mcp.server.mcp.run", side_effect=epipe):
        with patch("unity_mcp.crash_log.log_crash") as mock_log:
            srv.main()  # must not raise
    mock_log.assert_not_called()


# ── scene enhancement tools (from test_enhancements.py) ──────────────────────

@pytest.mark.asyncio
async def test_object_diff_sends_args(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "= (identical)"})
    result = await object_diff(path_a="/Julia", path_b="SceneB:/Julia")
    mock_bridge.send.assert_called_once_with(
        "object_diff", {"pathA": "/Julia", "pathB": "SceneB:/Julia"}, timeout=30.0
    )
    assert "identical" in result


@pytest.mark.asyncio
async def test_hierarchy_scene_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Player #1"})
    await get_hierarchy(scene="Gameplay")
    args = mock_bridge.send.call_args[0][1]
    assert args.get("scene") == "Gameplay"


@pytest.mark.asyncio
async def test_hierarchy_no_scene_omitted(mock_bridge):
    """Regression: single-scene callers don't get 'scene' key added."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Player #1"})
    await get_hierarchy()
    args = mock_bridge.send.call_args[0][1]
    assert "scene" not in args


@pytest.mark.asyncio
async def test_search_scene_forwarded(mock_bridge):
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Enemy #2"})
    await search_scene(query="Enemy", scene="Level2")
    args = mock_bridge.send.call_args[0][1]
    assert args.get("scene") == "Level2"


@pytest.mark.asyncio
async def test_search_no_scene_omitted(mock_bridge):
    """Regression: default call without scene must not include scene key."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Enemy #2"})
    await search_scene(query="Enemy")
    args = mock_bridge.send.call_args[0][1]
    assert "scene" not in args


# ---------------------------------------------------------------------------
# PY2.test.1: _send_raw CancelledError → ToolError
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_send_raw_cancelled_error_raises_tool_error(mock_bridge):
    """CancelledError from bridge.send → ToolError('Operation cancelled')."""
    import asyncio
    from unity_mcp.server import _send_raw
    mock_bridge.send = AsyncMock(side_effect=asyncio.CancelledError())
    with pytest.raises(ToolError, match="Operation cancelled"):
        await _send_raw("ping", {})


# ---------------------------------------------------------------------------
# PY2.test.2: ConnectionError → UNITY_UNAVAILABLE ToolError
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_send_raw_connection_error_produces_unity_unavailable_tool_error(mock_bridge):
    """ConnectionRefusedError + busy probe → ToolError with [UNITY_UNAVAILABLE]."""
    from unity_mcp.server import _send_raw
    mock_bridge.send = AsyncMock(side_effect=ConnectionRefusedError("refused"))
    probe = MagicMock()
    probe.has_strong_busy_signal.return_value = True
    probe.estimated_remaining_s.return_value = 5.0
    mock_bridge._probe = probe
    with pytest.raises(ToolError, match=r"\[UNITY_UNAVAILABLE\]"):
        await _send_raw("ping", {})


# ---------------------------------------------------------------------------
# PY2.test.3: _refresh_tools_cache skips when lock already held
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_refresh_tools_cache_skips_when_lock_held():
    """Second call while lock is held must not call bridge.send."""
    import asyncio
    import unity_mcp.server as srv

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})

    orig_lock = srv._refresh_tools_lock
    try:
        lock = asyncio.Lock()
        srv._refresh_tools_lock = lock
        async with lock:  # hold the lock
            await srv._refresh_tools_cache(bridge)
        bridge.send.assert_not_called()
    finally:
        srv._refresh_tools_lock = orig_lock


# ---------------------------------------------------------------------------
# X4.cross.2: main() with UNITY_MCP_TRANSPORT=http
# ---------------------------------------------------------------------------

def test_main_http_transport(monkeypatch):
    """UNITY_MCP_TRANSPORT=http → mcp.run called with transport='streamable-http'."""
    import unity_mcp.server as srv
    monkeypatch.setenv("UNITY_MCP_TRANSPORT", "http")
    with patch("unity_mcp.server.mcp.run") as mock_run:
        srv.main()
    mock_run.assert_called_once_with(
        transport="streamable-http", host="127.0.0.1", port=8765
    )


# ---------------------------------------------------------------------------
# PY1.arch.2: _on_port_change acquire failure must log a warning
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_on_port_change_acquire_failure_logs_warning(monkeypatch):
    """_on_port_change must log.warning when acquire_lock raises on new port."""
    import logging
    import unity_mcp.server as srv

    call_count = [0]
    def fake_acquire(port):
        call_count[0] += 1
        if call_count[0] == 1:
            return 99  # initial acquire succeeds
        raise RuntimeError("lock busy")

    monkeypatch.setattr("unity_mcp.server.acquire_lock", fake_acquire)
    monkeypatch.setattr("unity_mcp.server.release_lock", lambda fd: None)
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")

    captured_slot = [None]

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        _cbs = []

        def __init__(self, **kwargs):
            self._on_port_change = kwargs.get("on_port_change")
            captured_slot[0] = self

        async def connect(self, *a, **kw): return "ok"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", FakeSlot)
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)

    warnings = []

    class CapHandler(logging.Handler):
        def emit(self, record):
            if record.levelno >= logging.WARNING:
                warnings.append(record.getMessage())

    handler = CapHandler()
    log = logging.getLogger("unity_mcp.server")
    log.addHandler(handler)
    log.setLevel(logging.WARNING)
    try:
        class FakeApp: pass
        async with srv.lifespan(FakeApp()):
            captured_slot[0]._on_port_change(9500, 9501)
    except Exception:
        pass
    finally:
        log.removeHandler(handler)

    assert warnings, "Expected a warning when acquire_lock fails on port change"
    assert any("9501" in w or "lock" in w.lower() or "port" in w.lower() for w in warnings)


# ---------------------------------------------------------------------------
# PY2.test.4: _on_reconnect debounce limits rapid refresh calls
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_lifespan_on_reconnect_throttles_refresh(monkeypatch):
    """Two reconnect callbacks within <5s must result in at most one refresh call."""
    import asyncio
    import unity_mcp.server as srv

    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")

    bridge = AsyncMock()
    bridge.connected = True
    bridge.send = AsyncMock(return_value={"ok": True, "data": ""})
    bridge.start_heartbeat = MagicMock()
    bridge.stop_heartbeat = MagicMock()

    reconnect_cbs = []

    _bridge = bridge

    class FakeSlot:
        def __init__(self, **kwargs): pass

        @property
        def bridge(self): return _bridge
        connected = True
        port = 9500

        def add_reconnect_callback(self, cb): reconnect_cbs.append(cb)
        async def connect(self, *a, **kw): return "ok"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", FakeSlot)
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)

    class FakeApp: pass
    async with srv.lifespan(FakeApp()):
        # Reset call count after initial connect
        bridge.send.reset_mock()
        # Fire reconnect twice rapidly
        for cb in reconnect_cbs:
            cb()
        for cb in reconnect_cbs:
            cb()
        await asyncio.sleep(0.05)

    disabled_calls = [c for c in bridge.send.call_args_list if c[0][0] == "get_disabled_tools"]
    # Debounce: 2 rapid fires → at most 1 refresh sent
    assert len(disabled_calls) <= 1, f"Debounce failed: {len(disabled_calls)} calls"
