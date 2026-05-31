import pytest
from unity_mcp.server import batch


@pytest.mark.asyncio
async def test_batch_passes_timeout_ms(mock_bridge, bridge_response):
    """batch() must include timeout_ms in args sent to bridge."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    args = mock_bridge.send.call_args[0][1]
    assert "timeout_ms" in args


@pytest.mark.asyncio
async def test_batch_default_timeout_30s(mock_bridge, bridge_response):
    """Default timeout=30 → timeout_ms=25000 (30-5)*1000."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy")
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == 25000


@pytest.mark.asyncio
async def test_batch_custom_timeout(mock_bridge, bridge_response):
    """timeout=60 → timeout_ms=55000."""
    bridge_response(data="ok:1")
    await batch(commands="get_hierarchy", timeout=60.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["timeout_ms"] == 55000
