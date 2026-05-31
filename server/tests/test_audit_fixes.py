"""TDD tests for audit fix findings (batch fixes 2-28)."""
import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


# ── Fix 2+3: lifespan lock lifecycle ──────────────────────────────────────────

@pytest.mark.asyncio
async def test_lifespan_raises_when_acquire_lock_fails(monkeypatch):
    """Fix 2+3: When acquire_lock raises, lifespan must re-raise (not continue silently)."""
    from unity_mcp import server as srv

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda: FakeSlot())
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setattr("unity_mcp.server.acquire_lock",
                        lambda **kw: (_ for _ in ()).throw(RuntimeError("lock busy")))

    class FakeApp: pass
    with pytest.raises(RuntimeError, match="lock busy"):
        async with srv.lifespan(FakeApp()):
            pass


@pytest.mark.asyncio
async def test_lifespan_releases_lock_on_normal_exit(monkeypatch):
    """Fix 2+3: lock_fd released in finally even on normal exit."""
    from unity_mcp import server as srv

    released = []
    fake_fd = object()

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda: FakeSlot())
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setattr("unity_mcp.server.acquire_lock", lambda **kw: fake_fd)
    monkeypatch.setattr("unity_mcp.server.release_lock", lambda fd: released.append(fd))

    class FakeApp: pass
    async with srv.lifespan(FakeApp()):
        pass

    assert released == [fake_fd]


# ── Fix 4: reconnect callbacks on new bridge ──────────────────────────────────

def test_connection_slot_stores_callbacks():
    """Fix 4: ConnectionSlot must expose a way to register/re-register reconnect callbacks."""
    from unity_mcp.connection_slot import ConnectionSlot
    s = ConnectionSlot()
    cb = MagicMock()
    s.add_reconnect_callback(cb)
    assert cb in s._reconnect_callbacks


@pytest.mark.asyncio
async def test_connection_slot_registers_callbacks_on_new_bridge():
    """Fix 4: callbacks registered on slot are applied to every new bridge created by connect()."""
    from unity_mcp.connection_slot import ConnectionSlot

    b1 = MagicMock()
    b1.connect = AsyncMock()
    b1.close = AsyncMock()
    b1.connected = True
    b1.stop_heartbeat = MagicMock()
    b1.add_reconnect_callback = MagicMock()

    b2 = MagicMock()
    b2.connect = AsyncMock()
    b2.close = AsyncMock()
    b2.connected = True
    b2.stop_heartbeat = MagicMock()
    b2.add_reconnect_callback = MagicMock()

    bridges = iter([b1, b2])
    cb = MagicMock()

    with patch("unity_mcp.connection_slot.UnityBridge", side_effect=lambda h, p: next(bridges)):
        s = ConnectionSlot()
        s.add_reconnect_callback(cb)
        await s.connect(9500)
        # callbacks must be registered on b1
        b1.add_reconnect_callback.assert_called_with(cb)

        # reconnect to new port → b2 must also get callbacks
        await s.connect(9501)
        b2.add_reconnect_callback.assert_called_with(cb)


# ── Fix 5: globals declaration ─────────────────────────────────────────────────

def test_lifespan_global_budget_tracker_declared():
    """Fix 5: _budget_tracker and _budget_router must be in server module globals."""
    import unity_mcp.server as srv
    # They exist as module-level names (may be None before lifespan)
    assert hasattr(srv, "_budget_tracker")
    # The key assertion: lifespan uses `global` for these names (code inspection)
    import inspect
    src = inspect.getsource(srv.lifespan)
    assert "_budget_tracker" in src
    assert "_budget_router" in src
    # After the `global` line in lifespan, both names must appear
    global_line = next(l for l in src.splitlines() if l.strip().startswith("global "))
    assert "_budget_tracker" in global_line
    assert "_budget_router" in global_line


# ── Fix 6: CircuitBreaker HALF_OPEN probe flag ────────────────────────────────

def test_circuit_breaker_half_open_allows_only_one_probe():
    """Fix 6: In HALF_OPEN, only first request passes; subsequent ones are blocked."""
    from unity_mcp.middleware import CircuitBreaker
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()  # → OPEN
    # Force to HALF_OPEN directly (avoids time.time() race)
    cb.state = CircuitBreaker.HALF_OPEN
    cb._probe_in_flight = False
    assert cb.allow_request()  # first request in HALF_OPEN → sets probe flag, allowed
    # second request in HALF_OPEN must be blocked
    assert not cb.allow_request()


def test_circuit_breaker_half_open_resets_after_success():
    """Fix 6: After probe succeeds (record_success), circuit is CLOSED again."""
    from unity_mcp.middleware import CircuitBreaker
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    cb.state = CircuitBreaker.HALF_OPEN
    cb._probe_in_flight = False
    cb.allow_request()  # probe
    cb.record_success()
    assert cb.state == CircuitBreaker.CLOSED
    assert cb.allow_request()  # CLOSED → allowed


# ── Fix 7: reset_session completeness ─────────────────────────────────────────

def test_reset_session_clears_is_playing():
    """Fix 7: reset_session must reset is_playing."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.is_playing = True
    mw.reset_session()
    assert mw.is_playing is False


def test_reset_session_resets_circuit_breaker():
    """Fix 7: reset_session must reset the circuit breaker to CLOSED."""
    from unity_mcp.middleware import Middleware, CircuitBreaker
    mw = Middleware()
    mw.circuit.state = CircuitBreaker.OPEN
    mw.circuit.failures = 5
    mw.reset_session()
    assert mw.circuit.state == CircuitBreaker.CLOSED
    assert mw.circuit.failures == 0


def test_reset_session_clears_schema_cache():
    """Fix 7: reset_session must invalidate schema_cache if present."""
    from unity_mcp.middleware import Middleware
    from unity_mcp.schema_cache import SchemaCache
    from unity_mcp.schema_guard import SchemaGuard
    mw = Middleware()
    # Ensure schema_cache exists regardless of env var
    if mw.schema_cache is None:
        mw.schema_cache = SchemaCache()
        mw.schema_guard = SchemaGuard(mw, mw.schema_cache)
    mw.schema_cache.put("Rigidbody", frozenset(["mass"]))
    mw.reset_session()
    assert mw.schema_cache.get("Rigidbody") is None


def test_reset_session_clears_component_cache():
    """Fix 7: reset_session must clear _component_cache."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw._component_cache["/Cube"] = {"Transform"}
    mw.reset_session()
    assert len(mw._component_cache) == 0


# ── Fix 8: Component cache update after manage_component ─────────────────────

def test_cache_components_adds_on_manage_component_add():
    """Fix 8: cache_components must update cache when manage_component add succeeds."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw._component_cache["/Cube"] = {"Transform"}
    mw.cache_components(
        "manage_component",
        {"path": "/Cube", "type": "Rigidbody", "action": "add"},
        "ok: added",
    )
    assert "Rigidbody" in mw._component_cache.get("/Cube", set())


def test_cache_components_removes_on_manage_component_remove():
    """Fix 8: cache_components must remove component from cache on remove action."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw._component_cache["/Cube"] = {"Transform", "Rigidbody"}
    mw.cache_components(
        "manage_component",
        {"path": "/Cube", "type": "Rigidbody", "action": "remove"},
        "ok: removed",
    )
    assert "Rigidbody" not in mw._component_cache.get("/Cube", set())


def test_cache_components_clears_on_delete_object():
    """Fix 8: cache_components must remove path from cache on delete_object."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw._component_cache["/Cube"] = {"Transform"}
    mw.cache_components("delete_object", {"path": "/Cube"}, "ok: deleted")
    assert "/Cube" not in mw._component_cache


# ── Fix 9: schema_cache 'Cannot instantiate' ─────────────────────────────────

def test_schema_cache_parse_returns_empty_on_cannot_instantiate():
    """Fix 9: SchemaCache.parse must return empty frozenset for 'Cannot instantiate' text."""
    from unity_mcp.schema_cache import SchemaCache
    result = SchemaCache.parse("Cannot instantiate abstract class Component")
    assert result == frozenset()


def test_schema_cache_parse_type_not_found_still_empty():
    """Fix 9: existing 'Type not found' guard still works."""
    from unity_mcp.schema_cache import SchemaCache
    result = SchemaCache.parse("Type not found: Foo")
    assert result == frozenset()


# ── Fix 10: animator_intent sanitizes target ──────────────────────────────────

def test_animator_intent_sanitizes_target():
    """Fix 10: animator_intent must sanitize the target param to strip injection chars."""
    from unity_mcp.tools.animator_intent_tool import animator_intent
    from unity_mcp.tools.intent_common import sanitize_intent
    # sanitize_intent strips {} from target
    raw_target = "/Player{injection}"
    sanitized = sanitize_intent(raw_target)
    assert "{" not in sanitized
    assert "}" not in sanitized


def test_vfx_intent_sanitizes_target():
    """Fix 10: vfx_intent prompt must apply sanitize_intent to target param."""
    from unity_mcp.tools.vfx_intent_tool import _PROMPT_TEMPLATE
    from unity_mcp.tools.intent_common import sanitize_intent
    target = "/Enemy{inject}"
    intent = "fire explosion"
    prompt = _PROMPT_TEMPLATE.format(target=sanitize_intent(target), kind="particle", intent=sanitize_intent(intent))
    assert "{" not in prompt


# ── Fix 11+12: do_intent executor retry logic ─────────────────────────────────

def test_is_partial_detects_err_format():
    """Fix 12: _is_partial must detect '[N] err:' batch format."""
    from unity_mcp.do_intent.executor import _is_partial
    # actual Unity bridge batch error format
    assert _is_partial("[1] err: path not found")


def test_is_partial_detects_partial_colon():
    """Fix 12: _is_partial also detects legacy 'PARTIAL:' prefix."""
    from unity_mcp.do_intent.executor import _is_partial
    assert _is_partial("PARTIAL: 1/2 ok")


def test_is_partial_false_for_all_ok():
    """Fix 12: _is_partial returns False when all ops succeed."""
    from unity_mcp.do_intent.executor import _is_partial
    assert not _is_partial("[1] ok: created")
    assert not _is_partial("ok: 3 ops")


def test_count_failures_matches_actual_format():
    """Fix 12: _count_failures must parse '[N] err: ...' lines."""
    from unity_mcp.do_intent.executor import _count_failures
    result = "[1] ok: created\n[2] err: path not found\n[3] err: missing component"
    failed = _count_failures(result)
    assert len(failed) == 2
    assert any("path not found" in f for f in failed)


@pytest.mark.asyncio
async def test_executor_applies_validate_plan_to_retry(monkeypatch):
    """Fix 11: validate_plan is applied to the retry commands before executing them."""
    from unity_mcp.do_intent.executor import Executor

    call_args = []

    async def fake_send(cmd, args):
        call_args.append(args.get("commands", ""))
        if len(call_args) == 1:
            return "[1] err: path not found"
        return "[1] ok: created"

    svc = MagicMock()
    # Haiku returns a valid plan
    svc.generate = AsyncMock(return_value="create_object name=Cube")

    ex = Executor(fake_send, sampling=svc)
    # Provide scene_paths so validate_plan can check paths
    result = await ex.execute(
        "create_object name=Cube",
        original_intent="add cube",
        scene_paths=set(),
    )
    assert result is not None
    assert len(call_args) == 2


# ── Fix 22: autobatch setup_objects full path with parent ─────────────────────

@pytest.mark.asyncio
async def test_setup_objects_uses_full_path_when_parent_given():
    """Fix 22: set_property/manage_component must use full path when parent specified."""
    import unity_mcp.tools.autobatch as ab
    send = AsyncMock(return_value="ok")
    ab._send = send

    from unity_mcp.tools.autobatch import setup_objects
    await setup_objects("Child parent=Root pos=(1,0,0)")

    cmds = send.call_args[0][1]["commands"]
    # Must reference /Root/Child not just /Child
    assert "path=/Root/Child" in cmds or "path=Root/Child" in cmds

    ab._send = None


# ── Fix 23: scene.py editor annotation ───────────────────────────────────────

def test_editor_tool_annotation_is_rw():
    """Fix 23: editor() must use _RW annotation, not _RW_IDEM (it mutates editor state)."""
    import inspect
    import unity_mcp.tools.scene as scene_mod
    register_src = inspect.getsource(scene_mod.register)
    found = False
    for line in register_src.splitlines():
        if "editor" in line and "mcp.tool" in line:
            found = True
            assert "_RW_IDEM" not in line, "editor must use _RW not _RW_IDEM"
            break
    assert found, "Could not find editor tool registration in scene.register()"


# ── Fix 24: bridge.py preserve exception type ────────────────────────────────

def test_bridge_connection_error_chains_original():
    """Fix 24: ConnectionError raised in bridge.send must chain the original exception."""
    import asyncio
    import pytest
    from unity_mcp.bridge import UnityBridge

    async def _run():
        bridge = UnityBridge("127.0.0.1", 19999)  # nothing listening
        try:
            await bridge.send("ping", {}, timeout=1.0)
            pytest.fail("Expected ConnectionError")
        except (ConnectionError, TimeoutError) as ce:
            assert ce.__cause__ is not None, "Exception must chain original via 'from e'"

    asyncio.get_event_loop().run_until_complete(_run())


# ── Fix 28: plugins PLUGIN_DIRS API version check ─────────────────────────────

def test_load_plugin_dirs_calls_check_api_version(tmp_path, monkeypatch):
    """Fix 28: _load_plugin_dirs must call _check_api_version for each loaded plugin."""
    import sys
    # Create a fake plugin
    plugin_file = tmp_path / "fake_plugin.py"
    plugin_file.write_text(
        "REQUIRED_API_VERSION = 999\n"
        "def register(mcp, send, args): pass\n"
    )
    monkeypatch.setenv("UNITY_MCP_PLUGIN_DIRS", str(tmp_path))
    monkeypatch.setenv("UNITY_MCP_SKIP_PLUGINS", "")

    # Remove any cached import
    for k in list(sys.modules.keys()):
        if "fake_plugin" in k:
            del sys.modules[k]

    checked = []
    import unity_mcp.plugins as plug

    original_check = plug._check_api_version

    def spy_check(module, name):
        checked.append(name)
        return original_check(module, name)

    monkeypatch.setattr(plug, "_check_api_version", spy_check)

    mcp = MagicMock()
    plug._load_plugin_dirs(mcp, MagicMock(), MagicMock())

    assert "fake_plugin" in checked, f"_check_api_version not called. checked={checked}"


# ── F16: error deduplication ──────────────────────────────────────────────────

def test_f16_error_dedup_first_is_full():
    """F16: first occurrence of an error must return full message."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result == "Error: path not found"


def test_f16_error_dedup_second_is_collapsed():
    """F16: second identical error must return collapsed '(repeated 2x)' form."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.dedup_error("set_property", "Error: path not found")
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result.startswith("(repeated 2x)")


def test_f16_error_dedup_different_cmd_not_collapsed():
    """F16: same error text for different cmd must NOT be collapsed."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.dedup_error("set_property", "Error: path not found")
    result = mw.dedup_error("create_object", "Error: path not found")
    assert not result.startswith("(repeated")


def test_f16_error_dedup_resets_on_session_reset():
    """F16: _error_dedup must be cleared on reset_session."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.dedup_error("set_property", "Error: path not found")
    mw.reset_session()
    result = mw.dedup_error("set_property", "Error: path not found")
    assert result == "Error: path not found"


@pytest.mark.asyncio
async def test_f16_success_payload_with_error_substring_not_deduped():
    """F16-regression: a SUCCESS read whose payload merely contains 'Error' must NOT be
    deduped/truncated on repeat. Dedup is gated on the protocol-ok flag, not a substring scan."""
    from unity_mcp.middleware import Middleware, wrap_send
    mw = Middleware()
    mw._prefetch_cache = None  # isolate from caching paths
    payload = "[Log] 12:00 ErrorManager started\n[Warning] mem high\n[Log] Player spawned"

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": payload}

    wrapped = wrap_send(send_fn, mw)
    r1 = await wrapped("get_console", {})
    r2 = await wrapped("get_console", {})
    assert "Player spawned" in r1 and "Player spawned" in r2, "success payload must survive intact"
    assert not r2.startswith("(repeated"), "2nd identical SUCCESS read must not be collapsed"


def test_f16_dedup_full_message_distinct_long_errors():
    """F16: two errors differing only past char 80 must be treated as distinct (full-key)."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    base = "Error: Component not found on path /Game/UI/Canvas/Panel/SubPanel/HealthBar/Fill"
    assert len(base) >= 80  # prefix collides under [:80] truncation
    mw.dedup_error("get_component", base + "A")
    result = mw.dedup_error("get_component", base + "B")
    assert not result.startswith("(repeated"), "distinct errors must not collide on 80-char prefix"
    assert result == base + "B", "full message preserved (no truncation)"


def test_f16_dedup_dict_bounded():
    """F16: _error_dedup must stay bounded (no unbounded growth across a long session)."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    for i in range(400):
        mw.dedup_error("set_property", f"Error #{i}")
    assert len(mw._error_dedup) <= 256


@pytest.mark.asyncio
async def test_f16_recorder_not_fooled_by_error_substring():
    """F16-regression: the LessonRecorder must classify on the protocol ok-flag, not a
    substring scan. A SUCCESS read whose payload contains 'Error' (get_console logs) repeated
    must NOT accumulate as a failure (which would emit a bogus lesson after 3)."""
    from unity_mcp.middleware import Middleware, wrap_send
    from unity_mcp.lessons import LessonRecorder, LessonStore
    mw = Middleware()
    mw._prefetch_cache = None
    mw.recorder = LessonRecorder(LessonStore(path=None))

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "[Error] NullRef in Foo\n[Error] again\n[Log] ok"}

    wrapped = wrap_send(send_fn, mw)
    for _ in range(4):
        await wrapped("get_console", {"level": "Error"})
    assert mw.recorder._recent_fails == {}, "success payload with 'Error' must not count as a fail"


# ── F05: circuit breaker is_ready_fn + cache above circuit ───────────────────

def test_f05_circuit_ready_fn_transitions_open_to_half_open():
    """F05: is_ready_fn returning True in OPEN state → transitions to HALF_OPEN."""
    from unity_mcp.middleware import CircuitBreaker
    ready_calls = [0]

    def is_ready():
        ready_calls[0] += 1
        return True

    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=is_ready)
    cb.record_failure()  # → OPEN (cooldown=9999 so time check won't fire)
    assert cb.state == CircuitBreaker.OPEN

    allowed = cb.allow_request()
    assert allowed is True
    assert cb.state == CircuitBreaker.HALF_OPEN
    assert ready_calls[0] == 1


def test_f05_circuit_ready_fn_none_uses_cooldown():
    """F05: without is_ready_fn, OPEN state uses time-based cooldown."""
    from unity_mcp.middleware import CircuitBreaker
    cb = CircuitBreaker(threshold=1, cooldown=9999.0)  # no is_ready_fn
    cb.record_failure()
    assert not cb.allow_request()  # cooldown not elapsed
    assert cb.state == CircuitBreaker.OPEN


def test_f05_circuit_ready_fn_false_stays_open():
    """F05: is_ready_fn returning False must not transition to HALF_OPEN."""
    from unity_mcp.middleware import CircuitBreaker
    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=lambda: False)
    cb.record_failure()
    assert not cb.allow_request()
    assert cb.state == CircuitBreaker.OPEN


def test_f05_circuit_ready_fn_exception_is_ignored():
    """F05: is_ready_fn raising must not crash allow_request; falls through to cooldown."""
    from unity_mcp.middleware import CircuitBreaker
    def bad_fn():
        raise RuntimeError("probe error")
    cb = CircuitBreaker(threshold=1, cooldown=9999.0, is_ready_fn=bad_fn)
    cb.record_failure()
    # Should not raise; cooldown not elapsed → False
    assert not cb.allow_request()


@pytest.mark.asyncio
async def test_f05_cache_served_when_circuit_open():
    """F05: PrefetchCache hit for cacheable read must be served even when circuit OPEN."""
    from unity_mcp.middleware import Middleware, CircuitBreaker
    from unity_mcp.prefetch_cache import PrefetchCache
    mw = Middleware()
    # Force OPEN state with cooldown not elapsed
    mw.circuit = CircuitBreaker(threshold=1, cooldown=9999.0)
    mw.circuit.record_failure()  # → OPEN
    assert mw.circuit.state == CircuitBreaker.OPEN

    # Seed cache
    mw._prefetch_cache = PrefetchCache()
    mw._prefetch_cache.put("get_component", {"path": "/Cube", "type": "Transform"}, "Transform data")

    send_called = [False]
    async def send_fn(cmd, args, timeout=30.0):
        send_called[0] = True
        return {"ok": True, "data": "from unity"}

    wrapped = __import__("unity_mcp.middleware", fromlist=["wrap_send"]).wrap_send(send_fn, mw)
    result = await wrapped("get_component", {"path": "/Cube", "type": "Transform"})

    assert not send_called[0], "send_fn must not be called when cache serves the response"
    assert "Transform data" in result or "CACHED" in result


@pytest.mark.asyncio
async def test_f05_non_cacheable_blocked_when_circuit_open():
    """F05: non-cacheable command must still be blocked when circuit OPEN."""
    import time
    from unity_mcp.middleware import Middleware, CircuitBreaker
    mw = Middleware()
    # Force OPEN state with cooldown not elapsed
    mw.circuit = CircuitBreaker(threshold=1, cooldown=9999.0)
    mw.circuit.record_failure()  # → OPEN
    assert mw.circuit.state == CircuitBreaker.OPEN

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "ok"}

    wrapped = __import__("unity_mcp.middleware", fromlist=["wrap_send"]).wrap_send(send_fn, mw)
    result = await wrapped("set_property", {"path": "/Cube", "component": "Transform", "prop": "x", "value": "1"})
    assert "Circuit OPEN" in result


# ── F17: negative path cache ─────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_f17_negative_path_cache_skips_tcp():
    """F17: second unknown-path call within TTL must NOT invoke send_fn (cache hit)."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.known_paths = {"/Known"}  # non-empty so resolve_path_live is active

    call_count = [0]

    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""  # no candidates

    # First call: cache miss → TCP
    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert call_count[0] == 1

    # Second call within TTL: cache hit → no TCP
    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert call_count[0] == 1, "Second call must use negative cache, not TCP"


@pytest.mark.asyncio
async def test_f17_negative_path_cache_expires():
    """F17: after TTL expires, unknown path must query TCP again."""
    import time as time_mod
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.known_paths = {"/Known"}

    call_count = [0]

    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""

    with patch("unity_mcp.middleware_paths.time") as mock_time:  # resolve_path_live lives here (F14 split)
        mock_time.monotonic.return_value = 1000.0
        await mw.resolve_path_live("/UnknownPath", send_fn)
        assert call_count[0] == 1

        # Advance time past TTL (10s)
        mock_time.monotonic.return_value = 1012.0
        await mw.resolve_path_live("/UnknownPath", send_fn)
        assert call_count[0] == 2, "Expired cache must re-query TCP"


@pytest.mark.asyncio
async def test_f17_negative_path_cache_cleared_on_reset():
    """F17: reset_session must clear negative path cache."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.known_paths = {"/Known"}

    async def send_fn(cmd, args, timeout=30.0):
        return ""

    await mw.resolve_path_live("/UnknownPath", send_fn)
    assert len(mw._negative_path_cache) > 0

    mw.reset_session()
    assert len(mw._negative_path_cache) == 0


@pytest.mark.asyncio
async def test_f17_not_for_known_paths():
    """F17: known paths must not be added to negative cache."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.known_paths = {"/Known"}

    call_count = [0]

    async def send_fn(cmd, args, timeout=30.0):
        call_count[0] += 1
        return ""

    await mw.resolve_path_live("/Known", send_fn)
    assert call_count[0] == 0  # exact match, never searched
    assert "/Known" not in mw._negative_path_cache


@pytest.mark.asyncio
async def test_f17_tcp_failure_does_not_poison_cache():
    """F17-regression: a transient TCP failure (send raises) must NOT add the path to the
    negative cache — 'absent' and 'unreachable' are different. Else a blip blocks the path 10s."""
    from unity_mcp.middleware import Middleware
    mw = Middleware()
    mw.known_paths = {"/Known"}

    async def send_fn(cmd, args, timeout=30.0):
        raise ConnectionError("TCP dropped")

    resolved, _ = await mw.resolve_path_live("/UnknownPath", send_fn)
    assert resolved == "/UnknownPath"
    assert "/UnknownPath" not in mw._negative_path_cache, "transient failure must not poison cache"


@pytest.mark.asyncio
async def test_f17_write_invalidates_negative_cache():
    """F17-regression: a write (create/rename) may make a previously-absent path resolvable,
    so any write must drop the negative cache."""
    import time as time_mod
    from unity_mcp.middleware import Middleware, wrap_send
    mw = Middleware()
    mw._prefetch_cache = None  # isolate from prefetch path
    mw.known_paths = set()     # keep resolve_path_live a no-op (no re-add)
    mw._negative_path_cache = {"/Ghost": time_mod.monotonic() + 999}

    async def send_fn(cmd, args, timeout=30.0):
        return {"ok": True, "data": "created"}

    wrapped = wrap_send(send_fn, mw)
    await wrapped("create_object", {"name": "Ghost"})
    assert mw._negative_path_cache == {}, "write must clear negative cache"


# ── F01-qw: ping timeout, counter lock removal, watchdog gather ───────────────

def test_f01_raw_ping_default_timeout_5s():
    """F01: _raw_ping default timeout must be 5.0s (reduced from 10.0)."""
    import inspect
    from unity_mcp.bridge import UnityBridge
    sig = inspect.signature(UnityBridge._raw_ping)
    assert sig.parameters["timeout"].default == 5.0


def test_f01_heartbeat_ping_timeout_5s():
    """F01: _heartbeat_tick must call _raw_ping with timeout=5 (not 20)."""
    import inspect
    from unity_mcp.bridge import UnityBridge
    src = inspect.getsource(UnityBridge._heartbeat_tick)
    assert "timeout=5" in src, "heartbeat must use timeout=5"
    assert "timeout=20" not in src, "timeout=20 must be removed"


@pytest.mark.asyncio
async def test_f01_watchdog_scan_runs_sends_concurrently():
    """F01-behavioral: _scan must dispatch its two probes concurrently (gather), not serially.
    Detected by observing both sends in-flight at the same time."""
    import asyncio
    from unity_mcp.watchdog import ProactiveWatchdog

    in_flight = [0]
    max_in_flight = [0]

    async def send_fn(cmd, args, timeout=5.0):
        in_flight[0] += 1
        max_in_flight[0] = max(max_in_flight[0], in_flight[0])
        await asyncio.sleep(0.02)  # hold the slot so a concurrent peer overlaps
        in_flight[0] -= 1
        return ""

    wd = ProactiveWatchdog(send_fn)
    await wd._scan()
    assert max_in_flight[0] == 2, "both probes must be in-flight together (gather, not serial)"


@pytest.mark.asyncio
async def test_f01_send_concurrent_unique_ids():
    """F01-behavioral: with the inner counter lock removed, concurrent send() calls must still
    get unique message IDs (asyncio single-thread → counter++ atomic between awaits)."""
    import asyncio, json
    from unittest.mock import AsyncMock, MagicMock
    from unity_mcp.bridge import UnityBridge

    bridge = UnityBridge("127.0.0.1", 19998)
    sent_ids = []

    writer = MagicMock()
    writer.is_closing.return_value = False
    writer.get_extra_info.return_value = None  # skip socket-level liveness probe

    def _write(buf):
        sent_ids.append(json.loads(buf[4:])["id"])  # strip 4-byte length prefix
    writer.write.side_effect = _write
    writer.drain = AsyncMock()
    bridge._writer = writer
    bridge._reader = MagicMock()

    async def _read_response():
        return {"id": sent_ids[-1], "ok": True, "data": "ok"}  # echo current id (serialized by lock)
    bridge._read_response = _read_response

    results = await asyncio.gather(*[bridge.send("ping", {}) for _ in range(50)])
    assert len(set(sent_ids)) == 50, "concurrent sends must get unique IDs"
    assert all(r.get("ok") for r in results)
