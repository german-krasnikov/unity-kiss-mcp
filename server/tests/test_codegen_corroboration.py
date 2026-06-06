"""TDD tests: codegen.auto_fix corroboration via editor_log.corroborate + smart_build branches."""
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


@pytest.mark.asyncio
async def test_auto_fix_corroborates_get_compile_errors(monkeypatch):
    """auto_fix must pass get_compile_errors result through editor_log.corroborate."""
    import unity_mcp.editor_log as el
    import unity_mcp.tools.codegen as codegen

    raw_csharp = "No compilation errors."
    corroborated = "[editor.log - dll stale]\nAssets/Foo.cs:1:1: error CS0117: stale"

    send_mock = AsyncMock(side_effect=lambda cmd, args=None, **kw: (
        "some console errors" if cmd == "get_console" else raw_csharp
    ))
    monkeypatch.setattr(codegen, "_send", send_mock)

    ctx = MagicMock()

    with patch.object(el, "corroborate", return_value=corroborated) as mock_cor:
        result = await codegen.auto_fix(ctx)

    mock_cor.assert_called_once_with(raw_csharp)
    # auto_fix sees corroborated (has errors) → includes error in output
    assert "CS0117" in result or "stale" in result


@pytest.mark.asyncio
async def test_auto_fix_no_errors_returns_no_errors_to_fix(monkeypatch):
    """auto_fix with clean corroborate result → 'No errors to fix.'"""
    import unity_mcp.editor_log as el
    import unity_mcp.tools.codegen as codegen

    send_mock = AsyncMock(side_effect=lambda cmd, args=None, **kw: (
        "" if cmd == "get_console" else "No compilation errors."
    ))
    monkeypatch.setattr(codegen, "_send", send_mock)

    ctx = MagicMock()

    with patch.object(el, "corroborate", return_value="No compilation errors."):
        result = await codegen.auto_fix(ctx)

    assert result == "No errors to fix."


# ---------------------------------------------------------------------------
# smart_build branch coverage
# ---------------------------------------------------------------------------

def _make_ctx(text: str) -> MagicMock:
    """Build a mock ctx whose session.create_message returns text."""
    content = MagicMock()
    content.text = text
    response = MagicMock()
    response.content = [content]
    ctx = MagicMock()
    ctx.session.create_message = AsyncMock(return_value=response)
    return ctx


@pytest.mark.asyncio
async def test_smart_build_fenced_csharp_extracted(monkeypatch):
    """smart_build strips ```csharp fences and passes inner code to execute_code."""
    import unity_mcp.tools.codegen as codegen

    inner = "var go = new GameObject(\"Test\");"
    llm_text = f"Here you go:\n```csharp\n{inner}\n```"
    ctx = _make_ctx(llm_text)

    send_mock = AsyncMock(return_value="ok")
    monkeypatch.setattr(codegen, "_send", send_mock)

    await codegen.smart_build("create a cube", ctx)

    send_mock.assert_called_once()
    # _send("execute_code", {"code": ...}) — second positional arg is the dict
    called_code = send_mock.call_args[0][1]["code"]
    assert called_code == inner


@pytest.mark.asyncio
async def test_smart_build_unfenced_windows_line_endings(monkeypatch):
    """smart_build handles Windows \\r\\n unfenced response correctly."""
    import unity_mcp.tools.codegen as codegen

    inner = "var go = new GameObject(\"Test\");\r\nreturn go.name;"
    ctx = _make_ctx(inner)

    send_mock = AsyncMock(return_value="ok")
    monkeypatch.setattr(codegen, "_send", send_mock)

    await codegen.smart_build("create a cube", ctx)

    send_mock.assert_called_once()
    called_code = send_mock.call_args[0][1]["code"]
    assert "GameObject" in called_code


@pytest.mark.asyncio
async def test_smart_build_empty_response_returns_early(monkeypatch):
    """smart_build with empty LLM response returns without calling execute_code."""
    import unity_mcp.tools.codegen as codegen

    ctx = _make_ctx("   ")  # whitespace-only

    send_mock = AsyncMock(return_value="ok")
    monkeypatch.setattr(codegen, "_send", send_mock)

    result = await codegen.smart_build("create a cube", ctx)

    send_mock.assert_not_called()
    assert "empty" in result.lower()
