"""Tests for bootstrap install scripts (syntax + content validation)."""
import os
import subprocess
from pathlib import Path

import pytest

_REPO_ROOT = Path(__file__).parents[2]
SH = str(_REPO_ROOT / "install" / "bootstrap.sh")
PS1 = str(_REPO_ROOT / "install" / "bootstrap.ps1")


def _read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


# --- bootstrap.sh ---

def test_bootstrap_sh_syntax():
    result = subprocess.run(
        ["bash", "-n", SH],
        capture_output=True, text=True, encoding="utf-8"
    )
    assert result.returncode == 0, result.stderr


def test_bootstrap_sh_is_executable():
    assert os.access(SH, os.X_OK), f"{SH} must be executable"


def test_bootstrap_sh_has_error_handling():
    assert "set -euo pipefail" in _read(SH)


def test_bootstrap_sh_checks_uv():
    assert "command -v uv" in _read(SH)


def test_bootstrap_sh_installs_uv():
    assert "astral.sh/uv/install.sh" in _read(SH)


def test_bootstrap_sh_clones_repo():
    assert "git clone" in _read(SH)


def test_bootstrap_sh_runs_install_py():
    assert "install.py" in _read(SH)


def test_bootstrap_sh_supports_custom_dir():
    assert "UNITY_MCP_DIR" in _read(SH)


def test_bootstrap_sh_handles_existing_install():
    content = _read(SH)
    assert "git" in content and "pull" in content


def test_bootstrap_sh_macos_quarantine():
    assert "quarantine" in _read(SH)


def test_bootstrap_sh_no_unquoted_variables():
    """Key path variables must be quoted to handle spaces."""
    content = _read(SH)
    # INSTALL_DIR should always appear quoted
    assert '"$INSTALL_DIR"' in content, "INSTALL_DIR must be quoted"


# --- bootstrap.ps1 ---

def test_bootstrap_ps1_syntax():
    if subprocess.run(["which", "pwsh"], capture_output=True, encoding="utf-8").returncode != 0:
        pytest.skip("pwsh not installed")
    result = subprocess.run(
        ["pwsh", "-NoProfile", "-NonInteractive", "-Command",
         f"$null = [System.Management.Automation.Language.Parser]::ParseFile('{PS1}', [ref]$null, [ref]$null); exit 0"],
        capture_output=True, text=True, encoding="utf-8"
    )
    assert result.returncode == 0, result.stderr


def test_bootstrap_ps1_checks_uv():
    assert "Get-Command uv" in _read(PS1)


def test_bootstrap_ps1_installs_uv():
    assert "astral.sh/uv/install.ps1" in _read(PS1)


def test_bootstrap_ps1_clones_repo():
    assert "git clone" in _read(PS1)


def test_bootstrap_ps1_runs_install_py():
    assert "install.py" in _read(PS1)


def test_bootstrap_ps1_supports_custom_dir():
    assert "UNITY_MCP_DIR" in _read(PS1)


def test_bootstrap_ps1_long_paths():
    assert "core.longpaths" in _read(PS1)


def test_bootstrap_ps1_execution_policy_note():
    assert "ExecutionPolicy" in _read(PS1)


def test_bootstrap_ps1_quoted_install_dir():
    """$installDir must be quoted in git commands to handle paths with spaces."""
    content = _read(PS1)
    assert '"$installDir"' in content, "$installDir must be quoted"
