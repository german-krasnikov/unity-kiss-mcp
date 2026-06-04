import asyncio
import json
import os
import time
os.environ.setdefault("UNITY_MCP_DISTILL", "1")
from contextlib import asynccontextmanager
from mcp.server.fastmcp import FastMCP
from mcp.server.fastmcp.exceptions import ToolError
import mcp.types as mcp_types
from .connection_slot import ConnectionSlot
from .lockfile import acquire_lock, release_lock
from .plugins import load_plugins
from .tools import register_all
from mcp.server.fastmcp import Context
from .tools.gating import filter_by_tier, discover_tools as _discover_tools_impl, TIER1, is_visible, FORCE_VISIBLE, get_catalog, _CORE_TOOLS
from .tools.schema_registry import _registry as _schema_registry, STUB_SCHEMA

# FORCE_VISIBLE ⊆ _CORE_TOOLS, so the union equals _CORE_TOOLS — precomputed once.
_SCHEMA_KEEP_FULL: frozenset[str] = _CORE_TOOLS

# Re-export tool functions for test imports
from .tools.scene import (
    compress_hierarchy, get_hierarchy, get_console, get_compile_errors, screenshot,
    recompile, run_tests, get_test_results, scene, search_scene, editor, checkpoint,
    fingerprint, get_changes, scene_diff, save_session, load_session,
    screenshot_baseline, screenshot_compare,
)
from .tools.objects import (
    get_component, inspect, get_components_list, find_objects,
    set_property, create_object, set_active, wire_event, unwire_event,
    delete_object, manage_component, get_object_detail, set_material,
)
from .tools.asset import (
    asset, project_settings, material, prefab, scriptable_object, get_enabled_tools,
)
from .tools.animation import animation, timeline, animator, particle
# Re-exported for tests that import these from `unity_mcp.server` (split from advanced.py: F19).
from .tools.batch import batch, references, validate_references
from .tools.codegen import execute_code, get_schema, auto_fix, smart_build
from .tools.skills import save_skill, use_skill, list_skills, apply_template, save_template, list_templates
from .tools.spatial import validate_layout, get_spatial_context, scan_scene, check_colliders, spatial_query
from .tools.ui import create_ui, set_rect, menu, shader
from .tools.connection import list_connections, reconnect_unity
from .tools.runtime import invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest
from .tools.autobatch import setup_objects, set_properties, configure_objects
from .tools.code_intel import find_references, compile_preflight, semantic_at
from .tools.animator_intent_tool import animator_intent
from .tools.vfx_intent_tool import vfx_intent
from .tools.ui_intent_tool import ui_intent
from .tools.metrics_tool import get_metrics
from .middleware import wrap_send, Middleware

from typing import Optional

_disabled_tools_cache: Optional[set] = None
_refresh_tools_lock: Optional[asyncio.Lock] = None

COMMAND_TIMEOUTS: dict[str, float] = {
    "run_tests": 120.0,
    "run_playtest": 120.0,
    "fuzz_playtest": 120.0,
    "compile_preflight": 60.0,
    "batch": 60.0,
}

slot: Optional[ConnectionSlot] = None
manager: Optional[ConnectionSlot] = None  # backward-compat alias for tests/conftest
_middleware: Optional[Middleware] = None
_budget_tracker = None
_budget_router = None


async def _send_raw(cmd: str, args: dict, timeout: float = 0) -> str:
    if slot is None:
        raise ToolError("Server not initialized. Restart MCP server (/mcp).")
    bridge = slot.bridge
    if bridge is None:
        raise ToolError("No Unity connection configured. Use reconnect_unity(port).")
    if timeout <= 0:
        timeout = COMMAND_TIMEOUTS.get(cmd, 30.0)
    probe = getattr(bridge, "_probe", None)
    try:
        result = await bridge.send(cmd, args, timeout=timeout)
    except asyncio.CancelledError:
        raise ToolError("Operation cancelled. Retry the command.")
    except (ConnectionError, TimeoutError, OSError) as e:
        ue = getattr(e, "unity_error", None)
        if ue is None:
            try:
                from .errors import classify_failure
                probe_busy = probe.has_strong_busy_signal() if probe else False
                rem = probe.estimated_remaining_s() if probe else 0.0
                ue = classify_failure(e, probe_busy, rem)
            except Exception:
                ue = None
        if ue is not None:
            raise ToolError(
                f"[UNITY_UNAVAILABLE] state={ue.unity_state} transient={ue.is_transient} "
                f"retry_after={ue.retry_after_seconds}s | {ue.message}"
            ) from e
        raise ToolError(f"Unity connection lost: {e}. Retry or /mcp to reconnect.") from e
    except Exception as e:
        raise ToolError(f"Unexpected error: {type(e).__name__}: {e}") from e
    if not result["ok"]:
        raise ToolError(result["err"])
    data = result.get("data", "")
    if "file" in result:
        file_msg = f"Data saved to: {result['file']}"
        return f"{data}\n{file_msg}" if data else file_msg
    return data


async def _send(cmd: str, args: dict, timeout: float = 30.0) -> str:
    if _middleware is not None:
        return await wrap_send(_send_raw, _middleware)(cmd, args, timeout=timeout)
    return await _send_raw(cmd, args, timeout)



def _args(**kwargs) -> dict:
    return {k: v for k, v in kwargs.items() if v is not None}


def _read_unity_port() -> int:
    """Discover Unity MCP port from discovery files, env var, or default 9500."""
    if os.environ.get("UNITY_MCP_PORT"):
        return int(os.environ["UNITY_MCP_PORT"])
    from pathlib import Path
    ports_dir = Path.home() / ".unity-mcp" / "ports"
    if ports_dir.exists():
        candidates = []
        for f in ports_dir.glob("*.port"):
            try:
                lines = f.read_text().strip().split("\n")
                port = int(lines[0])
                pid = int(f.stem)
                os.kill(pid, 0)
                project = lines[2] if len(lines) > 2 else "?"
                candidates.append((f.stat().st_mtime, port, project, pid))
            except (ValueError, ProcessLookupError, PermissionError, OSError):
                try:
                    f.unlink()
                except OSError:
                    pass
        if candidates:
            candidates.sort(reverse=True)
            _, port, project, pid = candidates[0]
            import logging
            logging.getLogger("unity_mcp").info(
                "Auto-discovered Unity '%s' on port %d (pid %d)", project, port, pid)
            return port
    return 9500


@asynccontextmanager
async def lifespan(app):
    global slot, manager, _middleware, _budget_tracker, _budget_router
    try:
        unity_port = _read_unity_port()
    except (ValueError, OSError):
        unity_port = 9500
    lock_fd = acquire_lock(port=unity_port)  # raises on failure — do not swallow
    try:
        slot = ConnectionSlot()
        manager = slot  # backward-compat alias
        if os.environ.get("UNITY_MCP_MIDDLEWARE"):
            _middleware = Middleware()
            from .sampling import SamplingService
            _middleware.sampling = SamplingService()
        # ToolHinter: always wired when middleware is active (or standalone)
        if os.environ.get("UNITY_MCP_HINTS", "1") != "0":
            if _middleware is None:
                _middleware = Middleware()
            from .hinter import ToolHinter
            _middleware.hinter = ToolHinter(enabled=True)
        if os.environ.get("UNITY_MCP_SCENE_BRIEF"):
            if _middleware is None:
                _middleware = Middleware()
            from .scene_brief import SceneBrief
            _middleware.scene_brief = SceneBrief()
        if os.environ.get("UNITY_MCP_SPECULATION"):
            from .speculation import SpeculativeLayer
            if _middleware is None:
                _middleware = Middleware()
            _middleware.speculation = SpeculativeLayer(_send_raw)
        if os.environ.get("UNITY_MCP_LESSONS"):
            from .lessons import LessonStore, LessonRecorder
            from pathlib import Path
            if _middleware is None:
                _middleware = Middleware()
            store = LessonStore(Path.home() / ".unity-mcp" / "lessons.json")
            _middleware.lessons = store
            _middleware.recorder = LessonRecorder(store)
        if os.environ.get("UNITY_MCP_WATCHDOG"):
            from .watchdog import ProactiveWatchdog
            if _middleware is None:
                _middleware = Middleware()
            _middleware.watchdog = ProactiveWatchdog(_send_raw)
        if os.environ.get("UNITY_MCP_INFERENCE"):
            from .inference import SessionContext, Inferrer
            if _middleware is None:
                _middleware = Middleware()
            _middleware.session = SessionContext()
            _middleware.inferrer = Inferrer()
        if os.environ.get("UNITY_MCP_BUDGET", "1") != "0":
            from .budget import CostTracker, BudgetRouter
            from .sampling import init_budget
            session_cap = float(os.environ.get("UNITY_MCP_HAIKU_BUDGET", "0.50"))
            day_cap = float(os.environ.get("UNITY_MCP_HAIKU_DAY_CAP", "5.00"))
            _budget_tracker = CostTracker(session_cap=session_cap, day_cap=day_cap)

            def _hit_rate(feature: str):
                if feature == "speculation" and _middleware and _middleware.speculation:
                    spec = _middleware.speculation
                    total = spec._hits + spec._misses
                    return spec._hits / total if total > 0 else None
                return None

            _budget_router = BudgetRouter(_budget_tracker, _hit_rate)
            init_budget(_budget_tracker, _budget_router)
            from .tools import budget_tool as _bt
            _bt._tracker = _budget_tracker
        await slot.connect(unity_port)
        active = slot.bridge
        if active is not None:
            if active.connected:
                await _refresh_tools_cache(active)
                await _push_catalog(active)
            _last_refresh_ts: float = 0.0

            def _on_reconnect():
                nonlocal _last_refresh_ts
                now = time.monotonic()
                if now - _last_refresh_ts < 5.0:
                    return
                _last_refresh_ts = now
                asyncio.ensure_future(_refresh_tools_cache(slot.bridge))
                asyncio.ensure_future(_push_catalog(slot.bridge))
            slot.add_reconnect_callback(_on_reconnect)
            if _middleware is not None:
                slot.add_reconnect_callback(_middleware.reset_session)
                # F05: wire circuit breaker readiness to compile state probe
                probe = getattr(active, "_probe", None)
                if probe is not None:
                    ready_fn = lambda: not probe.has_strong_busy_signal()
                    _middleware._circuit_ready_fn = ready_fn
                    _middleware.circuit._is_ready_fn = ready_fn
            active.start_heartbeat()
        yield
    finally:
        if slot and slot.bridge:
            slot.bridge.stop_heartbeat()
        if _middleware and _middleware.watchdog:
            try:
                await _middleware.watchdog.cancel()
            except Exception:
                pass
        if slot:
            await slot.close()
        release_lock(lock_fd)


mcp = FastMCP("UnityMCP", lifespan=lifespan)

register_all(mcp, _send, _args, get_slot=lambda: slot,
             get_middleware=lambda: _middleware)
load_plugins(mcp, _send, _args)
@mcp.tool()
async def discover_tools(category: str | None = None, enable: bool = True, ctx: Context = None) -> str:
    """Find and enable tools by category.
    Categories: object, animation, asset, advanced, ui, runtime, connection, session.
    Pass enable=False to browse without enabling."""
    result = await _discover_tools_impl(category, enable)
    if enable and category and ctx:
        await ctx.session.send_tool_list_changed()
    return result

from .resources import register as register_resources
register_resources(mcp, _send, _args)


@mcp.tool()
async def resolve_tool_schema(tools: str) -> str:
    """Return full parameter schemas for deferred tools. tools=comma-separated names."""
    names = [n.strip() for n in tools.split(",") if n.strip()]
    text = _schema_registry.format_text(names)
    if not text:
        unknown = ", ".join(names)
        return f"No schema found for: {unknown}"
    return text


# --- Dynamic tool filtering based on Unity MCPSettings ---

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


async def _push_catalog(bridge_) -> None:
    """Push the Python-authoritative tool catalog to Unity on connect/reconnect.

    Silent on failure — Unity can still operate with stale/no catalog.
    """
    try:
        if bridge_ is None or not bridge_.connected:
            return
        catalog_str = json.dumps(get_catalog())
        await bridge_.send("set_tool_catalog", {"catalog": catalog_str}, timeout=5.0)
    except Exception:
        pass


async def _refresh_tools_cache(bridge_) -> None:
    """Fetch disabled tools from Unity and populate cache. Called on connect/reconnect.

    Idempotent: if already refreshing, skip. Failures are silent — stale cache
    is acceptable until next successful reconnect.
    """
    global _disabled_tools_cache, _refresh_tools_lock
    if _refresh_tools_lock is None:
        _refresh_tools_lock = asyncio.Lock()
    if _refresh_tools_lock.locked():
        return  # another refresh in flight — skip
    if bridge_ is None or not bridge_.connected:
        return
    async with _refresh_tools_lock:
        try:
            result = await bridge_.send("get_disabled_tools", {}, timeout=5.0)
            if result.get("ok"):
                data = result.get("data", "").strip()
                _disabled_tools_cache = set(data.split(",")) if data else set()
        except Exception:
            pass


async def _filter_tools(tools: list, bridge_) -> list:
    """Filter tools by gating then subtract disabled set (from Unity MCPSettings).
    Cache is None → gating-only fallback (no TCP call)."""
    result = _apply_gating(tools)
    disabled = _disabled_tools_cache
    if disabled:
        result = [t for t in result if t.name not in disabled or t.name in FORCE_VISIBLE]
    return _strip_deferred_schemas(result)


_original_handler = mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest]


async def _filtered_tools_handler(req):
    result = await _original_handler(req)
    # Capture full schemas into registry BEFORE stripping
    for t in result.root.tools:
        schema = getattr(t, "inputSchema", None) or {}
        desc = getattr(t, "description", "") or ""
        ann = getattr(t, "annotations", None)
        _schema_registry.capture(t.name, schema, desc, annotations=ann)
    result.root.tools = await _filter_tools(result.root.tools, slot.bridge if slot else None)
    return result


mcp._mcp_server.request_handlers[mcp_types.ListToolsRequest] = _filtered_tools_handler


def main():
    import signal
    try:
        signal.signal(signal.SIGPIPE, signal.SIG_IGN)
    except (OSError, ValueError):
        pass
    transport = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
    try:
        if transport == "http":
            port = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8765"))
            mcp.run(transport="streamable-http", host="127.0.0.1", port=port)
        else:
            mcp.run(transport="stdio")
    except BrokenPipeError:
        pass
    except OSError as e:
        import errno
        if e.errno != errno.EPIPE:
            raise


if __name__ == "__main__":
    main()
