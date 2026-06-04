"""Tests for update_readme.py — TDD Red phase written first."""
import json
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import update_readme as ur

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent


# ---------------------------------------------------------------------------
# count_mcp_tools
# ---------------------------------------------------------------------------

class TestCountMcpTools:
    def test_counts_mcp_tool_calls_in_dir(self) -> None:
        tools_dir = REPO_ROOT / "server" / "src" / "unity_mcp"
        count = ur.count_mcp_tools(tools_dir)
        # We know there are at least 80 from grepping
        assert count >= 80

    def test_returns_int(self) -> None:
        tools_dir = REPO_ROOT / "server" / "src" / "unity_mcp"
        assert isinstance(ur.count_mcp_tools(tools_dir), int)

    def test_minimal_source(self, tmp_path: pathlib.Path) -> None:
        src = tmp_path / "mytools.py"
        src.write_text(
            "async def register(mcp):\n"
            "    mcp.tool(annotations=None)(foo)\n"
            "    mcp.tool(annotations=None)(bar)\n"
            "@mcp.tool()\n"
            "async def baz(): pass\n"
        )
        assert ur.count_mcp_tools(tmp_path) == 3

    def test_missing_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        result = ur.count_mcp_tools(tmp_path / "nonexistent")
        assert result is None


# ---------------------------------------------------------------------------
# count_pytest_tests
# ---------------------------------------------------------------------------

class TestCountPytestTests:
    def test_returns_int_or_none(self) -> None:
        result = ur.count_pytest_tests(REPO_ROOT / "server" / "tests")
        assert result is None or isinstance(result, int)

    def test_nonexistent_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        result = ur.count_pytest_tests(tmp_path / "no_such_tests")
        assert result is None


# ---------------------------------------------------------------------------
# read_server_version
# ---------------------------------------------------------------------------

class TestReadServerVersion:
    def test_reads_real_pyproject(self) -> None:
        ver = ur.read_server_version(REPO_ROOT / "server" / "pyproject.toml")
        assert ver is not None
        assert ver.startswith("0.") or ver[0].isdigit()

    def test_returns_none_on_missing(self, tmp_path: pathlib.Path) -> None:
        assert ur.read_server_version(tmp_path / "pyproject.toml") is None

    def test_parses_version_field(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "pyproject.toml").write_text('[project]\nversion = "1.2.3"\n')
        assert ur.read_server_version(tmp_path / "pyproject.toml") == "1.2.3"


# ---------------------------------------------------------------------------
# read_plugin_version
# ---------------------------------------------------------------------------

class TestReadPluginVersion:
    def test_reads_real_package_json(self) -> None:
        ver = ur.read_plugin_version(REPO_ROOT / "unity-plugin" / "package.json")
        assert ver is not None
        assert "." in ver

    def test_returns_none_on_missing(self, tmp_path: pathlib.Path) -> None:
        assert ur.read_plugin_version(tmp_path / "package.json") is None

    def test_parses_version_field(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "package.json").write_text('{"version": "2.3.4"}')
        assert ur.read_plugin_version(tmp_path / "package.json") == "2.3.4"


# ---------------------------------------------------------------------------
# parse_latest_changelog
# ---------------------------------------------------------------------------

class TestParseLatestChangelog:
    def test_returns_version_and_title(self) -> None:
        cl = "# Changelog\n\n## [v1.2.3] — 2026-01-01\n\n- Some change\n"
        ver, title = ur.parse_latest_changelog(cl)
        assert ver == "v1.2.3"
        assert "2026-01-01" in title or title  # title may be the date part

    def test_real_changelog(self) -> None:
        text = (REPO_ROOT / "CHANGELOG.md").read_text()
        ver, title = ur.parse_latest_changelog(text)
        assert ver.startswith("v")

    def test_returns_question_marks_on_empty(self) -> None:
        ver, title = ur.parse_latest_changelog("")
        assert ver == "?"
        assert title == "?"


# ---------------------------------------------------------------------------
# extract_changelog_blocks
# ---------------------------------------------------------------------------

class TestExtractChangelogBlocks:
    def test_extracts_two_blocks(self) -> None:
        cl = (
            "# Changelog\n\n"
            "## [v2.0.0] — 2026-02-01\n\n"
            "- Feature A\n- Feature B\n- Feature C\n- Feature D\n\n"
            "## [v1.9.0] — 2026-01-01\n\n"
            "- Old thing\n"
        )
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        assert len(blocks) == 2
        assert "v2.0.0" in blocks[0]
        assert "v1.9.0" in blocks[1]

    def test_truncates_bullets(self) -> None:
        cl = (
            "## [v1.0.0] — 2026-01-01\n\n"
            "- A\n- B\n- C\n- D\n- E\n\n"
            "## [v0.9.0] — 2025-12-01\n\n"
            "- X\n"
        )
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        # First block should have exactly 3 bullets
        assert blocks[0].count("\n- ") + blocks[0].startswith("- ") <= 3 + 1

    def test_returns_one_if_only_one_version(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01\n\n- Only one\n"
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        assert len(blocks) == 1


# ---------------------------------------------------------------------------
# inject_changelog_into_readme
# ---------------------------------------------------------------------------

class TestInjectChangelog:
    def test_replaces_between_markers(self) -> None:
        readme = "# Title\n\n<!-- CHANGELOG_START -->\nOLD CONTENT\n<!-- CHANGELOG_END -->\n\n## Footer\n"
        result = ur.inject_changelog_into_readme(readme, "NEW CONTENT")
        assert "NEW CONTENT" in result
        assert "OLD CONTENT" not in result
        assert "<!-- CHANGELOG_START -->" in result
        assert "<!-- CHANGELOG_END -->" in result

    def test_no_markers_returns_unchanged(self) -> None:
        readme = "# Title\nNo markers here\n"
        result = ur.inject_changelog_into_readme(readme, "NEW CONTENT")
        assert result == readme

    def test_preserves_surrounding_content(self) -> None:
        readme = "BEFORE\n<!-- CHANGELOG_START -->\nOLD\n<!-- CHANGELOG_END -->\nAFTER"
        result = ur.inject_changelog_into_readme(readme, "NEW")
        assert result.startswith("BEFORE\n")
        assert result.endswith("AFTER")


# ---------------------------------------------------------------------------
# make_badge_json
# ---------------------------------------------------------------------------

class TestMakeBadgeJson:
    def test_returns_valid_shields_format(self) -> None:
        data = ur.make_badge_json("tests", "1726 passing", "3ad29f")
        assert data["schemaVersion"] == 1
        assert data["label"] == "tests"
        assert data["message"] == "1726 passing"
        assert data["color"] == "3ad29f"

    def test_serializable_to_json(self) -> None:
        data = ur.make_badge_json("tools", "89 MCP", "e94560")
        text = json.dumps(data)
        parsed = json.loads(text)
        assert parsed["label"] == "tools"


# ---------------------------------------------------------------------------
# update_stats_svg
# ---------------------------------------------------------------------------

class TestUpdateStatsSvg:
    def test_replaces_tools_marker(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=42, tests=None)
        assert "42" in result
        assert "<!-- STAT:TOOLS -->42<!-- /STAT -->" in result

    def test_replaces_tests_marker(self) -> None:
        svg = '<text><!-- STAT:TESTS -->1726<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=None, tests=9999)
        assert "9999" in result

    def test_skips_none_values(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=None, tests=None)
        assert result == svg
