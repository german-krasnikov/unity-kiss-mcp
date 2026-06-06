"""Tests for P3 macro tools: setup_objects, set_properties, configure_objects."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mock_send():
    """Mock _send that captures batch commands arg."""
    send = AsyncMock(return_value="batch_result")
    return send


@pytest.fixture(autouse=True)
def patch_send(mock_send):
    """Inject mock_send into autobatch module."""
    import unity_mcp.tools.autobatch as ab
    ab._send = mock_send
    yield
    ab._send = None


def _batch_cmds(mock_send) -> str:
    """Extract 'commands' from last batch call."""
    return mock_send.call_args[0][1]["commands"]


# ── setup_objects ─────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_setup_objects_single(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("MyObj")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=MyObj" in cmds
    assert "inspect paths=/MyObj" in cmds


@pytest.mark.asyncio
async def test_setup_objects_with_position(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("MyObj pos=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/MyObj component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds


@pytest.mark.asyncio
async def test_setup_objects_with_primitive(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("NPC1 primitive=Capsule")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=NPC1 primitive=Capsule" in cmds


@pytest.mark.asyncio
async def test_setup_objects_with_components(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("NPC1 components=Health,Rigidbody")
    cmds = _batch_cmds(mock_send)
    assert "manage_component path=/NPC1 type=Health action=add" in cmds
    assert "manage_component path=/NPC1 type=Rigidbody action=add" in cmds


@pytest.mark.asyncio
async def test_setup_objects_multiple(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("A\nB primitive=Cube")
    cmds = _batch_cmds(mock_send)
    assert "create_object name=A" in cmds
    assert "create_object name=B primitive=Cube" in cmds
    # inspect should contain both
    assert "/A" in cmds
    assert "/B" in cmds


@pytest.mark.asyncio
async def test_setup_objects_empty(mock_send):
    from unity_mcp.tools.autobatch import setup_objects
    result = await setup_objects("   ")
    # Should not call send at all, return error message
    mock_send.assert_not_called()
    assert "No" in result


@pytest.mark.asyncio
async def test_setup_objects_parent_double_slash_normalized(mock_send):
    """parent path containing // is not double-slashed in generated commands."""
    from unity_mcp.tools.autobatch import setup_objects
    # parent already has leading slash — should not produce //
    await setup_objects("Enemy parent=/World/Enemies")
    cmds = _batch_cmds(mock_send)
    assert "//" not in cmds
    assert "create_object name=Enemy" in cmds
    assert "parent=/World/Enemies" in cmds


# ── set_properties ────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_set_properties_single(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/NPC1 component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds


@pytest.mark.asyncio
async def test_set_properties_multiple(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0)\nRigidbody.mass=5\nTransform.m_LocalScale=(2,2,2)")
    cmds = _batch_cmds(mock_send)
    assert cmds.count("set_property") == 3
    # read-back for Transform and Rigidbody
    assert "get_component path=/NPC1 type=Rigidbody" in cmds
    assert "get_component path=/NPC1 type=Transform" in cmds


@pytest.mark.asyncio
async def test_set_properties_semicolons(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    await set_properties("/NPC1", "Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5")
    cmds = _batch_cmds(mock_send)
    assert cmds.count("set_property") == 2


@pytest.mark.asyncio
async def test_set_properties_invalid(mock_send):
    from unity_mcp.tools.autobatch import set_properties
    result = await set_properties("/NPC1", "no_dot_here=value")
    mock_send.assert_not_called()
    assert "No valid" in result


# ── configure_objects ─────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_configure_objects_multi(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/NPC1 Transform.m_LocalPosition=(1,0,0)\n/NPC2 Transform.m_LocalPosition=(3,0,0)")
    cmds = _batch_cmds(mock_send)
    assert "set_property path=/NPC1 component=Transform prop=m_LocalPosition value=(1,0,0)" in cmds
    assert "set_property path=/NPC2 component=Transform prop=m_LocalPosition value=(3,0,0)" in cmds
    assert "inspect paths=" in cmds


@pytest.mark.asyncio
async def test_configure_objects_invalid(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    result = await configure_objects("NPC1 Transform.pos=(1,0,0)")  # no leading /
    mock_send.assert_not_called()
    assert "No valid" in result


@pytest.mark.asyncio
async def test_configure_objects_multiple_props(mock_send):
    from unity_mcp.tools.autobatch import configure_objects
    await configure_objects("/NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100")
    cmds = _batch_cmds(mock_send)
    assert "component=Transform" in cmds
    assert "component=Health" in cmds
