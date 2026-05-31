"""Tests for response compressor — strip default values from component reads."""
import pytest
from unity_mcp.compressor import strip_defaults, project_fields


SAMPLE_COMPONENT = """\
[Transform]
position: (0, 0, 0)
rotation: (0, 0, 0, 1)
localScale: (1, 1, 1)
localPosition: (5, 3, 0)
localRotation: (0, 0.5, 0, 0.866)
---
[Rigidbody]
mass: 1
drag: 0
angularDrag: 0.05
useGravity: true
isKinematic: false
velocity: (0, 0, 0)
"""


def test_strip_defaults_removes_zero_tuple():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "position: (0, 0, 0)" not in result


def test_strip_defaults_removes_identity_quaternion():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "rotation: (0, 0, 0, 1)" not in result


def test_strip_defaults_removes_unit_scale():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "localScale: (1, 1, 1)" not in result


def test_strip_defaults_keeps_nondefault_values():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "localPosition: (5, 3, 0)" in result
    assert "localRotation: (0, 0.5, 0, 0.866)" in result


def test_strip_defaults_keeps_section_headers():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "[Transform]" in result
    assert "[Rigidbody]" in result


def test_strip_defaults_keeps_separators():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "---" in result


def test_strip_defaults_removes_false():
    data = "[C]\nisKinematic: false\nenabled: true\n"
    result = strip_defaults(data)
    assert "isKinematic" not in result
    assert "enabled: true" in result


def test_strip_defaults_removes_zero_int():
    data = "[C]\ndrag: 0\nmass: 2\n"
    result = strip_defaults(data)
    assert "drag" not in result
    assert "mass: 2" in result


def test_strip_defaults_removes_null():
    data = "[C]\ntarget: null\nname: Player\n"
    result = strip_defaults(data)
    assert "target" not in result
    assert "name: Player" in result


def test_strip_defaults_removes_empty_string():
    data = '[C]\ntag: ""\nname: Cube\n'
    result = strip_defaults(data)
    assert 'tag' not in result
    assert "name: Cube" in result


def test_strip_defaults_removes_empty_list():
    data = "[C]\nevents: []\nchildren: [a, b]\n"
    result = strip_defaults(data)
    assert "events" not in result
    assert "children" in result


def test_strip_defaults_removes_transparent_color():
    data = "[C]\ncolor: #00000000\noutlineColor: #FF0000FF\n"
    result = strip_defaults(data)
    assert "color: #00000000" not in result
    assert "outlineColor" in result


def test_strip_defaults_keeps_error_lines():
    data = "[C]\nerr: something went wrong\nvalue: 0\n"
    result = strip_defaults(data)
    assert "err:" in result


def test_strip_defaults_noop_on_empty():
    assert strip_defaults("") == ""


def test_strip_defaults_removes_none_string():
    data = "[C]\nparent: None\nname: X\n"
    result = strip_defaults(data)
    assert "parent" not in result
    assert "name: X" in result


# ─── F08: expanded defaults ───────────────────────────────────────────────────

def test_strip_defaults_removes_mass_one():
    data = "[C]\nmass: 1\ndrag: 5\n"
    result = strip_defaults(data)
    assert "mass" not in result
    assert "drag: 5" in result


def test_strip_defaults_removes_untagged():
    data = "[C]\ntag: Untagged\nlayer: Default\nname: Cube\n"
    result = strip_defaults(data)
    assert "tag" not in result
    assert "layer" not in result
    assert "name: Cube" in result


def test_strip_defaults_removes_vector2_zero():
    data = "[C]\noffset: (0, 0)\nsize: (100, 50)\n"
    result = strip_defaults(data)
    assert "offset" not in result
    assert "size: (100, 50)" in result


def test_strip_defaults_removes_white_color():
    data = "[C]\ncolor: #FFFFFFFF\noutline: #FF0000FF\n"
    result = strip_defaults(data)
    assert "color" not in result
    assert "outline: #FF0000FF" in result


def test_strip_defaults_removes_vector4_zero():
    data = "[C]\npadding: (0, 0, 0, 0)\nmargin: (1, 2, 3, 4)\n"
    result = strip_defaults(data)
    assert "padding" not in result
    assert "margin: (1, 2, 3, 4)" in result


# ─── F07: fields= projection ──────────────────────────────────────────────────

def test_project_fields_keeps_only_requested():
    result = project_fields(SAMPLE_COMPONENT, "mass,localPosition")
    assert "mass: 1" in result
    assert "localPosition: (5, 3, 0)" in result
    assert "drag: 0" not in result
    assert "velocity" not in result


def test_project_fields_case_insensitive():
    result = project_fields(SAMPLE_COMPONENT, "MASS")
    assert "mass: 1" in result


def test_project_fields_dotted_subfields():
    data = "[Transform]\nm_LocalPosition.x: 5\nm_LocalPosition.y: 3\nm_Mass: 2\n"
    result = project_fields(data, "m_LocalPosition")
    assert "m_LocalPosition.x: 5" in result
    assert "m_LocalPosition.y: 3" in result
    assert "m_Mass" not in result


def test_project_fields_exact_subfield():
    data = "[T]\nm_LocalPosition.x: 5\nm_LocalPosition.y: 3\n"
    result = project_fields(data, "m_LocalPosition.x")
    assert "m_LocalPosition.x: 5" in result
    assert "m_LocalPosition.y" not in result


def test_project_fields_prefix_requires_dot_boundary():
    """Requesting 'pos' must NOT match 'position' (no loose substring matching)."""
    data = "[C]\nposition: (5, 0, 0)\npos: 1\n"
    result = project_fields(data, "pos")
    assert "pos: 1" in result
    assert "position:" not in result


def test_project_fields_keeps_headers_and_separators():
    result = project_fields(SAMPLE_COMPONENT, "mass")
    assert "[Transform]" in result
    assert "[Rigidbody]" in result
    assert "---" in result


def test_project_fields_keeps_error_lines():
    data = "[C]\nerr: not found\nmass: 1\n"
    result = project_fields(data, "drag")
    assert "err: not found" in result


def test_project_fields_empty_fields_returns_all():
    assert project_fields(SAMPLE_COMPONENT, "") == SAMPLE_COMPONENT
    assert project_fields(SAMPLE_COMPONENT, "  ,  ") == SAMPLE_COMPONENT


def test_project_fields_noop_on_empty_text():
    assert project_fields("", "mass") == ""
