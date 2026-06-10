"""Atomic patch version bump for Unity package.json.

Uses os.replace() for atomic write (cross-platform).
Bumps only the patch component: 0.20.3 → 0.20.4.
"""
import json
import os
from pathlib import Path


def bump_patch(package_json: Path) -> str:
    """Increment patch version in package.json atomically. Returns new version string."""
    data = json.loads(package_json.read_text(encoding="utf-8"))
    major, minor, patch = data["version"].split(".")
    data["version"] = f"{major}.{minor}.{int(patch) + 1}"

    tmp = package_json.with_suffix(".json.tmp")
    tmp.write_text(json.dumps(data, indent=2), encoding="utf-8")
    os.replace(tmp, package_json)  # atomic on POSIX; best-effort on Windows

    return data["version"]
