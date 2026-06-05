import asyncio
import os
import time
os.environ.setdefault("UNITY_MCP_DISTILL", "1")
from contextlib import asynccontextmanager
from mcp.server.fastmcp import FastMCP
from mcp.server.fastmcp.exceptions import ToolError
from .connection_slot import ConnectionSlot
from .lockfile import acquire_lock, release_lock
from .plugins import load_plugins
from .tools import register_all
from mcp.server.fastmcp import Context
from .tools.gating import discover_tools as _discover_tools_impl
from .tools.schema_registry import _registry as _schema_registry
from .server_filtering import (
    _SCHEMA_KEEP_FULL,
    _apply_gating,
    _strip_deferred_schemas,
    push_catalog as _push_catalog,
    filter_tools as _filter_tools_pure,
    install_list_tools_filter,
    read_unity_port as _read_unity_port,
)
from .server_lifespan import build_middleware, init_budget, wire_circuit_breaker

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

# Disabled-tools state lives here so tests can mutate srv._disabled_tools_cache directly.
_disabled_tools_cache: Optional[set] = None
_refresh_tools_lock: Optional[asyncio.Lock] = None


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
    return _filter_tools_pure(tools, _disabled_tools_cache)


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
_wrapped_send = None
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
    if _wrapped_send is not None:
        return await _wrapped_send(cmd, args, timeout=timeout)
    return await _send_raw(cmd, args, timeout)


def _args(**kwargs) -> dict:
    return {k: v for k, v in kwargs.items() if v is not None}


@asynccontextmanager
async def lifespan(app):
    global slot, manager, _middleware, _wrapped_send, _budget_tracker, _budget_router
    try:
        unity_port = _read_unity_port()
    except (ValueError, OSError):
        unity_port = 9500
    lock_fd = acquire_lock(port=unity_port)  # raises on failure — do not swallow
    try:
        slot = ConnectionSlot()
        manager = slot  # backward-compat alias
        _middleware = build_middleware(_send_raw)
        _budget_tracker, _budget_router = init_budget(_middleware)
        if _budget_tracker is not None:
            from .tools import budget_tool as _bt
            _bt._tracker = _budget_tracker
        global _wrapped_send
        if _middleware is not None:
            _wrapped_send = wrap_send(_send_raw, _middleware)
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
                wire_circuit_breaker(_middleware, active)
            active.start_heartbeat()
        yield
    finally:
        _wrapped_send = None
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


# Install filtering handler — captures schemas + applies gating + disabled-set.
install_list_tools_filter(mcp, lambda: slot, lambda: _disabled_tools_cache)


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
