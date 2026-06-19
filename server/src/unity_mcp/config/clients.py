"""Client registry: known MCP-compatible AI tools and their config paths."""
import os
import pathlib
import sys
from dataclasses import dataclass


@dataclass
class ClientInfo:
    name: str
    config_path: pathlib.Path
    scope: str  # "global" or "project"
    stdout_only: bool = False  # if True: print config to stdout instead of writing file


def _claude_desktop_path() -> pathlib.Path:
    if sys.platform == "darwin":
        return pathlib.Path.home() / "Library" / "Application Support" / "Claude" / "claude_desktop_config.json"
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Claude" / "claude_desktop_config.json"
    return pathlib.Path.home() / ".config" / "Claude" / "claude_desktop_config.json"


def _windsurf_path() -> pathlib.Path:
    if sys.platform == "win32":
        appdata = os.environ.get("APPDATA", pathlib.Path.home() / "AppData" / "Roaming")
        return pathlib.Path(appdata) / "Codeium" / "windsurf" / "mcp_config.json"
    return pathlib.Path.home() / ".codeium" / "windsurf" / "mcp_config.json"


CLIENT_REGISTRY: dict[str, ClientInfo] = {
    "claude-desktop": ClientInfo(
        name="Claude Desktop",
        config_path=_claude_desktop_path(),
        scope="global",
    ),
    "claude-code": ClientInfo(
        name="Claude Code",
        config_path=pathlib.Path.home() / ".claude.json",
        scope="global",
    ),
    "cursor": ClientInfo(
        name="Cursor",
        config_path=pathlib.Path.home() / ".cursor" / "mcp.json",
        scope="global",
    ),
    "windsurf": ClientInfo(
        name="Windsurf",
        config_path=_windsurf_path(),
        scope="global",
    ),
    "generic": ClientInfo(
        name="Generic (stdout)",
        config_path=pathlib.Path("/dev/null"),
        scope="global",
        stdout_only=True,
    ),
}


def detect_installed() -> list[str]:
    """Return keys of clients whose config file or parent dir exists. Skips stdout_only."""
    found = []
    for key, info in CLIENT_REGISTRY.items():
        if info.stdout_only:
            continue
        if info.config_path.exists() or info.config_path.parent.exists():
            found.append(key)
    return found
