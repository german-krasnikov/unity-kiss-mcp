"""Tests for readme_facts.py — collect/meta tests (split from test_single_source.py)."""
import json
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import readme_facts as rf

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_META = REPO_ROOT / "docs" / "assets" / "_meta.json"


class TestCollectFacts:
    def test_returns_all_keys(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        for k in ("tools", "tests_total", "tests_python", "tests_unity",
                  "tests_live", "server_version", "plugin_version", "batch_savings"):
            assert k in f, f"missing key: {k}"

    def test_tools_is_int_and_plausible(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        assert isinstance(f["tools"], int) and f["tools"] >= 90

    def test_versions_are_strings(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        assert isinstance(f["server_version"], str) and "." in f["server_version"]
        assert isinstance(f["plugin_version"], str) and "." in f["plugin_version"]

    def test_test_counts_are_ints(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        for k in ("tests_total", "tests_python", "tests_unity", "tests_live"):
            assert isinstance(f[k], int) and f[k] >= 0

    def test_totals_add_up(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        assert f["tests_total"] == f["tests_python"] + f["tests_unity"] + f["tests_live"]

    def test_batch_savings_string(self) -> None:
        f = rf.collect_facts(REPO_ROOT)
        assert isinstance(f["batch_savings"], str) and "%" in f["batch_savings"]

    def test_deterministic(self) -> None:
        f1, f2 = rf.collect_facts(REPO_ROOT), rf.collect_facts(REPO_ROOT)
        assert f1["tools"] == f2["tools"]
        assert f1["server_version"] == f2["server_version"]


class TestMetaJson:
    def test_meta_json_exists(self) -> None:
        assert _META.exists(), "_meta.json not written yet — run --collect first"

    def test_meta_json_is_valid(self) -> None:
        data = json.loads(_META.read_text())
        assert isinstance(data, dict) and "tools" in data

    def test_meta_json_tools_matches_real_count(self) -> None:
        data = json.loads(_META.read_text())
        live_count = rf.count_mcp_tools(REPO_ROOT / "server" / "src" / "unity_mcp")
        assert data["tools"] == live_count
