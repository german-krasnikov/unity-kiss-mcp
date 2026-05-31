"""Tests for ResponseDistiller — heuristic + Haiku fallback + validation."""
import pytest
from unity_mcp.distiller import ResponseDistiller, DistillResult


def test_heuristic_skip_small_input():
    d = ResponseDistiller()
    text = "/A\n/B"  # < 1500 chars
    result = d.distill_heuristic("get_hierarchy", text, ("/A",))
    assert result.method == "skip"
    assert result.text == text


def test_heuristic_skip_write_cmd():
    d = ResponseDistiller(min_size=10)
    text = "Created /A\n" + "x" * 2000
    result = d.distill_heuristic("set_property", text, ("/A",))
    assert result.method == "skip"


def test_heuristic_hierarchy_keeps_focus_branch():
    d = ResponseDistiller(min_size=10)
    text = "Scene Total: 5\n/Player\n  /Player/Health\n/Enemy\n  /Enemy/AI\n/Camera\n/UI/Panel\n/Light"
    result = d.distill_heuristic("get_hierarchy", text, ("/Player",))
    assert "/Player" in result.text
    assert "/Player/Health" in result.text
    # Other branches dropped
    assert "/Camera" not in result.text or "[... +" in result.text
    assert result.method in ("heuristic", "passthrough")


def test_heuristic_inspect_section_mode():
    d = ResponseDistiller(min_size=10)
    text = (
        "--- /Player ---\n"
        "[Health]\nhp: 100\n"
        "--- /Enemy ---\n"
        "[AI]\nstate: idle\n"
        "--- /Camera ---\n"
        "[Camera]\nfov: 60\n"
    )
    result = d.distill_heuristic("inspect", text, ("/Player",))
    assert "/Player" in result.text
    assert "/Enemy" not in result.text
    assert "+2 sections hidden" in result.text or "+1 sections hidden" in result.text


def test_heuristic_passthrough_when_kept_gte_90pct():
    d = ResponseDistiller(min_size=10)
    # Only 1 line out of focus
    text = "/Player\n" * 100 + "/X\n"
    result = d.distill_heuristic("get_hierarchy", text, ("/Player",))
    assert result.method == "passthrough"
    assert result.text == text


def test_heuristic_no_focus_returns_skip():
    d = ResponseDistiller(min_size=10)
    text = "x" * 2000
    result = d.distill_heuristic("get_hierarchy", text, ())
    assert result.method == "skip"


def test_heuristic_appends_hidden_sentinel():
    d = ResponseDistiller(min_size=10)
    text = "Scene\n/A\n/B\n/C\n/D\n/E\n/F\n/G\n/H\n/I\n/J\n" * 10  # ~700 chars, then * 4
    text = text * 4
    result = d.distill_heuristic("get_hierarchy", text, ("/A",))
    # Hidden sentinel present
    if result.method == "heuristic":
        assert "hidden" in result.text


def test_validate_substring_paths():
    original = "/Player\n/Enemy"
    # Valid: subset
    assert ResponseDistiller.validate_distilled(original, "/Player") is True
    # Invalid: hallucinated path
    assert ResponseDistiller.validate_distilled(original, "/Hallucinated") is False


def test_validate_well_formed():
    original = "Scene\n/A"
    # Orphan bracket
    assert ResponseDistiller.validate_distilled(original, "/A [orphan") is False


def test_validate_size_smaller():
    original = "/A"
    # Distilled larger than original — invalid
    assert ResponseDistiller.validate_distilled(original, "/A\n/A\n/A") is False


def test_extract_paths():
    text = "/Player/Health\n/Enemy\nplain text"
    paths = ResponseDistiller.extract_paths(text)
    assert "/Player/Health" in paths
    assert "/Enemy" in paths


def test_paths_overlap_no_false_positive_on_prefix():
    d = ResponseDistiller()
    # /PlayerCar must NOT match /Player
    assert not d._paths_overlap("/PlayerCar", "/Player")
    assert not d._paths_overlap("/PlayerCar/Wheel", "/Player")
    # /Player/Health MUST match /Player
    assert d._paths_overlap("/Player/Health", "/Player")
    assert d._paths_overlap("/Player", "/Player/Health")
    # Equal paths
    assert d._paths_overlap("/A", "/A")


@pytest.mark.asyncio
async def test_distill_haiku_returns_none_when_sampling_disabled():
    d = ResponseDistiller(sampling=None)
    result = await d.distill_haiku("get_hierarchy", "x" * 2000, ("/Player",))
    assert result is None


@pytest.mark.asyncio
async def test_distill_haiku_unknown_cmd_returns_none():
    from unittest.mock import AsyncMock, MagicMock
    sampling = MagicMock()
    d = ResponseDistiller(sampling=sampling)
    result = await d.distill_haiku("unknown_cmd", "x" * 2000, ("/Player",))
    assert result is None


@pytest.mark.asyncio
async def test_distill_haiku_validation_rejects_hallucination():
    from unittest.mock import AsyncMock
    sampling = AsyncMock()
    sampling.generate = AsyncMock(return_value="/Hallucinated/Path")
    d = ResponseDistiller(sampling=sampling)
    result = await d.distill_haiku("get_hierarchy", "/Player\n/Enemy", ("/Player",))
    assert result is None


@pytest.mark.asyncio
async def test_distill_haiku_accepts_valid_subset():
    from unittest.mock import AsyncMock
    sampling = AsyncMock()
    # Returns shorter, all paths in original
    sampling.generate = AsyncMock(return_value="/Player")
    d = ResponseDistiller(sampling=sampling)
    text = "/Player\n/Enemy\n/Camera\n" * 100  # > 1500 chars
    result = await d.distill_haiku("get_hierarchy", text, ("/Player",))
    assert result is not None
    assert result.method == "haiku"
    assert result.distilled_size < result.original_size


@pytest.mark.asyncio
async def test_distill_haiku_handles_exception():
    from unittest.mock import AsyncMock
    sampling = AsyncMock()
    sampling.generate = AsyncMock(side_effect=RuntimeError("boom"))
    d = ResponseDistiller(sampling=sampling)
    result = await d.distill_haiku("get_hierarchy", "x" * 2000, ("/Player",))
    assert result is None


# ---------------------------------------------------------------------------
# strip_defaults integration
# ---------------------------------------------------------------------------

def test_strip_defaults_applied_for_get_component():
    d = ResponseDistiller(min_size=10)
    text = "[Transform]\nposition: (0, 0, 0)\nlocalPosition: (5, 3, 0)\nrotation: (0, 0, 0, 1)"
    result = d.distill_heuristic("get_component", text, ())
    assert "position: (0, 0, 0)" not in result.text
    assert "rotation: (0, 0, 0, 1)" not in result.text
    assert "localPosition: (5, 3, 0)" in result.text


def test_strip_defaults_applied_for_inspect():
    d = ResponseDistiller(min_size=10)
    text = "[Rigidbody]\ndrag: 0\nmass: 5\nvelocity: (0, 0, 0)"
    result = d.distill_heuristic("inspect", text, ())
    assert "drag" not in result.text
    assert "velocity" not in result.text
    assert "mass: 5" in result.text


def test_strip_defaults_not_applied_for_hierarchy():
    d = ResponseDistiller(min_size=10)
    text = "drag: 0\nmass: 5"
    result = d.distill_heuristic("get_hierarchy", text, ())
    assert "drag: 0" in result.text
