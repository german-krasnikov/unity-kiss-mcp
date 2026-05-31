"""F07: get_component/inspect `fields=` projection wiring (tool level)."""
import pytest
from unity_mcp.tools import objects


def _args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


@pytest.fixture
def fake_send(monkeypatch):
    sent = {}
    COMP = "[Transform]\nm_LocalPosition.x: 5\nm_Mass: 2\nm_Drag: 0\n"

    async def send_fn(cmd, args, timeout=30.0):
        sent["cmd"], sent["args"] = cmd, args
        return COMP

    monkeypatch.setattr(objects, "_send", send_fn)
    monkeypatch.setattr(objects, "_args", _args)
    return sent


@pytest.mark.asyncio
async def test_get_component_fields_projects_and_sets_no_strip(fake_send):
    result = await objects.get_component("/Cube", "Transform", fields="m_Mass")
    assert fake_send["args"].get("_no_strip") is True, "explicit fields must bypass default-stripping"
    assert "m_Mass: 2" in result
    assert "m_LocalPosition" not in result and "m_Drag" not in result


@pytest.mark.asyncio
async def test_get_component_no_fields_no_strip_flag(fake_send):
    await objects.get_component("/Cube", "Transform")
    assert "_no_strip" not in fake_send["args"], "default path must not force _no_strip"


@pytest.mark.asyncio
async def test_inspect_fields_projects(fake_send):
    result = await objects.inspect("/A,/B", fields="m_Mass")
    assert fake_send["args"].get("_no_strip") is True
    assert "m_Mass: 2" in result
    assert "m_Drag" not in result
