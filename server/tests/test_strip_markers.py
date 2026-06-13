"""Unit tests for strip_markers helper used in live tests."""
# Import from live conftest — will fail until implemented
from tests.live.conftest import strip_markers


def test_strip_markers_removes_confidence_suffix():
    text = "hello\n[confidence: 1.00]extra stuff"
    assert strip_markers(text) == "hello"


def test_strip_markers_preserves_clean_text():
    assert strip_markers("clean response") == "clean response"


def test_strip_markers_inline_confidence():
    """[confidence:...] mid-line: only strip from the newline before it."""
    text = "line1\nline2\n[confidence: 0.87] something"
    assert strip_markers(text) == "line1\nline2"


def test_strip_markers_empty_string():
    assert strip_markers("") == ""


def test_strip_markers_only_marker():
    assert strip_markers("[confidence: 1.00]") == ""
