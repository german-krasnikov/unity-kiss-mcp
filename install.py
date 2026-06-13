#!/usr/bin/env python3
"""Unity MCP installer. stdlib only, Python 3.10+."""
import argparse
import json
import platform
import shutil
import socket
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).parent.resolve()
SERVER_DIR = REPO_ROOT / "server"
CODEX_DIR = REPO_ROOT / ".codex"
MCP_JSON = REPO_ROOT / ".mcp.json"
CODEX_CONFIG = CODEX_DIR / "config.toml"


def _check_python() -> bool:
    return sys.version_info >= (3, 10)


def _venv_python() -> Path:
    if platform.system() == "Windows":
        return SERVER_DIR / ".venv" / "Scripts" / "python.exe"
    return SERVER_DIR / ".venv" / "bin" / "python"


def _sync_uv() -> None:
    subprocess.run(["uv", "sync"], cwd=SERVER_DIR, check=True)


def _create_venv() -> None:
    venv_dir = SERVER_DIR / ".venv"
    subprocess.run([sys.executable, "-m", "venv", str(venv_dir)], check=True)
    pip = venv_dir / ("Scripts" if platform.system() == "Windows" else "bin") / "pip"
    subprocess.run([str(pip), "install", "-e", ".[dev]"], cwd=SERVER_DIR, check=True)


def _venv_stale() -> bool:
    """True if venv python doesn't point into SERVER_DIR (moved folder)."""
    py = _venv_python()
    if not py.exists():
        return False
    try:
        resolved = py.resolve()
        return SERVER_DIR not in resolved.parents
    except Exception:
        return True


def _write_codex_config() -> None:
    py = _venv_python()
    CODEX_DIR.mkdir(exist_ok=True)
    CODEX_CONFIG.write_text(
        "[mcp_servers.unity-mcp]\n"
        f'command = "{py}"\n'
        'args = ["-m", "unity_mcp.server"]\n'
        "startup_timeout_sec = 10\n"
        "tool_timeout_sec = 120\n"
        "enabled = true\n"
        "\n[mcp_servers.unity-mcp.env]\n"
        'PYTHONUTF8 = "1"\n',
        encoding="utf-8",
    )
    print(f"  Wrote {CODEX_CONFIG}")


def _setup_env(force_recreate: bool = False) -> None:
    if not _check_python():
        sys.exit(f"Python 3.10+ required, got {sys.version}")

    has_uv = shutil.which("uv") is not None

    if force_recreate and not has_uv:
        venv_dir = SERVER_DIR / ".venv"
        if venv_dir.exists():
            print("  Removing stale .venv …")
            shutil.rmtree(venv_dir)

    if has_uv:
        print("  uv found — running uv sync …")
        _sync_uv()
    else:
        print("  uv not found — using venv + pip …")
        _create_venv()

    _write_codex_config()


def cmd_setup(_args: argparse.Namespace) -> None:
    print("=== Unity MCP setup ===")
    _setup_env()
    print("Done. Run 'python install.py doctor' to verify.")


def cmd_update(_args: argparse.Namespace) -> None:
    print("=== Unity MCP update ===")
    stale = _venv_stale()
    if stale:
        print("  Stale venv detected (folder moved).")
    _setup_env(force_recreate=stale)
    print("Done.")


def cmd_doctor(_args: argparse.Namespace) -> None:
    def ok(label: str, result: bool, info: str = "") -> None:
        tag = "[OK]  " if result else "[FAIL]"
        suffix = f" ({info})" if info else ""
        print(f"  {tag} {label}{suffix}")

    print("=== Unity MCP doctor ===")
    ok("Python >= 3.10", _check_python(), sys.version.split()[0])
    ok("uv found", shutil.which("uv") is not None)

    py = _venv_python()
    ok(".venv/python exists", py.exists(), str(py))

    importable = False
    if py.exists():
        r = subprocess.run([str(py), "-c", "import unity_mcp"], capture_output=True)
        importable = r.returncode == 0
    ok("unity_mcp importable", importable)

    paths_ok = False
    if CODEX_CONFIG.exists():
        content = CODEX_CONFIG.read_text(encoding="utf-8")
        paths_ok = str(py) in content
    ok(".codex/config.toml paths correct", paths_ok)

    mcp_ok = False
    if MCP_JSON.exists():
        try:
            data = json.loads(MCP_JSON.read_text(encoding="utf-8"))
            mcp_ok = "unity" in data.get("mcpServers", {})
        except Exception:
            pass
    ok(".mcp.json configured", mcp_ok)

    # TCP :9500 probe
    try:
        with socket.create_connection(("127.0.0.1", 9500), timeout=1):
            print("  [INFO] TCP :9500 — OPEN (Unity plugin listening)")
    except OSError:
        print("  [INFO] TCP :9500 — closed (Unity not running or plugin disabled)")


def cmd_configure(args: argparse.Namespace) -> None:
    project = Path(args.project).resolve()
    if not project.is_dir():
        sys.exit(f"Directory not found: {project}")
    target = project / ".mcp.json"
    config = {
        "mcpServers": {
            "unity": {
                "command": "uv",
                "args": ["run", "--directory", str(SERVER_DIR), "unity-mcp"],
                "env": {"PYTHONUTF8": "1"},
            }
        }
    }
    target.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n",
                      encoding="utf-8")
    print(f"Wrote {target}")


def main() -> None:
    p = argparse.ArgumentParser(description="Unity MCP installer")
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("setup", help="First-time setup")
    sub.add_parser("update", help="Re-sync after folder move")
    sub.add_parser("doctor", help="Diagnose installation")
    cfg = sub.add_parser("configure", help="Write .mcp.json into a Unity project")
    cfg.add_argument("--project", required=True, help="Path to Unity project root")

    args = p.parse_args()
    dispatch = {"setup": cmd_setup, "update": cmd_update,
                "doctor": cmd_doctor, "configure": cmd_configure}
    dispatch[args.cmd](args)


if __name__ == "__main__":
    main()
