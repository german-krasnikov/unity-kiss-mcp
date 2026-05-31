"""Tests for run_playtest tool."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.server import run_playtest
from unity_mcp.tools.runtime import _compress_report


@pytest.mark.asyncio
async def test_run_playtest_sends_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS: 3 steps"}
    script = "WAIT 1\nASSERT_CONSOLE_CLEAN"
    result = await run_playtest(script)
    mock_bridge.send.assert_called_once_with(
        "run_playtest",
        {"script": script, "timeout": "120.0"},
        timeout=130.0,
    )
    assert result == "PASS: 3 steps"


@pytest.mark.asyncio
async def test_run_playtest_timeout_passthrough(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("WAIT 1", timeout=60.0)
    call = mock_bridge.send.call_args
    assert call[0][1]["timeout"] == "60.0"
    assert call[1]["timeout"] == 70.0


@pytest.mark.asyncio
async def test_run_playtest_default_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PASS"}
    await run_playtest("LOG hi")
    call = mock_bridge.send.call_args
    assert call[0][1]["timeout"] == "120.0"
    assert call[1]["timeout"] == 130.0


def test_compress_report_all_pass_returns_compact():
    report = "PLAYTEST: 3/3 (1.2s) OK"
    assert _compress_report(report) == report


def test_compress_report_strips_passing_lines():
    report = "PLAYTEST: 2/3 (1.0s)\n[1] ASSERT HP==100 — PASS (100)\n[2] ASSERT Money>500 — FAIL (100)\n[3] LOG check"
    result = _compress_report(report)
    assert "FAIL" in result
    assert "LOG check" in result
    assert "PASS" not in result


def test_compress_report_short_passthrough():
    assert _compress_report("OK") == "OK"
    assert _compress_report("") == ""


def test_compress_report_snapshot_kept():
    report = "PLAYTEST: 2/2 (1.0s)\n[1] SNAPSHOT\nhp=100\n[2] ASSERT HP==100 — PASS"
    result = _compress_report(report)
    assert "SNAPSHOT" in result
    assert "hp=100" in result
