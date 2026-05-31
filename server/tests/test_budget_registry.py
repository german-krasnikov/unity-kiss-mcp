"""TDD: budget registry — feature metadata."""
import pytest
from unity_mcp.budget.registry import FEATURES, FeatureMeta, get_feature, DEFAULT_FEATURE


def test_get_feature_known_returns_meta():
    meta = get_feature("do_intent")
    assert isinstance(meta, FeatureMeta)
    assert meta.priority == "critical"


def test_get_feature_unknown_returns_default():
    meta = get_feature("nonexistent_feature_xyz")
    assert meta == DEFAULT_FEATURE


def test_features_have_valid_priorities():
    valid = {"critical", "medium", "low"}
    for name, meta in FEATURES.items():
        assert meta.priority in valid, f"{name}.priority invalid: {meta.priority}"


def test_features_have_difficulty_in_range():
    for name, meta in FEATURES.items():
        assert 0.0 <= meta.difficulty <= 1.0, f"{name}.difficulty out of range"


def test_image_features_marked():
    for name in ("visual_verify", "visual_diff", "screenshot_describe"):
        assert FEATURES[name].image is True, f"{name} should be image=True"
