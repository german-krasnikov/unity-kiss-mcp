"""TDD tests: codegen.auto_fix corroboration via editor_log.corroborate."""
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
