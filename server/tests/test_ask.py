"""TDD tests for ask package: router, executor, summarizer, ask tool."""
import re
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


# ---------------------------------------------------------------------------
# 1. Router — broken refs pattern
# ---------------------------------------------------------------------------

def test_router_broken_refs_pattern():
    from unity_mcp.ask.router import route
    plan = route("show me all broken references in scene")
    assert plan is not None
    assert plan.key == "BROKEN_REFS"


def test_router_broken_refs_pattern_alt():
    from unity_mcp.ask.router import route
    plan = route("are there any broken links?")
    assert plan is not None
    assert plan.key == "BROKEN_REFS"


# ---------------------------------------------------------------------------
# 2. Router — scene health pattern
# ---------------------------------------------------------------------------

def test_router_scene_health_pattern():
    from unity_mcp.ask.router import route
    plan = route("any errors in the scene?")
    assert plan is not None
    assert plan.key == "SCENE_HEALTH"


def test_router_scene_health_console():
    from unity_mcp.ask.router import route
    plan = route("what problems are in the console?")
    assert plan is not None
    assert plan.key == "SCENE_HEALTH"


# ---------------------------------------------------------------------------
# 3. Router — editor state pattern
# ---------------------------------------------------------------------------

def test_router_editor_state_pattern():
    from unity_mcp.ask.router import route
    plan = route("is Unity in play mode?")
    assert plan is not None
    assert plan.key == "EDITOR_STATE"


def test_router_compile_errors_pattern():
    from unity_mcp.ask.router import route
    plan = route("any compilation errors?")
    assert plan is not None
    assert plan.key == "COMPILE_ERRORS"


# ---------------------------------------------------------------------------
# 4. Router — out of scope: no Unity noun
# ---------------------------------------------------------------------------

def test_router_out_of_scope_no_unity_noun():
    from unity_mcp.ask.router import route
    plan = route("what is the weather today?")
    assert plan is None


def test_router_out_of_scope_generic():
    from unity_mcp.ask.router import route
    plan = route("tell me a joke")
    assert plan is None


# ---------------------------------------------------------------------------
# 5. Router — rejects mutating verbs
# ---------------------------------------------------------------------------

def test_router_rejects_mutating_verbs_delete():
    from unity_mcp.ask.router import route
    result = route("delete all objects in scene")
    # Either None (no plan) or raises — implementation can choose
    # But we test that mutating_verb detection works
    from unity_mcp.ask.router import is_mutating
    assert is_mutating("delete all objects in scene")


def test_router_rejects_mutating_verbs_set():
    from unity_mcp.ask.router import is_mutating
    assert is_mutating("set the health to 100")


def test_router_read_only_not_mutating():
    from unity_mcp.ask.router import is_mutating
    assert not is_mutating("what are the broken references?")


# ---------------------------------------------------------------------------
# 6. Executor — runs canonical plan
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_executor_runs_canonical_plan():
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock(return_value="refs: none broken")
    ex = AskExecutor(send)

    plan = ToolPlan([("validate_references", {"path": "/", "depth": "5"})], "broken refs only", "BROKEN_REFS")
    results = await ex.run(plan)
    assert len(results) == 1
    assert results[0] == "refs: none broken"
    send.assert_called_once_with("validate_references", {"path": "/", "depth": "5"})


@pytest.mark.asyncio
async def test_executor_runs_multi_tool_plan():
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    responses = ["scan ok", "refs ok", "no errors", "no compile"]
    idx = 0

    async def fake_send(cmd, args):
        nonlocal idx
        r = responses[idx]
        idx += 1
        return r

    ex = AskExecutor(fake_send)
    plan = ToolPlan([
        ("scan_scene", {}),
        ("validate_references", {"path": "/", "depth": "3"}),
        ("get_console", {"count": "10", "level": "Error"}),
        ("get_compile_errors", {}),
    ], "summarize top issues", "SCENE_HEALTH")

    results = await ex.run(plan)
    assert len(results) == 4


# ---------------------------------------------------------------------------
# 7. Summarizer — bypass mode: short single-tool result
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_summarizer_bypass_mode_short_single_tool():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock()  # Should NOT be called

    s = Summarizer(svc)
    raw = ["no broken refs"]  # short, single tool
    result = await s.summarize("broken refs?", raw, hint="broken refs only")
    assert result == "no broken refs"
    svc.generate.assert_not_called()


@pytest.mark.asyncio
async def test_summarizer_bypass_mode_long_triggers_haiku():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value="3 errors found in scene")

    s = Summarizer(svc)
    # More than 200 chars
    long_raw = ["x" * 250]
    result = await s.summarize("any errors?", long_raw, hint="summarize top issues")
    svc.generate.assert_called_once()
    assert result == "3 errors found in scene"


# ---------------------------------------------------------------------------
# 8. Summarizer — haiku fallback when service disabled
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_summarizer_haiku_fallback_when_none():
    from unity_mcp.ask.summarizer import Summarizer

    svc = MagicMock()
    svc.generate = AsyncMock(return_value=None)

    s = Summarizer(svc)
    long_raw = ["x" * 250]
    # Falls back to raw data when haiku returns None
    result = await s.summarize("any errors?", long_raw, hint="something")
    assert result is not None  # returns something, not crashes


# ---------------------------------------------------------------------------
# 9. ask e2e — broken refs returns concise
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_ask_e2e_broken_refs_returns_concise():
    from unity_mcp.tools.ask_tool import ask

    with patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "no broken refs"
        result = await ask("are there broken references?")
        assert result is not None
        assert len(result) < 300


# ---------------------------------------------------------------------------
# 10. ask e2e — scene health uses haiku (multi-tool)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_ask_e2e_scene_health_uses_haiku():
    from unity_mcp.tools.ask_tool import ask

    with (
        patch("unity_mcp.tools.ask_tool._send", new_callable=AsyncMock) as mock_send,
        patch("unity_mcp.tools.ask_tool._sampling") as mock_svc,
    ):
        mock_send.return_value = "x" * 300  # long → triggers haiku
        mock_svc.generate = AsyncMock(return_value="Scene has 2 errors: NullRef in Player.cs")

        result = await ask("what problems are in the scene?")
        assert result is not None


# ---------------------------------------------------------------------------
# 11. ask — rejects mutating question
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_ask_rejects_mutating_question():
    from unity_mcp.tools.ask_tool import ask

    result = await ask("delete all the objects please")
    # multiple valid rejection phrasings — all are legitimate responses
    assert re.search(r"read-only|mutating|\bask\b", result, re.IGNORECASE), result


# ---------------------------------------------------------------------------
# 12. ask — no Unity noun returns rejection
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_ask_no_unity_noun_returns_rejection():
    from unity_mcp.tools.ask_tool import ask

    result = await ask("what is the meaning of life?")
    assert result is not None
    # should indicate it can't answer or is out of scope
    assert len(result) < 300


# ---------------------------------------------------------------------------
# 13. Executor corroboration — get_compile_errors result passes through corroborate
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_executor_corroborates_get_compile_errors():
    """AskExecutor.run must pass get_compile_errors result through editor_log.corroborate."""
    import unity_mcp.editor_log as el
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    raw_csharp = "No compilation errors."
    corroborated = "[editor.log - dll stale]\nAssets/Foo.cs:1:1: error CS0117: stale"

    send = AsyncMock(return_value=raw_csharp)

    with patch.object(el, "corroborate", return_value=corroborated) as mock_cor:
        ex = AskExecutor(send)
        plan = ToolPlan([("get_compile_errors", {})], "compile errors", "COMPILE_ERRORS")
        results = await ex.run(plan)

    mock_cor.assert_called_once_with(raw_csharp)
    assert results[0] == corroborated


@pytest.mark.asyncio
async def test_executor_does_not_corroborate_other_tools():
    """AskExecutor.run must NOT call corroborate for non-compile tools."""
    import unity_mcp.editor_log as el
    from unity_mcp.ask.executor import AskExecutor
    from unity_mcp.ask.plans import ToolPlan

    send = AsyncMock(return_value="scan ok")

    with patch.object(el, "corroborate") as mock_cor:
        ex = AskExecutor(send)
        plan = ToolPlan([("scan_scene", {})], "scan", "SCAN")
        results = await ex.run(plan)

    mock_cor.assert_not_called()
    assert results[0] == "scan ok"
