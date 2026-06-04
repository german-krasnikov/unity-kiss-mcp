"""Live integration test: search_scene with root scope. F13 Scenario 26.
Requires Unity Editor running with MCP plugin on :9500.
Run with: pytest -m live
"""
import uuid
import pytest
from unity_mcp.tools.scene import search_scene

pytestmark = pytest.mark.live


@pytest.mark.asyncio
async def test_live_search_scene_scoped(mock_bridge):
    """Create parent + children, search with root — only children returned, not siblings."""
    parent_name = f"Live_F13_Parent_{uuid.uuid4().hex[:6]}"
    child_name = f"Live_F13_Child_{uuid.uuid4().hex[:6]}"
    sibling_name = f"Live_F13_Sibling_{uuid.uuid4().hex[:6]}"

    from unity_mcp.tools.scene import _send, _args
    # Create objects
    await _send("create_object", _args(name=parent_name))
    await _send("create_object", _args(name=child_name, parent="/" + parent_name))
    await _send("create_object", _args(name=sibling_name))

    try:
        result = await search_scene(query="Live_F13_", root="/" + parent_name)
        assert child_name in result, f"Child not found in scoped result: {result}"
        assert sibling_name not in result, f"Sibling leaked into scoped result: {result}"
    finally:
        # Cleanup
        await _send("delete_object", _args(path="/" + parent_name))
        await _send("delete_object", _args(path="/" + sibling_name))
