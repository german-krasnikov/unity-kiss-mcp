"""Shared helpers and fixtures for relay-based live tests."""
from __future__ import annotations

import asyncio
import json
import os
import struct
import sys
from typing import AsyncGenerator

import pytest

RELAY_MODULE = "unity_mcp.chat_relay"
MCP_PORT     = int(os.environ.get("UNITY_MCP_PORT", "9602"))
PROMPT_TEXT  = "Say just the word 'hello' and nothing else."


def _turn_line(text: str) -> str:
    """Stream-json user-turn envelope (Claude/Kimi stdin format)."""
    payload = {"type": "user", "message": {
        "role": "user", "content": [{"type": "text", "text": text}],
    }}
    return json.dumps(payload, ensure_ascii=False)


async def _relay_cmd(port: int, cmd: str, args: dict | None = None) -> dict:
    r, w = await asyncio.open_connection("127.0.0.1", port)
    msg = json.dumps({"cmd": cmd, "args": args or {}}).encode()
    w.write(struct.pack(">I", len(msg)) + msg)
    await w.drain()
    raw_len = await r.readexactly(4)
    data = await r.readexactly(struct.unpack(">I", raw_len)[0])
    w.close()
    return json.loads(data)


def _parse_events(data: str) -> list[str]:
    """Parse 'seq\\ntext\\n...' format → list of pipe-format strings."""
    lines = data.splitlines()
    out = []
    i = 0
    while i + 1 < len(lines):
        out.append(lines[i + 1])
        i += 2
    return out


async def _poll_until_done(port: int, timeout: float = 90.0) -> list[str]:
    """Poll relay for events until d| or e| seen, or timeout."""
    events: list[str] = []
    seq = -1
    deadline = asyncio.get_running_loop().time() + timeout
    while asyncio.get_running_loop().time() < deadline:
        resp = await _relay_cmd(port, "events", {"after_seq": seq, "timeout_ms": 3000})
        if resp.get("ok") and resp.get("data"):
            new = _parse_events(resp["data"])
            if new:
                events.extend(new)
                raw_lines = resp["data"].splitlines()
                for j in range(0, len(raw_lines) - 1, 2):
                    try:
                        seq = max(seq, int(raw_lines[j]))
                    except ValueError:
                        pass
        if any(e.startswith("d|") or e.startswith("e|") for e in events):
            return events
    return events


@pytest.fixture
async def relay_port() -> AsyncGenerator[int, None]:
    """Start a relay subprocess, yield its port, kill on teardown."""
    proc = await asyncio.create_subprocess_exec(
        sys.executable, "-m", RELAY_MODULE,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.DEVNULL,
        cwd=os.path.join(os.path.dirname(__file__), "..", ".."),
    )
    port = None
    try:
        async with asyncio.timeout(10):
            while True:
                line = await proc.stdout.readline()
                text = line.decode().strip()
                if text.startswith("relay_port:"):
                    port = int(text.split(":", 1)[1])
                    break
    except (asyncio.TimeoutError, ValueError):
        proc.terminate()
        pytest.skip("Could not start relay or parse port")
        return

    yield port

    proc.terminate()
    try:
        await asyncio.wait_for(proc.wait(), timeout=5)
    except asyncio.TimeoutError:
        proc.kill()
