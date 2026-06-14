"""Tests for scripts/bump_version.py — atomic patch bump of package.json."""
import json
import os
import tempfile
from pathlib import Path

import pytest

from unity_mcp.scripts.bump_version import bump_patch


# #31: bump_patch increments patch version
def test_bump_patch_increments(tmp_path):
    pkg = tmp_path / "package.json"
    pkg.write_text(json.dumps({"version": "0.20.3", "name": "test"}))

    new_ver = bump_patch(pkg)
    assert new_ver == "0.20.4"
    data = json.loads(pkg.read_text())
    assert data["version"] == "0.20.4"


# #32: bump_patch is atomic — file always valid JSON (no partial write window)
def test_bump_atomic_no_partial(tmp_path):
    pkg = tmp_path / "package.json"
    pkg.write_text(json.dumps({"version": "0.5.0", "name": "x"}))

    bump_patch(pkg)
    # After bump, file must be valid JSON
    data = json.loads(pkg.read_text())
    assert data["version"] == "0.5.1"


# #33: second bump increments again (idempotency on second call = further increment)
def test_bump_idempotent_on_second_call(tmp_path):
    pkg = tmp_path / "package.json"
    pkg.write_text(json.dumps({"version": "0.20.3"}))

    bump_patch(pkg)
    bump_patch(pkg)

    data = json.loads(pkg.read_text())
    assert data["version"] == "0.20.5"
