"""TDD: sync_versions.py — updates pyproject.toml, package.json, __version__.py."""
import subprocess
import sys
import textwrap
from pathlib import Path

import pytest

SYNC_SCRIPT = Path(__file__).parents[2] / "scripts" / "sync_versions.py"


def run_sync(version: str, root: Path) -> subprocess.CompletedProcess:
    return subprocess.run(
        [sys.executable, str(SYNC_SCRIPT), version, "--root", str(root)],
        capture_output=True, text=True, encoding="utf-8",
    )


@pytest.fixture()
def project_root(tmp_path: Path) -> Path:
    """Minimal project tree mirroring real layout."""
    (tmp_path / "server" / "src" / "unity_mcp").mkdir(parents=True)
    (tmp_path / "unity-plugin").mkdir()

    (tmp_path / "server" / "pyproject.toml").write_text(textwrap.dedent("""\
        [project]
        name = "unity-mcp"
        version = "0.8.2"
        description = "MCP server"
    """), encoding="utf-8")

    (tmp_path / "unity-plugin" / "package.json").write_text(
        '{\n  "name": "com.unity-mcp.editor",\n  "version": "0.8.2"\n}\n',
        encoding="utf-8"
    )

    (tmp_path / "server" / "src" / "unity_mcp" / "__version__.py").write_text(
        '__version__ = "0.8.2"\n', encoding="utf-8"
    )

    return tmp_path


def test_sync_versions_updates_all_files(project_root: Path):
    result = run_sync("1.2.3", project_root)
    assert result.returncode == 0, result.stderr

    pyproject = (project_root / "server" / "pyproject.toml").read_text(encoding="utf-8")
    assert 'version = "1.2.3"' in pyproject

    pkg = (project_root / "unity-plugin" / "package.json").read_text(encoding="utf-8")
    assert '"version": "1.2.3"' in pkg

    ver_py = (project_root / "server" / "src" / "unity_mcp" / "__version__.py").read_text(encoding="utf-8")
    assert '__version__ = "1.2.3"' in ver_py


def test_sync_versions_preserves_other_content(project_root: Path):
    result = run_sync("2.0.0", project_root)
    assert result.returncode == 0, result.stderr

    pyproject = (project_root / "server" / "pyproject.toml").read_text(encoding="utf-8")
    assert 'name = "unity-mcp"' in pyproject
    assert 'description = "MCP server"' in pyproject


def test_sync_versions_invalid_version(project_root: Path):
    result = run_sync("not-semver", project_root)
    assert result.returncode != 0
    assert "semver" in result.stderr.lower() or "invalid" in result.stderr.lower()


def test_sync_versions_invalid_version_empty(project_root: Path):
    result = run_sync("", project_root)
    assert result.returncode != 0
