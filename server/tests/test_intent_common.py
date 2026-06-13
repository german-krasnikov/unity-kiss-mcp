"""TDD tests for intent_common — shared DSL utilities."""
# ---------------------------------------------------------------------------
# 1. strip_fences
# ---------------------------------------------------------------------------

def test_strip_fences_removes_backtick_block():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "```\nPARAM Speed float 0\n```"
    assert strip_fences(raw) == "PARAM Speed float 0"


def test_strip_fences_removes_language_tagged_block():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "```dsl\nSTATE Idle Idle.anim\n```"
    assert strip_fences(raw) == "STATE Idle Idle.anim"


def test_strip_fences_passthrough_no_fences():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "PARAM Speed float 0"
    assert strip_fences(raw) == "PARAM Speed float 0"


def test_strip_fences_trims_whitespace():
    from unity_mcp.tools.intent_common import strip_fences
    raw = "  PARAM Speed float 0  "
    assert strip_fences(raw) == "PARAM Speed float 0"


# ---------------------------------------------------------------------------
# 2. parse_kv — key=value line parser
# ---------------------------------------------------------------------------

def test_parse_kv_basic():
    from unity_mcp.tools.intent_common import parse_kv
    result = parse_kv("anchor=top-left pos=20,-20 size=200,30")
    assert result == {"anchor": "top-left", "pos": "20,-20", "size": "200,30"}


def test_parse_kv_quoted_value():
    from unity_mcp.tools.intent_common import parse_kv
    result = parse_kv('text="Hello World" fontSize=24')
    assert result["text"] == "Hello World"
    assert result["fontSize"] == "24"


def test_parse_kv_empty_string():
    from unity_mcp.tools.intent_common import parse_kv
    assert parse_kv("") == {}


def test_parse_kv_single_pair():
    from unity_mcp.tools.intent_common import parse_kv
    assert parse_kv("color=#c33") == {"color": "#c33"}


# ---------------------------------------------------------------------------
# 3. parse_indent_tree — 2-space indent parser
# ---------------------------------------------------------------------------

def test_parse_indent_tree_flat():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas"
    nodes = parse_indent_tree(dsl)
    assert len(nodes) == 1
    assert nodes[0]["line"] == "canvas Canvas"
    assert nodes[0]["depth"] == 0
    assert nodes[0]["parent"] is None


def test_parse_indent_tree_nested():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n  panel HUD"
    nodes = parse_indent_tree(dsl)
    assert nodes[1]["depth"] == 1
    assert nodes[1]["parent"] == nodes[0]


def test_parse_indent_tree_deep_nesting():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n  panel HUD\n    image HealthBar"
    nodes = parse_indent_tree(dsl)
    assert nodes[2]["depth"] == 2
    assert nodes[2]["parent"] == nodes[1]


def test_parse_indent_tree_skips_empty_lines():
    from unity_mcp.tools.intent_common import parse_indent_tree
    dsl = "canvas Canvas\n\n  panel HUD"
    nodes = parse_indent_tree(dsl)
    assert len(nodes) == 2


# ---------------------------------------------------------------------------
# 4. build_batch_line — assemble command line from parts
# ---------------------------------------------------------------------------

def test_build_batch_line_simple():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("animator", path="/Player", action="add_param")
    assert line == "animator path=/Player action=add_param"


def test_build_batch_line_skips_none():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("create_ui", type="Image", name=None, parent="/HUD")
    assert "name" not in line
    assert "create_ui type=Image parent=/HUD" == line


def test_build_batch_line_no_extra_args():
    from unity_mcp.tools.intent_common import build_batch_line
    line = build_batch_line("create_object", name="Cube")
    assert line == "create_object name=Cube"
