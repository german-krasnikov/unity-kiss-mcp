"""Tests for SchemaCache."""
import pytest
from unity_mcp.schema_cache import SchemaCache


# ── Fix 9: schema_cache 'Cannot instantiate' ─────────────────────────────────

def test_schema_cache_parse_returns_empty_on_cannot_instantiate():
    """Fix 9: SchemaCache.parse must return empty frozenset for 'Cannot instantiate' text."""
    result = SchemaCache.parse("Cannot instantiate abstract class Component")
    assert result == frozenset()


def test_schema_cache_parse_type_not_found_still_empty():
    """Fix 9: existing 'Type not found' guard still works."""
    result = SchemaCache.parse("Type not found: Foo")
    assert result == frozenset()
