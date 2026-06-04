"""Live integration test: search_scene with root scope. F13 Scenario 26.
Requires Unity Editor running with MCP plugin on :9500.
Run with: pytest -m live
"""
import uuid
import pytest

from tests.live.conftest import strip_markers, _destroy

pytestmark = pytest.mark.live


@pytest.mark.asyncio
async def test_live_search_scene_scoped(bridge):
    """Create parent + child + sibling; search with root — only child returned."""
    parent = f"Live_F13_Parent_{uuid.uuid4().hex[:6]}"
    child = f"Live_F13_Child_{uuid.uuid4().hex[:6]}"
    sibling = f"Live_F13_Sibling_{uuid.uuid4().hex[:6]}"

    await bridge.send("create_object", {"name": parent})
    await bridge.send("create_object", {"name": child, "parent": "/" + parent})
    await bridge.send("create_object", {"name": sibling})
    try:
        result = await bridge.send("search_scene", {"query": "Live_F13_", "root": "/" + parent})
        data = strip_markers(result.get("data", "") if isinstance(result, dict) else str(result))
        assert child in data, f"Child not found in scoped result: {data}"
        assert sibling not in data, f"Sibling leaked into scoped result: {data}"
    finally:
        await _destroy(bridge, parent)
        await _destroy(bridge, sibling)
