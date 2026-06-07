"""Tests for wrap_send dict-response extraction — specifically the file+data path."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware_pipeline import wrap_send


@pytest.mark.asyncio
async def test_wrap_send_file_and_data_combined():
    """wrap_send must return both manifest text AND file path when response has both."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "data": "FRONT:Player(vis)\nLEFT:Player(vis)", "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert "FRONT:Player(vis)" in result
    assert "Data saved to: /tmp/mv.png" in result


@pytest.mark.asyncio
async def test_wrap_send_file_only_no_data():
    """wrap_send with only 'file' key (no data) must return just the path string."""
    async def fake_send(cmd, args, timeout=30.0):
        return {"ok": True, "file": "/tmp/mv.png"}

    wrapped = wrap_send(fake_send)
    result = await wrapped("screenshot", {})
    assert result == "Data saved to: /tmp/mv.png"
