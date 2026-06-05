"""Crash/disconnect logger tests — TDD Red phase."""
import json
import time
import asyncio
import pytest
from pathlib import Path
from unittest.mock import MagicMock, patch, AsyncMock

from unity_mcp.crash_log import CrashLogger
from unity_mcp.bridge import UnityBridge


# ── Group 1: CrashLogger unit tests ──────────────────────────────────────────

def test_creates_log_dir_and_file(tmp_path):
    log_dir = tmp_path / "crash_logs"
    assert not log_dir.exists()
    CrashLogger(log_dir=log_dir)
    assert log_dir.exists()
    assert (log_dir / "crash.jsonl").exists()


def test_log_disconnect_writes_jsonl(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_disconnect(cmd="set_property", retry=0, error_type="ConnectionError",
                          unity_busy=False, port=9500)
    lines = (tmp_path / "crash.jsonl").read_text().strip().splitlines()
    assert len(lines) == 1
    entry = json.loads(lines[0])
    assert entry["ev"] == "disconnect"
    assert entry["cmd"] == "set_property"
    assert entry["retry"] == 0
    assert entry["err"] == "ConnectionError"
    assert entry["busy"] is False
    assert entry["port"] == 9500


def test_log_reconnect_writes_jsonl(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_reconnect(outage_s=6.66, retries=2, port=9500)
    entry = json.loads((tmp_path / "crash.jsonl").read_text().strip())
    assert entry["ev"] == "reconnect"
    assert abs(entry["outage_s"] - 6.66) < 0.01
    assert entry["retries"] == 2
    assert entry["port"] == 9500


def test_entries_have_timestamp(tmp_path):
    before = time.time()
    logger = CrashLogger(log_dir=tmp_path)
    logger.log_disconnect(cmd="x", retry=0, error_type="E", unity_busy=False, port=9500)
    after = time.time()
    entry = json.loads((tmp_path / "crash.jsonl").read_text().strip())
    assert before - 1 <= entry["t"] <= after + 1


def test_rotation_truncates_on_init(tmp_path):
    log_file = tmp_path / "crash.jsonl"
    # Write 600 lines
    lines = [json.dumps({"ev": "disconnect", "i": i}) for i in range(600)]
    log_file.write_text("\n".join(lines) + "\n")

    logger = CrashLogger(log_dir=tmp_path, max_entries=500)
    count = len(log_file.read_text().strip().splitlines())
    assert count == 250  # max_entries // 2


def test_no_rotation_under_limit(tmp_path):
    log_file = tmp_path / "crash.jsonl"
    lines = [json.dumps({"ev": "disconnect", "i": i}) for i in range(100)]
    log_file.write_text("\n".join(lines) + "\n")

    CrashLogger(log_dir=tmp_path, max_entries=500)
    count = len(log_file.read_text().strip().splitlines())
    assert count == 100


def test_close_idempotent(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.close()
    logger.close()  # must not raise


def test_write_after_close_no_crash(tmp_path):
    logger = CrashLogger(log_dir=tmp_path)
    logger.close()
    # All methods must silently drop — no exception
    logger.log_disconnect(cmd="x", retry=0, error_type="E", unity_busy=False, port=9500)
    logger.log_reconnect(outage_s=1.0, retries=1, port=9500)


# ── Group 2: Bridge integration ───────────────────────────────────────────────

def test_bridge_creates_crash_logger():
    bridge = UnityBridge("127.0.0.1", 9999)
    from unity_mcp.crash_log import CrashLogger
    assert isinstance(bridge._crash_log, CrashLogger)


@pytest.mark.asyncio
async def test_disconnect_logs_to_crash_log():
    bridge = UnityBridge("127.0.0.1", 9999)
    mock_log = MagicMock()
    bridge._crash_log = mock_log

    # simulate send failing with ConnectionRefusedError immediately
    with patch("asyncio.open_connection", side_effect=ConnectionRefusedError("refused")):
        with pytest.raises((ConnectionError, TimeoutError)):
            await bridge.send("ping", {})

    assert mock_log.log_disconnect.called
    call_kwargs = mock_log.log_disconnect.call_args.kwargs
    assert call_kwargs["cmd"] == "ping"
    assert call_kwargs["error_type"] == "ConnectionRefusedError"
    assert call_kwargs["port"] == 9999
