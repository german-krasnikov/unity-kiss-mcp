"""Tests for playtest result summarization via Haiku (P2)."""
from unittest.mock import AsyncMock, patch, MagicMock


async def test_playtest_short_result_not_summarized(mock_bridge):
    """Results under 300 chars are returned as-is, no Haiku call."""
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 3/3"}
    from unity_mcp.server import run_playtest

    with patch("unity_mcp.tools.runtime.SamplingService") as MockSvc:
        result = await run_playtest("LOG hi")

    MockSvc.assert_not_called()
    assert "PASS: 3/3" in result


async def test_playtest_long_result_summarized_when_enabled(mock_bridge):
    """Results over 300 chars get summarized via Haiku when enabled."""
    long_report = "PLAYTEST: 1/3 (2.1s)\n" + "[FAIL] " * 50 + "\nmore details\n" * 10
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    with patch("unity_mcp.tools.runtime.SamplingService") as MockSvc:
        instance = MockSvc.return_value
        instance.enabled = True
        instance.summarize = AsyncMock(return_value="1/3 FAIL: assertion mismatch")
        result = await run_playtest("LOG hi")

    assert result == "1/3 FAIL: assertion mismatch"


async def test_playtest_long_result_kept_when_disabled(mock_bridge):
    """Results over 300 chars kept as-is when SamplingService disabled."""
    long_report = "PLAYTEST: 1/3\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    with patch("unity_mcp.tools.runtime.SamplingService") as MockSvc:
        instance = MockSvc.return_value
        instance.enabled = False
        result = await run_playtest("LOG hi")

    instance.summarize.assert_not_called()
    assert "PLAYTEST" in result


async def test_playtest_summarize_fallback_on_none(mock_bridge):
    """If summarize() returns None, return compressed original."""
    long_report = "PLAYTEST: 1/3\n" + "x" * 350
    mock_bridge.send.return_value = {"ok": True, "data": long_report}
    from unity_mcp.server import run_playtest

    with patch("unity_mcp.tools.runtime.SamplingService") as MockSvc:
        instance = MockSvc.return_value
        instance.enabled = True
        instance.summarize = AsyncMock(return_value=None)
        result = await run_playtest("LOG hi")

    assert "PLAYTEST" in result
