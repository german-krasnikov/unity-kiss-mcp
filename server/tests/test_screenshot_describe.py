"""TDD tests for screenshot_describe package."""
import time
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


# ── prompts ──────────────────────────────────────────────────────────────────

def test_prompts_resolve_canned_returns_tuple():
    from unity_mcp.screenshot_describe.prompts import resolve
    prompt, max_tok = resolve("auto")
    assert isinstance(prompt, str) and len(prompt) > 0
    assert isinstance(max_tok, int) and max_tok > 0


def test_prompts_resolve_unknown_passes_through_as_custom():
    from unity_mcp.screenshot_describe.prompts import resolve
    custom = "Is there a red cube visible?"
    prompt, max_tok = resolve(custom)
    assert prompt == custom
    assert max_tok == 150


def test_prompts_all_canned_keys_return_valid_tuples():
    from unity_mcp.screenshot_describe.prompts import PROMPTS, resolve
    for key in PROMPTS:
        p, t = resolve(key)
        assert isinstance(p, str) and len(p) > 10
        assert isinstance(t, int) and 10 <= t <= 500


# ── cache ─────────────────────────────────────────────────────────────────────

def test_cache_get_returns_none_when_empty():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    c = FingerprintCache()
    assert c.get("fp123", "auto") is None


def test_cache_put_then_get_returns_value():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    c = FingerprintCache()
    c.put("fp123", "auto", "A red cube in center.")
    assert c.get("fp123", "auto") == "A red cube in center."


def test_cache_ttl_expires():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    c = FingerprintCache(ttl=0.01)
    c.put("fp123", "auto", "desc")
    time.sleep(0.02)
    assert c.get("fp123", "auto") is None


def test_cache_lru_eviction_at_max():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    c = FingerprintCache(max_entries=3)
    c.put("a", "p", "v1")
    c.put("b", "p", "v2")
    c.put("c", "p", "v3")
    c.put("d", "p", "v4")  # evicts "a"
    assert c.get("a", "p") is None
    assert c.get("d", "p") == "v4"


# ── describer ─────────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_describer_cache_hit_skips_sampling():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.prompts import resolve

    cache = FingerprintCache()
    # Store under resolved prompt text (as describer does internally)
    resolved_prompt, _ = resolve("auto")
    cache.put("fp_abc", resolved_prompt, "Cached description.")

    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock()

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_abc")

    assert result == "Cached description."
    mock_sampling.describe_image.assert_not_called()


@pytest.mark.asyncio
async def test_describer_cache_miss_calls_sampling_and_stores():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.prompts import resolve

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value="Fresh description.")

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_xyz")

    assert result == "Fresh description."
    # Verify stored under resolved prompt
    resolved_prompt, _ = resolve("auto")
    assert cache.get("fp_xyz", resolved_prompt) == "Fresh description."


@pytest.mark.asyncio
async def test_describer_sampling_returns_none_no_cache_write():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.prompts import resolve

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value=None)

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_none")

    # After degrade() wiring: returns degraded marker, not None
    assert result is not None
    assert "[DEGRADED:screenshot_describe:" in result
    resolved_prompt, _ = resolve("auto")
    assert cache.get("fp_none", resolved_prompt) is None


# ── degrade() wiring for describer ────────────────────────────────────────────

@pytest.mark.asyncio
async def test_describe_no_cache_no_haiku():
    """Both haiku and cache fail → describe_disabled rung."""
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value=None)

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_never_seen")

    assert "[DEGRADED:screenshot_describe:describe_disabled]" in result
    assert "(describe unavailable)" in result


# ── #1: describer all rungs None → no_fallback marker, no \nNone ──────────────

@pytest.mark.asyncio
async def test_describer_all_rungs_none_no_fallback_marker(monkeypatch):
    """#1: degrade() returns (step, None) → output must use wrap_degraded, not inline f-string."""
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe import describer as describer_mod
    from unity_mcp import degrade as degrade_mod

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value=None)

    async def fake_degrade(feature, steps):
        last_name = steps[-1][0] if steps else "unknown"
        return (last_name, None)

    monkeypatch.setattr(describer_mod, "degrade", fake_degrade)

    d = describer_mod.ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_test_nofallback")

    assert ":no_fallback]" in result, f"Expected :no_fallback] in {result!r}"
    assert "\nNone" not in result, f"Got literal None suffix: {result!r}"


# ── screenshot() tool integration ─────────────────────────────────────────────

@pytest.mark.asyncio
async def test_screenshot_default_describe_none_returns_path(mock_bridge):
    """CRITICAL backward compat: describe=None must not call Haiku."""
    from unity_mcp.tools.scene import screenshot

    mock_bridge.send.return_value = {"ok": True, "data": "Data saved to: /tmp/shot.png"}

    result = await screenshot()
    assert "Data saved to:" in result
    assert "img:" not in result


@pytest.mark.asyncio
async def test_screenshot_describe_auto_returns_text_with_img_suffix(mock_bridge):
    from unity_mcp.tools.scene import screenshot

    mock_bridge.send.return_value = {"ok": True, "data": "Data saved to: /tmp/shot.png"}

    mock_describer = MagicMock()
    mock_describer.describe = AsyncMock(return_value="A red cube in the center.")

    with patch("unity_mcp.tools.scene._get_describer_safe",
               return_value=mock_describer):
        result = await screenshot(describe="auto")

    assert "A red cube in the center." in result
    assert "[img:" in result


@pytest.mark.asyncio
async def test_screenshot_raw_true_forces_path_even_with_describe(mock_bridge):
    from unity_mcp.tools.scene import screenshot

    mock_bridge.send.return_value = {"ok": True, "data": "Data saved to: /tmp/shot.png"}

    result = await screenshot(describe="auto", raw=True)
    assert "Data saved to:" in result
    assert "[img:" not in result


@pytest.mark.asyncio
async def test_screenshot_describe_sampling_unavailable_returns_path(mock_bridge):
    from unity_mcp.tools.scene import screenshot

    mock_bridge.send.return_value = {"ok": True, "data": "Data saved to: /tmp/shot.png"}

    with patch("unity_mcp.tools.scene._get_describer_safe", return_value=None):
        result = await screenshot(describe="auto")

    assert "Data saved to:" in result


# ── ITEM 6: refusal detection ─────────────────────────────────────────────────

def test_is_refusal_detects_variants():
    from unity_mcp.screenshot_describe.describer import _is_refusal
    assert _is_refusal("I cannot help with that")
    assert _is_refusal("I'm unable to process")
    assert _is_refusal("Sorry, I can't describe")
    assert not _is_refusal("A button labeled Login")
    assert not _is_refusal("I see a red cube")


def test_is_refusal_allows_hedge_with_description():
    from unity_mcp.screenshot_describe.describer import _is_refusal
    assert not _is_refusal("I can't make out the text clearly, but I see a red cube")
    assert not _is_refusal("I cannot tell exactly, however the player is visible")
    # Real refusals still detected:
    assert _is_refusal("I cannot help with that.")
    assert _is_refusal("I can't process this image.")


@pytest.mark.asyncio
async def test_refusal_returns_degraded_not_cached():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.prompts import resolve

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value="I cannot describe this image.")

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_refusal")

    assert "[DEGRADED:screenshot_describe:haiku_refused]" in result
    # Not cached
    resolved_prompt, _ = resolve("auto")
    assert cache.get("fp_refusal", resolved_prompt) is None


@pytest.mark.asyncio
async def test_legitimate_description_cached():
    from unity_mcp.screenshot_describe.cache import FingerprintCache
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.prompts import resolve

    cache = FingerprintCache()
    mock_sampling = MagicMock()
    mock_sampling.describe_image = AsyncMock(return_value="A red cube in center.")

    d = ScreenshotDescriber(mock_sampling, cache)
    result = await d.describe("/fake/path.png", "auto", "fp_legit")

    assert result == "A red cube in center."
    resolved_prompt, _ = resolve("auto")
    assert cache.get("fp_legit", resolved_prompt) == "A red cube in center."
