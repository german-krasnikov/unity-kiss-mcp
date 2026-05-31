"""Live TCP tests for component lifecycle (add/set_ref/remove).

Requires Unity Editor running with MCP plugin on port 9500.
Run: pytest tests/live/ -v -m live
"""
import uuid

import pytest

pytestmark = pytest.mark.live


@pytest.mark.asyncio
async def test_full_component_lifecycle(ensure_edit_mode, bridge):
    """Create 2 objects → add component → set ref → verify → remove → cleanup."""
    uid = uuid.uuid4().hex[:8]
    name_a = f"Live_{uid}_A"
    name_b = f"Live_{uid}_B"

    def _ok(result):
        d = result.get("data", "") if isinstance(result, dict) else str(result)
        assert result.get("ok", True), f"Command failed: {result.get('err', d)}"
        return d

    r1 = await bridge.send("create_object", {"name": name_a})
    _ok(r1)
    r2 = await bridge.send("create_object", {"name": name_b})
    _ok(r2)

    try:
        add = await bridge.send("manage_component", {
            "path": f"/{name_a}", "type": "TestRefScript", "action": "add"
        })
        assert "TestRefScript" in _ok(add), _ok(add)

        set_ref = await bridge.send("set_property", {
            "path": f"/{name_a}", "component": "TestRefScript",
            "prop": "target", "value": f"/{name_b}"
        })
        _ok(set_ref)

        get = await bridge.send("get_component", {
            "path": f"/{name_a}", "type": "TestRefScript"
        })
        assert name_b in _ok(get), _ok(get)

        rm = await bridge.send("manage_component", {
            "path": f"/{name_a}", "type": "TestRefScript", "action": "remove"
        })
        _ok(rm)

        verify = await bridge.send("get_component", {
            "path": f"/{name_a}", "type": "TestRefScript"
        })
        d = verify.get("data", "")
        assert "TestRefScript" not in d or not verify.get("ok"), d

    finally:
        for name in (name_a, name_b):
            try:
                await bridge.send("delete_object", {"path": f"/{name}"})
            except Exception:
                pass
