"""Discover MCP server command and Unity port."""
import pathlib
import shutil
import sys
from typing import Optional

from unity_mcp.paths import ports_dir as _ports_dir_canonical


def _which(name: str) -> Optional[str]:
    """Wrapper around shutil.which for monkeypatching in tests."""
    return shutil.which(name)


def _ports_dir() -> pathlib.Path:
    """Return ~/.unity-mcp/ports directory. Seam for testing."""
    return _ports_dir_canonical()


def find_server_dir() -> Optional[pathlib.Path]:
    """Attempt to find local server installation directory.

    Returns None for uvx-managed installs (path not reliably discoverable).
    """
    return None  # uvx manages this; not reliably discoverable


def find_python() -> str:
    """Return the python/uvx executable for running the MCP server."""
    if _which("uvx"):
        return "uvx"
    venv_python = pathlib.Path(sys.executable).parent / "unity-mcp"
    if venv_python.exists():
        return str(venv_python)
    return sys.executable


def find_server_command() -> list[str]:
    """Return best command to start MCP server. Priority: uvx > venv python > sys.executable."""
    exe = find_python()
    if exe == "uvx":
        return ["uvx", "unity-mcp"]
    if pathlib.Path(exe) != pathlib.Path(sys.executable):
        return [exe]
    return [exe, "-m", "unity_mcp.server"]


def find_port() -> int:
    """Discover Unity MCP port from ~/.unity-mcp/ports/*.port files. Default 9500."""
    for port_file in _ports_dir().glob("*.port"):
        try:
            return int(port_file.read_text(encoding="utf-8").split("\n")[0])
        except (ValueError, OSError):
            continue
    return 9500


def build_server_entry(port: int = 0) -> dict:
    """Build MCP server entry dict for config files."""
    cmd = find_server_command()
    entry: dict = {"command": cmd[0], "args": cmd[1:]}
    if port:
        entry["env"] = {"UNITY_MCP_PORT": str(port)}
    return entry
