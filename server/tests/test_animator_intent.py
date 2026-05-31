"""TDD tests for animator_intent tool."""
import re
import pytest
from unittest.mock import AsyncMock, patch


# ---------------------------------------------------------------------------
# 1. DSL Parser
# ---------------------------------------------------------------------------

def test_animator_parse_dsl_params():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "PARAM Speed float 0\nPARAM IsGrounded bool true"
    result = parse_animator_dsl(dsl)
    assert result["params"] == [("Speed", "float", "0"), ("IsGrounded", "bool", "true")]


def test_animator_parse_dsl_states():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "STATE Idle Idle.anim\nSTATE Walk Walk.anim"
    result = parse_animator_dsl(dsl)
    assert result["states"] == [("Idle", "Idle.anim"), ("Walk", "Walk.anim")]


def test_animator_parse_dsl_default():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "DEFAULT Idle"
    result = parse_animator_dsl(dsl)
    assert result["default"] == "Idle"


def test_animator_parse_dsl_transitions():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "TRANS Idle -> Walk dur=0.15 if Speed>0.1"
    result = parse_animator_dsl(dsl)
    assert result["transitions"] == [
        {"source": "Idle", "target": "Walk", "duration": "0.15", "condition": "Speed>0.1"}
    ]


def test_animator_parse_dsl_transition_no_condition():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "TRANS Idle -> Walk dur=0.2"
    result = parse_animator_dsl(dsl)
    assert result["transitions"][0]["condition"] is None


def test_animator_parse_dsl_ignores_unknown_keywords():
    from unity_mcp.tools.animator_intent_tool import parse_animator_dsl
    dsl = "PARAM Speed float 0\nFOO bar baz"
    result = parse_animator_dsl(dsl)
    assert len(result["params"]) == 1


# ---------------------------------------------------------------------------
# 2. Validator
# ---------------------------------------------------------------------------

def test_animator_validate_trans_target_undeclared():
    from unity_mcp.tools.animator_intent_tool import validate_animator_dsl
    dsl_data = {
        "params": [("Speed", "float", "0")],
        "states": [("Idle", "Idle.anim")],
        "default": "Idle",
        "transitions": [{"source": "Idle", "target": "Ghost", "duration": "0.15", "condition": None}],
    }
    err = validate_animator_dsl(dsl_data)
    assert err is not None
    assert "Ghost" in err


def test_animator_validate_trans_condition_undeclared_param():
    from unity_mcp.tools.animator_intent_tool import validate_animator_dsl
    dsl_data = {
        "params": [],
        "states": [("Idle", "Idle.anim"), ("Walk", "Walk.anim")],
        "default": "Idle",
        "transitions": [{"source": "Idle", "target": "Walk", "duration": "0.15", "condition": "Speed>0.1"}],
    }
    err = validate_animator_dsl(dsl_data)
    assert err is not None
    assert "Speed" in err


def test_animator_validate_ok():
    from unity_mcp.tools.animator_intent_tool import validate_animator_dsl
    dsl_data = {
        "params": [("Speed", "float", "0")],
        "states": [("Idle", "Idle.anim"), ("Walk", "Walk.anim")],
        "default": "Idle",
        "transitions": [
            {"source": "Idle", "target": "Walk", "duration": "0.15", "condition": "Speed>0.1"},
            {"source": "Walk", "target": "Idle", "duration": "0.15", "condition": "Speed<0.1"},
        ],
    }
    assert validate_animator_dsl(dsl_data) is None


# ---------------------------------------------------------------------------
# 3. Builder
# ---------------------------------------------------------------------------

def test_animator_builder_params():
    from unity_mcp.tools.animator_intent_tool import build_animator_batch
    dsl_data = {
        "params": [("Speed", "float", "0"), ("IsGrounded", "bool", "true")],
        "states": [],
        "default": None,
        "transitions": [],
    }
    lines = build_animator_batch("/Player", dsl_data)
    assert any("add_param" in l and "Speed:float:0;IsGrounded:bool:true" in l for l in lines)


def test_animator_builder_states():
    from unity_mcp.tools.animator_intent_tool import build_animator_batch
    dsl_data = {
        "params": [],
        "states": [("Idle", "Idle.anim"), ("Walk", "Walk.anim")],
        "default": "Idle",
        "transitions": [],
    }
    lines = build_animator_batch("/Player", dsl_data)
    assert any("add_state" in l for l in lines)
    assert any("set_default" in l and "state=Idle" in l for l in lines)


def test_animator_builder_transitions():
    from unity_mcp.tools.animator_intent_tool import build_animator_batch
    dsl_data = {
        "params": [("Speed", "float", "0")],
        "states": [("Idle", "Idle.anim"), ("Walk", "Walk.anim")],
        "default": "Idle",
        "transitions": [{"source": "Idle", "target": "Walk", "duration": "0.15", "condition": "Speed>0.1"}],
    }
    lines = build_animator_batch("/Player", dsl_data)
    trans_line = next(l for l in lines if "add_transition" in l)
    assert "source=Idle" in trans_line
    assert "target=Walk" in trans_line
    assert "duration=0.15" in trans_line
    assert "conditions=Speed>0.1" in trans_line


def test_animator_builder_empty_dsl():
    from unity_mcp.tools.animator_intent_tool import build_animator_batch
    dsl_data = {"params": [], "states": [], "default": None, "transitions": []}
    lines = build_animator_batch("/Player", dsl_data)
    assert lines == []


# ---------------------------------------------------------------------------
# 4. E2E mock (no SamplingService → error)
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_animator_intent_no_sampling_returns_error():
    from unity_mcp.tools.animator_intent_tool import animator_intent
    with patch("unity_mcp.tools.animator_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.animator_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=None)
            result = await animator_intent(target="/Player", intent="basic walk cycle")
            # ERROR or unavailable are both legitimate responses when sampling disabled
            assert re.search(r"ERROR|unavailable", result, re.IGNORECASE), result


@pytest.mark.asyncio
async def test_animator_intent_dry_run_returns_plan():
    from unity_mcp.tools.animator_intent_tool import animator_intent
    dsl = "PARAM Speed float 0\nSTATE Idle Idle.anim\nSTATE Walk Walk.anim\nDEFAULT Idle\nTRANS Idle -> Walk dur=0.15 if Speed>0.1"
    with patch("unity_mcp.tools.animator_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with patch("unity_mcp.tools.animator_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await animator_intent(target="/Player", intent="basic walk cycle", dry_run=True)
            mock_send.assert_not_called()
            assert "animator" in result


@pytest.mark.asyncio
async def test_animator_intent_e2e_executes_batch():
    from unity_mcp.tools.animator_intent_tool import animator_intent
    dsl = "PARAM Speed float 0\nSTATE Idle Idle.anim\nSTATE Walk Walk.anim\nDEFAULT Idle\nTRANS Idle -> Walk dur=0.15 if Speed>0.1"
    with patch("unity_mcp.tools.animator_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 4 ops"
        with patch("unity_mcp.tools.animator_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await animator_intent(target="/Player", intent="basic walk cycle")
            mock_send.assert_called_once()
            call_args = mock_send.call_args
            assert call_args[0][0] == "batch"
            assert "ok" in result
