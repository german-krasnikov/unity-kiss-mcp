"""Integration tests for ChatRelay drain pipeline — real Python subprocesses, no Unity.

Uses sys.executable -c '<script>' as a mock CLI, verifying the full path:
  _cmd_spawn → _drain_stdout_loop → _transform_line → _enqueue → _cmd_events
"""
import asyncio
import sys

from unity_mcp.chat_relay import ChatRelay

# ── Mock CLI scripts (executed via sys.executable -c) ────────────────────────

ECHO_STREAM = """\
import sys, json
for d in [
    {"type": "system", "subtype": "init", "session_id": "sid1"},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "Hello!"}}},
    {"type": "result", "is_error": False, "session_id": "sid1",
        "total_cost_usd": 0.001, "usage": {"input_tokens": 10, "output_tokens": 5}},
]:
    print(json.dumps(d), flush=True)
"""

TOOL_RESULT_THEN_TEXT = """\
import sys, json
for d in [
    {"type": "stream_event", "event": {"type": "content_block_start",
        "content_block": {"type": "tool_result"}}},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "SUPPRESSED"}}},
    {"type": "stream_event", "event": {"type": "content_block_stop"}},
    {"type": "stream_event", "event": {"type": "content_block_delta",
        "delta": {"type": "text_delta", "text": "visible text"}}},
    {"type": "result", "is_error": False},
]:
    print(json.dumps(d), flush=True)
"""

EXIT_ZERO = "pass"
EXIT_ONE  = "import sys; sys.exit(1)"
RAW_LINE  = "print('raw_output_line', flush=True)"

# ── Helpers ──────────────────────────────────────────────────────────────────

def event_lines(resp: dict) -> list[str]:
    """Extract text lines from _cmd_events response (skips interleaved seq lines)."""
    parts = resp["data"].splitlines()
    return [parts[i] for i in range(1, len(parts), 2)]


async def wait_for_events(relay: ChatRelay, after: int = -1, timeout: float = 2.0) -> list[str]:
    """Poll _cmd_events until at least one event appears or timeout expires."""
    deadline = asyncio.get_event_loop().time() + timeout
    while asyncio.get_event_loop().time() < deadline:
        resp = await relay._cmd_events({"after_seq": after, "timeout_ms": 200})
        lines = event_lines(resp)
        if lines:
            return lines
        await asyncio.sleep(0.05)
    return []


async def spawn(relay: ChatRelay, script: str, transform: bool = True) -> None:
    relay._transform = transform
    await relay._cmd_spawn({
        "binary": sys.executable,
        "argv": ["-c", script],
        "env_set": {},
        "env_strip": [],
    })

# ── Tests ────────────────────────────────────────────────────────────────────

async def test_pipeline_text_arrives_as_pipe_format():
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    lines = await wait_for_events(relay)
    assert any(l == "si|sid1" for l in lines)
    assert any(l == "t|Hello!" for l in lines)
    assert any(l.startswith("d|") for l in lines)
    await relay._kill_current()


async def test_pipeline_no_raw_json_with_transform():
    """transform=True must not pass raw JSON lines to the event buffer."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    lines = await wait_for_events(relay)
    assert not any(l.startswith("{") for l in lines)
    await relay._kill_current()


async def test_pipeline_tool_result_text_suppressed():
    relay = ChatRelay()
    await spawn(relay, TOOL_RESULT_THEN_TEXT)
    lines = await wait_for_events(relay)
    assert not any("SUPPRESSED" in l for l in lines)
    assert any(l == "t|visible text" for l in lines)
    await relay._kill_current()


async def test_pipeline_clean_exit_produces_done_event():
    relay = ChatRelay()
    await spawn(relay, EXIT_ZERO)
    lines = await wait_for_events(relay)
    assert any(l.startswith("d|") for l in lines)
    assert not any(l.startswith("e|") for l in lines)
    await relay._kill_current()


async def test_pipeline_exit1_produces_error_event():
    relay = ChatRelay()
    await spawn(relay, EXIT_ONE)
    lines = await wait_for_events(relay)
    assert any(l.startswith("e|") for l in lines)
    await relay._kill_current()


async def test_pipeline_transform_false_passes_raw():
    """transform=False: raw stdout lines land verbatim in the event buffer."""
    relay = ChatRelay()
    await spawn(relay, RAW_LINE, transform=False)
    lines = await wait_for_events(relay)
    assert any("raw_output_line" in l for l in lines)
    assert not any(l.startswith("t|") for l in lines)
    await relay._kill_current()


async def test_pipeline_seq_monotonic_across_respawns():
    """Seq numbers never reset between kills — C# lastSeq filter stays valid."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)
    seq_after_first = relay._next_seq

    await relay._kill_current()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)

    assert relay._next_seq > seq_after_first
    await relay._kill_current()


async def test_pipeline_kill_clears_buffer():
    """kill() clears the event buffer to prevent cross-session event leakage."""
    relay = ChatRelay()
    await spawn(relay, ECHO_STREAM)
    await wait_for_events(relay)
    assert len(relay._buf) > 0

    await relay._kill_current()
    assert len(relay._buf) == 0
