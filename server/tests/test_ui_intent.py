"""TDD tests for ui_intent tool."""
import pytest
from unittest.mock import AsyncMock, patch


# ---------------------------------------------------------------------------
# 1. DSL Parser — indent-based
# ---------------------------------------------------------------------------

def test_ui_parse_dsl_flat_canvas():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas"
    nodes = parse_ui_dsl(dsl)
    assert len(nodes) == 1
    assert nodes[0]["type"] == "canvas"
    assert nodes[0]["name"] == "Canvas"
    assert nodes[0]["parent"] is None


def test_ui_parse_dsl_nested():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas\n  panel HUD anchor=stretch"
    nodes = parse_ui_dsl(dsl)
    assert nodes[1]["type"] == "panel"
    assert nodes[1]["name"] == "HUD"
    assert nodes[1]["attrs"]["anchor"] == "stretch"
    assert nodes[1]["parent"] == "Canvas"


def test_ui_parse_dsl_deep_nesting():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas\n  panel HUD\n    image HealthBar anchor=top-left pos=20,-20 size=200,30"
    nodes = parse_ui_dsl(dsl)
    assert nodes[2]["parent"] == "HUD"
    assert nodes[2]["attrs"]["anchor"] == "top-left"
    assert nodes[2]["attrs"]["pos"] == "20,-20"


def test_ui_parse_dsl_quoted_text():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = '    button Play text="Play Game"'
    nodes = parse_ui_dsl(dsl)
    assert nodes[0]["attrs"]["text"] == "Play Game"


def test_ui_parse_dsl_color_attr():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "    image HealthBar color=#c33"
    nodes = parse_ui_dsl(dsl)
    assert nodes[0]["attrs"]["color"] == "#c33"


# ---------------------------------------------------------------------------
# 2. Templates — bypass Haiku
# ---------------------------------------------------------------------------

def test_ui_template_hud_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    dsl = get_template_dsl("hud")
    assert dsl is not None
    assert "canvas" in dsl.lower()


def test_ui_template_menu_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    dsl = get_template_dsl("menu")
    assert dsl is not None
    assert "button" in dsl.lower()


def test_ui_template_dialog_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("dialog") is not None


def test_ui_template_grid_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("grid") is not None


def test_ui_template_unknown_returns_none():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("nonexistent") is None


# ---------------------------------------------------------------------------
# 3. Builder
# ---------------------------------------------------------------------------

def test_ui_builder_canvas():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    assert any("create_ui" in l and "Canvas" in l for l in lines)


def test_ui_builder_image_with_attrs():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left pos=20,-20 size=200,30 color=#c33"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    # Should produce create_ui and set_rect
    assert any("HealthBar" in l for l in lines)


def test_ui_builder_layout_group():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  layout Menu dir=vertical spacing=10"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    assert any("VerticalLayoutGroup" in l or "layout" in l.lower() for l in lines)


# ---------------------------------------------------------------------------
# 4. E2E mock
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_ui_intent_template_bypass_no_haiku():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 5 ops"
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            result = await ui_intent(intent="build UI", template="hud")
            mock_svc.generate.assert_not_called()
            assert "ok" in result


@pytest.mark.asyncio
async def test_ui_intent_no_sampling_returns_error():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=None)
            result = await ui_intent(intent="create a health bar UI")
            assert "ERROR" in result


@pytest.mark.asyncio
async def test_ui_intent_dry_run():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left"
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await ui_intent(intent="health bar", dry_run=True)
            mock_send.assert_not_called()
            assert "create_ui" in result


@pytest.mark.asyncio
async def test_ui_intent_e2e_executes_batch():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left pos=20,-20 size=200,30"
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 3 ops"
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await ui_intent(intent="health bar HUD")
            mock_send.assert_called_once()
            assert mock_send.call_args[0][0] == "batch"
