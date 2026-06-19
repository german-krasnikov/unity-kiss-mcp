"""Canonical path helpers for ~/.unity-mcp directory layout."""
from pathlib import Path


def unity_mcp_dir() -> Path:
    return Path.home() / ".unity-mcp"


def ports_dir() -> Path:
    return unity_mcp_dir() / "ports"
