"""F06: TIER1 tool descriptions stay terse but keep enums/grammar (anti-hallucination).

Two-sided lock: a char budget prevents prose creep; required-substring asserts
prevent a trim from silently dropping enum values the LLM needs to avoid hallucination.
"""
from unity_mcp.tools.scene import screenshot
from unity_mcp.tools.code_intel import find_references, compile_preflight, semantic_at
from unity_mcp.tools.runtime import run_playtest


def test_screenshot_doc_terse_keeps_enums():
    doc = screenshot.__doc__
    assert len(doc) < 520, f"screenshot doc creeping back to bloat: {len(doc)} chars"
    for token in ("scene_view", "multi_view", "single_view", "overview_game",
                  "front|left|top|iso", "supersample", "highlight", "show_colliders"):
        assert token in doc, f"screenshot doc lost anti-hallucination token: {token}"


def test_find_references_doc_terse_keeps_kind_enum():
    doc = find_references.__doc__
    assert len(doc) < 420, f"find_references doc bloat: {len(doc)} chars"
    assert "class|field|method|property|param|local|namespace" in doc
    assert "ROSLYN UNAVAILABLE" in doc


def test_compile_preflight_doc_terse():
    doc = compile_preflight.__doc__
    assert len(doc) < 320, f"compile_preflight doc bloat: {len(doc)} chars"
    assert "ROSLYN UNAVAILABLE" in doc


def test_semantic_at_doc_terse():
    doc = semantic_at.__doc__
    assert len(doc) < 320, f"semantic_at doc bloat: {len(doc)} chars"
    assert "1-based" in doc


def test_run_playtest_doc_keeps_full_dsl_grammar():
    """run_playtest is mostly irreducible DSL grammar — must NOT be trimmed away."""
    doc = run_playtest.__doc__
    for cmd in ("MOVE TO", "WAIT_UNTIL", "ASSERT_CONSOLE_CLEAN", "ASSERT_CONSERVED",
                "ASSERT_CTA", "TRACE_FLOW", "INVARIANT", "SIMULATE"):
        assert cmd in doc, f"run_playtest lost DSL command (hallucination risk): {cmd}"
