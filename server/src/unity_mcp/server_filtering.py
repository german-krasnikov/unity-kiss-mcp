"""Tool filtering pipeline: gating, schema stripping, port discovery.

Pure, stateless helpers. State (_disabled_tools_cache, _refresh_tools_lock)
lives in server.py so tests can mutate srv._disabled_tools_cache directly.
"""
import logging
import os
from pathlib import Path

from .tools.gating import filter_by_tier, FORCE_VISIBLE, get_catalog, _CORE_TOOLS
from .tools.schema_registry import _registry as _schema_registry

# Core tools keep full schemas; all others get stub schema on ListTools.
_SCHEMA_KEEP_FULL: frozenset[str] = _CORE_TOOLS


def _apply_gating(tools: list) -> list:
    if os.environ.get("UNITY_MCP_NO_GATING"):
        return tools
    return filter_by_tier(tools)


def _strip_deferred_schemas(tools: list) -> list:
    """Replace inputSchema of non-core tools with STUB unless UNITY_MCP_FULL_SCHEMAS=1.

    Safe: these are ListTools *response* objects, separate from mcp._tool_manager._tools
    which holds the callable fn. FastMCP dispatches via _tool_manager (validate_input=False
    path), so stripping inputSchema here cannot block tool execution.
    """
    if os.environ.get("UNITY_MCP_FULL_SCHEMAS", "0") == "1":
        return tools
    for t in tools:
        if t.name not in _SCHEMA_KEEP_FULL:
            # Fresh dict per tool — avoids shared-singleton mutation bugs
            t.inputSchema = {"type": "object"}
    return tools


async def push_catalog(bridge_) -> None:
    """Push the Python-authoritative tool catalog to Unity on connect/reconnect.

    Silent on failure — Unity can still operate with stale/no catalog.
    """
    try:
        if bridge_ is None or not bridge_.connected:
            return
        categories = get_catalog()["categories"]
        catalog_str = "\n".join(
            f"{cat}:{','.join(tools)}"
            for cat, tools in categories.items()
        )
        await bridge_.send("set_tool_catalog", {"catalog": catalog_str}, timeout=5.0)
    except Exception:
        pass


def filter_tools(tools: list, disabled: set | None) -> list:
    """Pure filter: gating → disabled-set subtraction → schema strip.
    disabled=None → gating-only fallback.
    """
    result = _apply_gating(tools)
    if disabled:
        result = [t for t in result if t.name not in disabled or t.name in FORCE_VISIBLE]
    return _strip_deferred_schemas(result)


def read_unity_port() -> int:
    """Discover Unity MCP port from discovery files, env var, or default 9500.

    Priority: env var → CWD project match → newest mtime → 9500.
    """
    if os.environ.get("UNITY_MCP_PORT"):
        try:
            return int(os.environ["UNITY_MCP_PORT"])
        except ValueError:
            pass
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if not ports_dir.exists():
        return 9500

    candidates = []
    for f in ports_dir.glob("*.port"):
        try:
            lines = f.read_text().strip().split("\n")
            port = int(lines[0])
            pid = int(f.stem)
            os.kill(pid, 0)
            project_path = lines[1] if len(lines) > 1 else ""
            project = lines[2] if len(lines) > 2 else "?"
            candidates.append((f.stat().st_mtime, port, project, pid, project_path))
        except PermissionError:
            # Process alive but owned by another user — keep as candidate
            try:
                lines = f.read_text().strip().split("\n")
                port = int(lines[0])
                project_path = lines[1] if len(lines) > 1 else ""
                project = lines[2] if len(lines) > 2 else "?"
                candidates.append((f.stat().st_mtime, port, project, int(f.stem), project_path))
            except (ValueError, OSError):
                pass
        except (ValueError, ProcessLookupError, OSError):
            try:
                f.unlink()
            except OSError:
                pass

    if not candidates:
        return 9500

    # CWD-based selection: prefer project whose path is a prefix of cwd.
    # If multiple match (nested), prefer longest path.
    cwd = os.getcwd()
    cwd_matches = [
        (len(pp), mtime, port, proj, pid)
        for mtime, port, proj, pid, pp in candidates
        if pp and (cwd == pp or cwd.startswith(pp + os.sep))
    ]
    if cwd_matches:
        cwd_matches.sort(reverse=True)  # longest path first, then newest mtime
        _, _, port, project, pid = cwd_matches[0]
    else:
        candidates.sort(reverse=True)  # newest mtime first
        _, port, project, pid, _ = candidates[0]

    logging.getLogger("unity_mcp").info(
        "Auto-discovered Unity '%s' on port %d (pid %d)", project, port, pid)
    return port


def install_list_tools_filter(mcp_server, get_disabled_cache_fn):
    """Patch mcp._mcp_server.request_handlers to inject filtering + schema capture."""
    import mcp.types as mcp_types

    original_handler = mcp_server._mcp_server.request_handlers[mcp_types.ListToolsRequest]

    async def _filtered_tools_handler(req):
        result = await original_handler(req)
        # Capture full schemas into registry BEFORE stripping
        for t in result.root.tools:
            schema = getattr(t, "inputSchema", None) or {}
            desc = getattr(t, "description", "") or ""
            ann = getattr(t, "annotations", None)
            _schema_registry.capture(t.name, schema, desc, annotations=ann)
        result.root.tools = filter_tools(result.root.tools, get_disabled_cache_fn())
        return result

    mcp_server._mcp_server.request_handlers[mcp_types.ListToolsRequest] = _filtered_tools_handler
