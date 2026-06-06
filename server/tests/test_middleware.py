import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware import (
    Middleware,
    CircuitBreaker,
    wrap_send,
    BLAST_RADIUS,
)

# TODO (test cycle 3): A8 review — markers using '⚠ ' prefix (RETRY, TAINT, OVERWRITE,
# STARVATION, BLAST RADIUS, PATH WARNING) still use loose `"X" in result` substring checks.
# Replace with `"⚠ X:" in result` once we audit actual emission format.


# ─── Feature 1: Retry Watchdog ────────────────────────────────────────────────

def test_retry_watchdog_allows_first_call(mw):
    result = mw.check_retry("get_hierarchy", {})
    assert result is None


def test_retry_watchdog_allows_different_calls(mw):
    mw.check_retry("get_hierarchy", {})
    result = mw.check_retry("get_component", {"path": "/A"})
    assert result is None


def test_retry_watchdog_blocks_duplicate(mw):
    # Use write cmd — read cmds (get_hierarchy) are never blocked by design
    mw.check_retry("set_property", {"path": "/A", "prop": "x", "value": "1"})
    result = mw.check_retry("set_property", {"path": "/A", "prop": "x", "value": "1"})
    assert result is not None
    assert "RETRY" in result


def test_retry_watchdog_allows_same_cmd_different_args(mw):
    # Use write cmd — read cmds are never blocked
    mw.check_retry("set_property", {"path": "/A"})
    result = mw.check_retry("set_property", {"path": "/B"})
    assert result is None


# ─── Feature 2: Confidence Decay ─────────────────────────────────────────────

def test_confidence_appended_to_response(mw):
    # Fresh mw has confidence 1.0 — after a read it stays 1.0 (capped), no suffix
    result = mw.update_confidence("get_hierarchy", "some output")
    assert "[confidence:" not in result


def test_confidence_suffix_appears_when_low(mw):
    # 0.4 + 0.15 = 0.55 >= 0.5 → no suffix
    mw.confidence = 0.4
    result = mw.update_confidence("get_hierarchy", "data")
    assert "[confidence:" not in result
    # 0.2 - 0.08 = 0.12 < 0.5 → suffix present
    mw.confidence = 0.2
    result = mw.update_confidence("set_property", "data")
    assert "[confidence:" in result


def test_confidence_decreases_on_write(mw):
    before = mw.confidence
    mw.update_confidence("set_property", "ok")
    assert mw.confidence < before


def test_confidence_increases_on_read(mw):
    mw.confidence = 0.5
    mw.update_confidence("get_hierarchy", "ok")
    assert mw.confidence > 0.5


def test_confidence_caps_at_1(mw):
    mw.confidence = 0.95
    mw.update_confidence("get_hierarchy", "ok")
    assert mw.confidence <= 1.0


def test_confidence_low_warning(mw):
    mw.confidence = 0.15  # after write: 0.15 - 0.08 = 0.07 < 0.3
    result = mw.update_confidence("set_property", "data")
    assert "LOW CONFIDENCE" in result


# ─── Feature 3: Taint Tracking ───────────────────────────────────────────────

def test_taint_warns_unverified_reference(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"})
    assert result is not None
    assert "TAINT" in result


def test_taint_allows_verified_value(mw):
    mw.record_read("get_component", {"path": "/Enemy"}, "Enemy component data")
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "/Enemy"})
    assert result is None


def test_taint_ignores_non_reference_prop(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "speed", "value": "5"})
    assert result is None


def test_taint_allows_hash_ref(mw):
    result = mw.check_taint("set_property", {"path": "/A", "component": "C", "prop": "targetReference", "value": "#abc123"})
    assert result is None


# ─── Feature 4: Periodic State Injection ─────────────────────────────────────

@pytest.mark.asyncio
async def test_state_injection_every_10_calls(mw):
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 9
    result = await mw.maybe_inject_state(fake_send, "original result")
    assert "AUTO STATE" in result
    assert "HierarchyData" in result


@pytest.mark.asyncio
async def test_auto_state_staleness_gate(mw):
    """Injects at call 10, then again at call 20 (gap > 5)."""
    fake_send = AsyncMock(return_value="H")
    mw.call_count = 9
    mw._last_hierarchy_call = 0
    await mw.maybe_inject_state(fake_send, "r")
    assert mw._last_hierarchy_call == 10
    fake_send.reset_mock()
    # second injection: call_count=19, gap=20-10=10 > 5
    mw.call_count = 19
    await mw.maybe_inject_state(fake_send, "r")
    assert mw._last_hierarchy_call == 20
    fake_send.assert_called_once()


@pytest.mark.asyncio
async def test_auto_state_skipped_when_recent(mw):
    """Auto-inject skipped when organic get_hierarchy was within last 5 calls.

    Drives a real get_hierarchy through wrapped() to set _last_hierarchy_call,
    then triggers the % 10 boundary — gap is <= 5 so injection must NOT fire.
    """
    call_log: list[str] = []

    async def fake_send(cmd, args, timeout=30.0):
        call_log.append(cmd)
        return "HierarchyData"

    mw = Middleware()
    wrapped = wrap_send(fake_send, mw)

    # Push 7 calls so call_count reaches 7; one of them is get_hierarchy (organic)
    for _ in range(6):
        await wrapped("get_component", {"path": "/A", "type": "T"})
    # Organic get_hierarchy at call_count=7
    await wrapped("get_hierarchy", {})
    # _last_hierarchy_call is now 7

    # Push 3 more to reach call_count=10 (% 10 boundary)
    # gap = 10 - 7 = 3, not > 5 → no auto-inject
    call_log.clear()
    for _ in range(3):
        await wrapped("get_component", {"path": "/A", "type": "T"})

    # get_hierarchy must NOT appear in call_log (no auto-injection)
    assert "get_hierarchy" not in call_log, (
        f"Auto-inject fired despite recent organic hierarchy (gap<=5). calls: {call_log}"
    )


@pytest.mark.asyncio
async def test_auto_state_organic_hierarchy_suppresses_injection(mw):
    """Organic get_hierarchy within 5 calls before a % 10 boundary skips auto-inject."""
    call_log: list[str] = []

    async def fake_send(cmd, args, timeout=30.0):
        call_log.append(cmd)
        return "H"

    mw2 = Middleware()
    wrapped = wrap_send(fake_send, mw2)

    # Get to call 48 via non-hierarchy calls
    for _ in range(48):
        await wrapped("get_component", {"path": "/A", "type": "T"})

    # Organic get_hierarchy at call 49
    call_log.clear()
    await wrapped("get_hierarchy", {})
    assert mw2._last_hierarchy_call == 49

    # Call 50 hits % 10; gap = 50 - 49 = 1, not > 5 → no auto-inject
    call_log.clear()
    await wrapped("get_component", {"path": "/A", "type": "T"})
    assert "get_hierarchy" not in call_log


def test_reset_session_clears_hierarchy_call_counter(mw):
    mw._last_hierarchy_call = 50
    mw.reset_session()
    assert mw._last_hierarchy_call == 0


@pytest.mark.asyncio
async def test_state_injection_not_before_10(mw):
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 0
    result = await mw.maybe_inject_state(fake_send, "original result")
    assert "AUTO STATE" not in result
    fake_send.assert_not_called()


@pytest.mark.asyncio
async def test_state_injection_increments_counter(mw):
    fake_send = AsyncMock(return_value="HierarchyData")
    mw.call_count = 0
    await mw.maybe_inject_state(fake_send, "r")
    assert mw.call_count == 1


# ─── Feature 5: Path Cache ────────────────────────────────────────────────────

def test_path_cache_empty_allows_all(mw):
    result = mw.validate_path("UnknownObject")
    assert result is None  # no cache yet


def test_path_cache_updated_on_hierarchy(mw):
    # Real Unity format: scene name at depth 0, objects at depth 1+
    hierarchy = "SampleScene\n├─ Main Camera $[Camera]\n├─ Player $[Transform]\n└─ Enemy $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    assert "/Player" in mw.known_paths
    assert "/Enemy" in mw.known_paths


def test_path_cache_warns_unknown_path(mw):
    hierarchy = "SampleScene\n├─ Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("/NonExistent")
    assert result is not None
    assert "PATH WARNING" in result


def test_path_cache_allows_known_path(mw):
    hierarchy = "SampleScene\n├─ Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("/Player")
    assert result is None


def test_path_cache_allows_ref_syntax(mw):
    hierarchy = "Player $[Transform]"
    mw.update_path_cache("get_hierarchy", hierarchy)
    result = mw.validate_path("$ref:abc")
    assert result is None


# ─── Feature 6: Dead Write Elimination ───────────────────────────────────────

def test_dead_write_warns_overwrite(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "2"})
    assert result is not None
    assert "OVERWRITE" in result


def test_dead_write_no_warn_first_time(mw):
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    assert result is None


def test_dead_write_cleared_on_read(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    mw.clear_write_on_read("get_component", {"path": "/A"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "2"})
    assert result is None


def test_dead_write_different_prop_no_warn(mw):
    mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "x", "value": "1"})
    result = mw.check_dead_write("set_property", {"path": "/A", "component": "C", "prop": "y", "value": "5"})
    assert result is None


# ─── Feature 7: Circuit Breaker ──────────────────────────────────────────────

def test_circuit_breaker_closed_allows():
    cb = CircuitBreaker()
    assert cb.allow_request() is True


def test_circuit_breaker_opens_after_3_failures():
    cb = CircuitBreaker(threshold=3)
    cb.record_failure()
    cb.record_failure()
    cb.record_failure()
    assert cb.get_status() == "OPEN"


def test_circuit_breaker_blocks_when_open():
    cb = CircuitBreaker(threshold=1, cooldown=9999.0)
    cb.record_failure()
    assert cb.allow_request() is False


def test_circuit_breaker_half_open_after_cooldown():
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    # cooldown=0 means already expired
    import time; time.sleep(0.01)
    assert cb.allow_request() is True
    assert cb.get_status() == "HALF_OPEN"


def test_circuit_breaker_closes_on_success():
    cb = CircuitBreaker(threshold=1, cooldown=0.0)
    cb.record_failure()
    import time; time.sleep(0.01)
    cb.allow_request()  # transitions to HALF_OPEN
    cb.record_success()
    assert cb.get_status() == "CLOSED"


# ─── wrap_send circuit integration ───────────────────────────────────────────

@pytest.mark.asyncio
async def test_wrap_send_circuit_open_returns_error():
    mw = Middleware()
    mw.circuit.failures = mw.circuit.threshold  # force open
    mw.circuit.state = CircuitBreaker.OPEN
    mw.circuit.opened_at = 9e18  # never expires

    fake_send = AsyncMock(return_value="ok")
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_hierarchy", {})
    assert "Circuit" in result or "circuit" in result.lower()
    fake_send.assert_not_called()


@pytest.mark.asyncio
async def test_wrap_send_circuit_records_success():
    mw = Middleware()
    fake_send = AsyncMock(return_value="hierarchy data")
    wrapped = wrap_send(fake_send, mw)
    await wrapped("get_hierarchy", {})
    assert mw.circuit.failures == 0
    assert mw.circuit.get_status() == "CLOSED"


@pytest.mark.asyncio
async def test_wrap_send_circuit_records_failure():
    mw = Middleware()
    fake_send = AsyncMock(side_effect=ConnectionError("TCP down"))
    wrapped = wrap_send(fake_send, mw)
    with pytest.raises(ConnectionError):
        await wrapped("get_hierarchy", {})
    assert mw.circuit.failures == 1


# ─── Feature 4 (new): Starvation Monitor ─────────────────────────────────────

def test_starvation_warns_after_5_identical(mw):
    for _ in range(5):
        result = mw.check_starvation("same result")
    assert "STARVATION" in result


def test_starvation_no_warn_on_different(mw):
    results = ["result A", "result B", "result C", "result D", "result E"]
    for r in results:
        result = mw.check_starvation(r)
    assert "STARVATION" not in result


def test_starvation_no_warn_before_5(mw):
    for _ in range(4):
        result = mw.check_starvation("same result")
    assert "STARVATION" not in result


# ─── Feature 5 (new): Blast Radius Tags ──────────────────────────────────────

def test_blast_radius_warns_high(mw):
    warn = mw.check_blast_radius("delete_object")
    assert warn is not None
    assert "BLAST RADIUS" in warn


def test_blast_radius_silent_low(mw):
    warn = mw.check_blast_radius("get_hierarchy")
    assert warn is None


def test_blast_radius_table_has_entries():
    assert "delete_object" in BLAST_RADIUS
    assert BLAST_RADIUS["delete_object"] >= 3
    assert BLAST_RADIUS["get_hierarchy"] == 0


# ─── Feature 6 (new): Incremental Verification ───────────────────────────────

def test_verification_checkpoint_every_5(mw):
    for _ in range(4):
        mw.check_verification_needed("set_property")
    result = mw.check_verification_needed("set_property")
    assert result is not None
    assert "VERIFICATION" in result


def test_verification_no_warn_before_5(mw):
    for _ in range(4):
        result = mw.check_verification_needed("set_property")
    assert result is None


def test_verification_only_counts_writes(mw):
    for _ in range(5):
        result = mw.check_verification_needed("get_hierarchy")
    assert result is None


# ─── Feature 7 (new): Alive Check ────────────────────────────────────────────

def test_alive_check_recent_ok(mw):
    mw._last_success = __import__("time").time()
    assert mw.check_alive() is True


def test_alive_check_stale_false(mw):
    mw._last_success = 0.0  # epoch — definitely stale
    assert mw.check_alive() is False


# ─── Feature 12: Workflow Phase FSM ──────────────────────────────────────────

def test_workflow_fsm_warns_after_3_writes(mw):
    mw.transition("set_property")
    mw.transition("set_property")
    result = mw.transition("set_property")
    assert result is not None
    assert "write" in result.lower() or "verif" in result.lower()


def test_workflow_fsm_resets_on_read(mw):
    mw.transition("set_property")
    mw.transition("set_property")
    mw.transition("get_hierarchy")  # read resets counter
    result = mw.transition("set_property")
    assert result is None


# ─── F08: strip_defaults in wrap_send ────────────────────────────────────────

@pytest.mark.asyncio
async def test_wrap_send_strips_defaults_for_get_component():
    """position (0,0,0) and mass:1 stripped; localPosition (5,3,0) kept."""
    raw = "[Transform]\nposition: (0, 0, 0)\nlocalPosition: (5, 3, 0)\n[Rigidbody]\nmass: 1\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/A", "type": ""})
    assert "position: (0, 0, 0)" not in result
    assert "mass: 1" not in result
    assert "localPosition: (5, 3, 0)" in result


@pytest.mark.asyncio
async def test_wrap_send_no_strip_for_hierarchy():
    """get_hierarchy NOT in _STRIP_CMDS — drag:0 kept."""
    raw = "SampleScene\nRigidbody drag: 0\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_hierarchy", {})
    assert "drag: 0" in result


@pytest.mark.asyncio
async def test_wrap_send_no_strip_escape_hatch():
    """_no_strip=True skips stripping even for get_component."""
    raw = "[Transform]\nposition: (0, 0, 0)\n"
    mw = Middleware()
    fake_send = AsyncMock(return_value=raw)
    wrapped = wrap_send(fake_send, mw)
    result = await wrapped("get_component", {"path": "/A", "_no_strip": True})
    assert "position: (0, 0, 0)" in result


# ── Fix 4: Bounded LRU on Middleware collections ─────────────────────────────

def test_clean_paths_bounded():
    """_clean_paths must not exceed 256 entries."""
    mw = Middleware()
    for i in range(300):
        mw.record_read("get_component", {"path": f"/Obj{i}"}, "")
    assert len(mw._clean_paths) <= 256


def test_component_cache_bounded():
    """_component_cache must not exceed 256 entries."""
    mw = Middleware()
    for i in range(300):
        mw.cache_components("get_component", {"path": f"/Obj{i}", "type": "Rigidbody"}, "")
    assert len(mw._component_cache) <= 256


def test_last_writes_bounded():
    """_last_writes must not exceed 128 entries."""
    mw = Middleware()
    for i in range(200):
        mw.check_dead_write("set_property", {"path": f"/Obj{i}", "component": "T", "prop": f"p{i}", "value": "v"})
    assert len(mw._last_writes) <= 128


# ─── LRU eviction ORDER: component cache ─────────────────────────────────────

def test_component_cache_evicts_oldest_not_newest():
    """Fill _component_cache to max+1; oldest path evicted, newest kept."""
    mw = Middleware()
    mw._MAX_COMPONENTS = 3
    mw._component_cache.clear()
    mw._lru_add_component("/a", "C")
    mw._lru_add_component("/b", "C")
    mw._lru_add_component("/c", "C")
    mw._lru_add_component("/d", "C")  # overflow — /a is oldest
    assert "/a" not in mw._component_cache
    assert "/b" in mw._component_cache
    assert "/c" in mw._component_cache
    assert "/d" in mw._component_cache
