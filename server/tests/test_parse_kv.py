"""TDD tests for the unified parse_kv / parse_kv_line in utils.py."""
from unity_mcp.utils import parse_kv, parse_kv_line


def test_parse_kv_simple():
    assert parse_kv("key=value other=123") == {"key": "value", "other": "123"}


def test_parse_kv_quoted():
    assert parse_kv('key="hello world" b=2') == {"key": "hello world", "b": "2"}


def test_parse_kv_parens():
    assert parse_kv("value=(1, 0, 0)") == {"value": "(1, 0, 0)"}


def test_parse_kv_parens_no_spaces():
    assert parse_kv("value=(1,0,0)") == {"value": "(1,0,0)"}


def test_parse_kv_mixed():
    result = parse_kv("path=/Player component=Transform prop=m_LocalPosition value=(1, 0, 0)")
    assert result == {
        "path": "/Player",
        "component": "Transform",
        "prop": "m_LocalPosition",
        "value": "(1, 0, 0)",
    }


def test_parse_kv_empty():
    assert parse_kv("") == {}


def test_parse_kv_line_with_cmd():
    cmd, kv = parse_kv_line("set_property path=/Player value=(1,0,0)")
    assert cmd == "set_property"
    assert kv == {"path": "/Player", "value": "(1,0,0)"}


def test_parse_kv_line_empty():
    assert parse_kv_line("") == ("", {})


def test_parse_kv_line_cmd_only():
    cmd, kv = parse_kv_line("some_command")
    assert cmd == "some_command"
    assert kv == {}


def test_parse_kv_hex_color():
    assert parse_kv("color=#ff0000 bg=#c33") == {"color": "#ff0000", "bg": "#c33"}


def test_parse_kv_line_with_parens_and_spaces():
    cmd, kv = parse_kv_line("set_property path=/NPC value=(1, 0, 0) component=Transform")
    assert cmd == "set_property"
    assert kv["value"] == "(1, 0, 0)"
    assert kv["component"] == "Transform"
