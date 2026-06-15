"""TDD: run_tests fire-and-poll for EditMode + PlayMode (Phase 2)."""
import asyncio
import unity_mcp.tools.scene as scene_mod



def _make_send(responses: list):
    """Build an async _send that pops from responses list sequentially.

    Each item is either:
      - an exception instance → raise it
      - a string → return it
    """
    it = iter(responses)

    async def _send(cmd, args, **kw):
        val = next(it)
        if isinstance(val, Exception):
            raise val
        return val

    return _send


# ─── 1. Poll fallback after ConnectionError (EditMode) ────────────────────────

async def test_run_tests_editmode_polls_after_connection_error(monkeypatch):
    """TCP dies (ConnectionError) on run_tests → polls get_test_results until result."""
    responses = [
        ConnectionError("TCP closed"),  # run_tests call dies
        "pending",                       # poll 1
        "pending",                       # poll 2
        "tests: 42 passed",              # poll 3 → done
    ]
    monkeypatch.setattr(scene_mod, "_send", _make_send(responses))
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "42 passed" in result


async def test_run_tests_playmode_polls_after_connection_error(monkeypatch):
    """Same behaviour for PlayMode."""
    responses = [
        ConnectionError("TCP closed"),
        "pending",
        "tests: 5 passed",
    ]
    monkeypatch.setattr(scene_mod, "_send", _make_send(responses))
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)

    result = await scene_mod.run_tests(mode="PlayMode")
    assert "5 passed" in result


# ─── 2. Poll fallback after TimeoutError ─────────────────────────────────────

async def test_run_tests_polls_after_timeout_error(monkeypatch):
    """asyncio.TimeoutError on run_tests → falls through to poll."""
    responses = [
        asyncio.TimeoutError(),
        "tests: 1 passed",
    ]
    monkeypatch.setattr(scene_mod, "_send", _make_send(responses))
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "1 passed" in result


# ─── 3. Immediate return when result contains "tests:" ────────────────────────

async def test_run_tests_returns_immediately_on_full_result(monkeypatch):
    """_send returns full result synchronously (no domain reload) → no polling."""
    call_log = []

    async def _send(cmd, args, **kw):
        call_log.append(cmd)
        if cmd == "run_tests":
            return "tests: 100 passed, 0 failed"
        return "should not reach"

    monkeypatch.setattr(scene_mod, "_send", _send)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "100 passed" in result
    assert call_log == ["run_tests"]  # no get_test_results call


# ─── 4. Poll timeout → error message ─────────────────────────────────────────

async def test_run_tests_poll_timeout_returns_error(monkeypatch):
    """All polls return 'pending' → returns error string after exhausting attempts."""
    call_count = 0

    async def _send(cmd, args, **kw):
        nonlocal call_count
        call_count += 1
        if cmd == "run_tests":
            raise ConnectionError("TCP closed")
        return "pending"  # all polls pending

    monkeypatch.setattr(scene_mod, "_send", _send)
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)
    monkeypatch.setattr(scene_mod, "_POLL_ATTEMPTS", 3)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "Error" in result
    assert "EditMode" in result


# ─── 5. Poll survives transient reconnect failures ────────────────────────────

async def test_run_tests_poll_ignores_transient_errors(monkeypatch):
    """get_test_results raises ConnectionError twice, then returns result → still works."""
    responses = [
        ConnectionError("run_tests TCP dies"),  # initial call
        ConnectionError("poll transient"),       # poll 1
        ConnectionError("poll transient"),       # poll 2
        "tests: 7 passed",                       # poll 3 → done
    ]
    monkeypatch.setattr(scene_mod, "_send", _make_send(responses))
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "7 passed" in result


# ─── 6. filter param is forwarded ────────────────────────────────────────────

async def test_run_tests_filter_forwarded(monkeypatch):
    """filter arg is included in the run_tests command args."""
    captured = {}

    async def _send(cmd, args, **kw):
        captured[cmd] = args
        if cmd == "run_tests":
            return "tests: 3 passed"
        return "pending"

    monkeypatch.setattr(scene_mod, "_send", _send)

    await scene_mod.run_tests(mode="EditMode", filter="MyTest|OtherTest")
    assert captured["run_tests"]["filter"] == "MyTest|OtherTest"


# ─── 7. "none" treated same as "pending" ────────────────────────────────────

async def test_run_tests_poll_treats_none_as_pending(monkeypatch):
    """get_test_results returning 'none' keeps polling until real result."""
    responses = [
        ConnectionError("TCP closed"),
        "none",
        "none",
        "tests: 1 passed",
    ]
    monkeypatch.setattr(scene_mod, "_send", _make_send(responses))
    monkeypatch.setattr(scene_mod, "_POLL_INTERVAL", 0.0)

    result = await scene_mod.run_tests(mode="EditMode")
    assert "1 passed" in result
