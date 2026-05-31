"""Unit tests for code_intel tools — mock_bridge with canned C# response format."""
import pytest
from pathlib import Path
from unittest.mock import AsyncMock


FIXTURES = Path(__file__).parent / "fixtures" / "roslyn_responses.txt"


# --- find_references ---

@pytest.mark.asyncio
async def test_find_references_passes_symbol_only_no_optional_args(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "SYMBOL Health class @ Assets/Scripts/Health.cs:5\n..."})

    await find_references("Health")

    args = mock_bridge.send.call_args[0][1]
    assert mock_bridge.send.call_args[0][0] == "find_references"
    assert args == {"symbol": "Health"}
    assert "kind" not in args
    assert "scope" not in args


@pytest.mark.asyncio
async def test_find_references_passes_kind_when_set(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "SYMBOL Health class @ ..."})

    await find_references("Health", kind="class")

    args = mock_bridge.send.call_args[0][1]
    assert args["kind"] == "class"
    assert args["symbol"] == "Health"


@pytest.mark.asyncio
async def test_find_references_passes_scope_when_set(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "SYMBOL Health class @ ..."})

    await find_references("Health", scope="Assembly-CSharp")

    args = mock_bridge.send.call_args[0][1]
    assert args["scope"] == "Assembly-CSharp"
    assert "kind" not in args


@pytest.mark.asyncio
async def test_find_references_passthrough_response(mock_bridge):
    """Tool returns C# text verbatim — no parsing on Python side (avoid drift)."""
    from unity_mcp.tools.code_intel import find_references

    canned = (
        "SYMBOL Health class @ Assets/Scripts/Health.cs:5\n"
        "Assets/Scripts/Player.cs:42:13 fieldRef\n"
        "1 refs in 1 files"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await find_references("Health")

    assert "SYMBOL Health class" in result
    assert "Player.cs:42:13" in result


@pytest.mark.asyncio
async def test_find_references_ambiguous_response(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    canned = (
        "AMBIGUOUS Health\n"
        "  class @ Assets/Scripts/Health.cs:5\n"
        "  field @ Assets/Scripts/Player.cs:12\n"
        "specify kind=class|field|method|property|param|local|namespace"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await find_references("Health")

    assert "AMBIGUOUS" in result
    assert "specify kind=" in result


@pytest.mark.asyncio
async def test_find_references_not_found_response(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    canned = "NOT FOUND Health\ncandidates: HealthBar (class), HealthSystem (class)"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await find_references("Health")

    assert "NOT FOUND" in result
    assert "candidates:" in result


@pytest.mark.asyncio
async def test_find_references_roslyn_unavailable(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    canned = "[ROSLYN UNAVAILABLE: Microsoft.CodeAnalysis.dll not found in Mono path] fallback: use grep/Find In Files"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await find_references("Health")

    assert "[ROSLYN UNAVAILABLE:" in result
    assert "fallback:" in result


@pytest.mark.asyncio
async def test_find_references_uses_10s_timeout(mock_bridge):
    from unity_mcp.tools.code_intel import find_references

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "SYMBOL x ..."})

    await find_references("x")

    assert mock_bridge.send.call_args[1]["timeout"] == 10.0


# --- compile_preflight ---

@pytest.mark.asyncio
async def test_compile_preflight_clean(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    canned = "OK preflight Assets/Scripts/Player.cs (3 asms recompiled, 142ms)"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await compile_preflight("Assets/Scripts/Player.cs", "public class Player {}")

    args = mock_bridge.send.call_args[0][1]
    assert mock_bridge.send.call_args[0][0] == "compile_preflight"
    assert args["file_path"] == "Assets/Scripts/Player.cs"
    assert args["new_content"] == "public class Player {}"
    assert "OK preflight" in result


@pytest.mark.asyncio
async def test_compile_preflight_with_errors(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    canned = (
        "ERR preflight Assets/Scripts/Player.cs (2 errors, 89ms)\n"
        "Player.cs(42,13): CS0103 The name 'helath' does not exist in the current context\n"
        "Player.cs(58,5): CS1002 ; expected"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await compile_preflight("Assets/Scripts/Player.cs", "broken code")

    assert "ERR preflight" in result
    assert "CS0103" in result
    assert "helath" in result


@pytest.mark.asyncio
async def test_compile_preflight_uses_15s_timeout(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK preflight ..."})

    await compile_preflight("Assets/x.cs", "content")

    assert mock_bridge.send.call_args[1]["timeout"] == 15.0


# --- semantic_at ---

@pytest.mark.asyncio
async def test_semantic_at_symbol_info(mock_bridge):
    from unity_mcp.tools.code_intel import semantic_at

    canned = (
        "Health class\n"
        "decl: Assets/Scripts/Health.cs:5:14\n"
        "namespace: Game.Combat\n"
        "members: int currentHp, void TakeDamage(int), event Action OnDeath"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await semantic_at("Assets/Scripts/Health.cs", line=5, col=14)

    args = mock_bridge.send.call_args[0][1]
    assert mock_bridge.send.call_args[0][0] == "semantic_at"
    assert args["file_path"] == "Assets/Scripts/Health.cs"
    assert args["line"] == 5
    assert args["col"] == 14
    assert "Health class" in result
    assert "namespace: Game.Combat" in result


@pytest.mark.asyncio
async def test_semantic_at_no_symbol(mock_bridge):
    from unity_mcp.tools.code_intel import semantic_at

    canned = "NO SYMBOL at Assets/Scripts/Player.cs:42:13 (whitespace or syntax error)"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await semantic_at("Assets/Scripts/Player.cs", 42, 13)

    assert "NO SYMBOL" in result


@pytest.mark.asyncio
async def test_semantic_at_line_col_int_coercion(mock_bridge):
    """Tool coerces line/col to int even if str passed (e.g. from JSON args)."""
    from unity_mcp.tools.code_intel import semantic_at

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Health class\n..."})

    # Pass strings — should be coerced
    await semantic_at("Assets/x.cs", "5", "14")

    args = mock_bridge.send.call_args[0][1]
    assert isinstance(args["line"], int)
    assert isinstance(args["col"], int)
    assert args["line"] == 5
    assert args["col"] == 14


@pytest.mark.asyncio
async def test_semantic_at_uses_10s_timeout(mock_bridge):
    from unity_mcp.tools.code_intel import semantic_at

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "Health class\n..."})

    await semantic_at("Assets/x.cs", 1, 1)

    assert mock_bridge.send.call_args[1]["timeout"] == 10.0


# --- gating ---

def test_all_three_tools_registered_in_tier1():
    """Verify gating.py exposes these 3 tools in TIER1."""
    from unity_mcp.tools.gating import TIER1
    assert "find_references" in TIER1
    assert "compile_preflight" in TIER1
    assert "semantic_at" in TIER1


def test_fixture_file_exists():
    """roslyn_responses.txt fixture exists and has expected sections."""
    text = FIXTURES.read_text()
    assert "find_references" in text
    assert "compile_preflight" in text
    assert "semantic_at" in text
    assert "ROSLYN UNAVAILABLE" in text
