#!/usr/bin/env python3
"""Unity MCP installer. stdlib only, Python 3.10+."""
import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).parent.resolve()
SERVER_DIR = REPO_ROOT / "server"
CODEX_DIR = REPO_ROOT / ".codex"
MCP_JSON = REPO_ROOT / ".mcp.json"
CODEX_CONFIG = CODEX_DIR / "config.toml"
_UNITY_MCP_DATA_DIR = Path.home() / ".unity-mcp"

# ── package imports ───────────────────────────────────────────────────────────
sys.path.insert(0, str(REPO_ROOT))
from install import ui  # noqa: E402
from install.ui import prompt_yn  # noqa: E402
import install.commands as _cmds  # noqa: E402

# ── lazy server-package imports (patchable at module level) ───────────────────

def _add_server_to_path() -> None:
    src = str(SERVER_DIR / "src")
    if src not in sys.path:
        sys.path.insert(0, src)


try:
    _add_server_to_path()
    from unity_mcp.config.clients import CLIENT_REGISTRY, detect_installed
    from unity_mcp.config.merger import merge_mcp_config, merge_toml_mcp
    from unity_mcp.config.backup import backup
    from unity_mcp.config.resolver import build_server_entry
except ImportError:
    CLIENT_REGISTRY = {}  # type: ignore[assignment]
    detect_installed = lambda: []  # type: ignore[assignment]
    merge_mcp_config = None  # type: ignore[assignment]
    merge_toml_mcp = None  # type: ignore[assignment]
    backup = None  # type: ignore[assignment]
    build_server_entry = lambda port=0: {}  # type: ignore[assignment]


# ── thin wrappers (read module globals at call time so tests can patch) ───────

def _venv_python() -> Path:
    return _cmds.venv_python(SERVER_DIR)


def _venv_stale() -> bool:
    return _cmds.venv_stale(SERVER_DIR)


def _setup_env(force_recreate: bool = False) -> None:
    _cmds.setup_env(SERVER_DIR, CODEX_DIR, CODEX_CONFIG, ui, force_recreate=force_recreate)


# ── subcommands ───────────────────────────────────────────────────────────────

def cmd_setup(_args: argparse.Namespace) -> None:
    ui.box(["Unity MCP setup"])
    _setup_env()
    ui.ok("Done. Run 'python install.py doctor' to verify.")


def cmd_update(_args: argparse.Namespace) -> None:
    ui.box(["Unity MCP update"])
    stale = _venv_stale()
    if stale:
        ui.info("Stale venv detected (folder moved).")
    _setup_env(force_recreate=stale)
    ui.ok("Done.")


def cmd_doctor(_args: argparse.Namespace) -> None:
    _cmds.cmd_doctor(SERVER_DIR, CODEX_CONFIG, MCP_JSON, ui, _args)


def cmd_configure(args: argparse.Namespace) -> None:
    """Configure AI tools (--tool) or write project-scoped config (--project-dir)."""
    if merge_mcp_config is None:
        ui.fail("unity_mcp not installed. Run: python install.py setup")
        sys.exit(1)

    entry = build_server_entry(port=getattr(args, "port", 0))
    tool_key = getattr(args, "tool", None)

    project_dir = getattr(args, "project_dir", None) or getattr(args, "project", None)
    if project_dir:
        project = Path(project_dir).resolve()
        if not project.is_dir():
            sys.exit(f"Directory not found: {project}")
        target = _project_config_path(project, tool_key or "claude-code")
        target.parent.mkdir(parents=True, exist_ok=True)
        backup(target)
        client = CLIENT_REGISTRY.get(tool_key or "claude-code", CLIENT_REGISTRY["claude-code"])
        if client.is_toml:
            merge_toml_mcp(target, entry)
        else:
            merge_mcp_config(target, entry, root_key=client.root_key, entry_transformer=client.entry_transformer)
        ui.ok(f"{client.name} configured at {target}")
        return

    if tool_key:
        tools = [tool_key]
    else:
        installed = detect_installed()
        ui.info(f"Detected: {', '.join(installed) or 'none'}")
        tools = [t for t in installed if prompt_yn(f"Configure {t}?")]

    for tool in tools:
        client = CLIENT_REGISTRY[tool]
        if client.stdout_only:
            print(json.dumps({"mcpServers": {"unity-mcp": entry}}, indent=2, ensure_ascii=False))
            continue
        backup(client.config_path)
        if client.is_toml:
            merge_toml_mcp(client.config_path, entry)
        else:
            merge_mcp_config(
                client.config_path, entry,
                root_key=client.root_key,
                entry_transformer=client.entry_transformer,
            )
        ui.ok(f"{client.name} configured at {client.config_path}")


def _project_config_path(project: Path, tool_key: str) -> Path:
    """Return per-tool project-scoped config path."""
    paths = {
        "claude-code": project / ".mcp.json",
        "cursor": project / ".cursor" / "mcp.json",
        "vscode": project / ".vscode" / "mcp.json",
    }
    return paths.get(tool_key, project / ".mcp.json")


def cmd_pull(_args: argparse.Namespace) -> None:
    ui.box(["Unity MCP — pull latest"])
    code = _cmds.cmd_pull(REPO_ROOT, ui)
    if code != 0:
        sys.exit(code)


def cmd_uninstall(_args: argparse.Namespace) -> None:
    _cmds.cmd_uninstall(SERVER_DIR, _UNITY_MCP_DATA_DIR, ui, prompt_yn, _args)


def cmd_connect(args: argparse.Namespace) -> None:
    sys.exit(_cmds.cmd_connect(args, ui))


def cmd_disconnect(args: argparse.Namespace) -> None:
    sys.exit(_cmds.cmd_disconnect(args, ui))


# ── argparse ──────────────────────────────────────────────────────────────────

def main() -> None:
    p = argparse.ArgumentParser(description="Unity MCP installer")
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("setup", help="First-time setup")
    sub.add_parser("update", help="Re-sync after folder move")
    sub.add_parser("doctor", help="Diagnose installation")

    cfg = sub.add_parser("configure", help="Configure AI tools or write .mcp.json")
    cfg.add_argument("--project", help="Path to Unity project root (legacy alias for --project-dir)")
    cfg.add_argument("--project-dir", help="Unity project root — writes project-scope config")
    cfg.add_argument("--tool", choices=list(CLIENT_REGISTRY) or
                     ["claude-desktop", "claude-code", "cursor", "windsurf", "generic"])
    cfg.add_argument("--port", type=int, default=0)

    sub.add_parser("pull", help="Pull latest code (git clone installs only)")
    sub.add_parser("uninstall", help="Remove Unity MCP")

    p_connect = sub.add_parser("connect", help="Connect Unity MCP to a Unity project")
    p_connect.add_argument("unity_project", help="Path to Unity project root")

    p_disconnect = sub.add_parser("disconnect", help="Disconnect Unity MCP from a Unity project")
    p_disconnect.add_argument("unity_project", help="Path to Unity project root")

    args = p.parse_args()
    dispatch = {
        "setup": cmd_setup, "update": cmd_update, "doctor": cmd_doctor,
        "configure": cmd_configure, "pull": cmd_pull, "uninstall": cmd_uninstall,
        "connect": cmd_connect, "disconnect": cmd_disconnect,
    }
    dispatch[args.cmd](args)


if __name__ == "__main__":
    main()
