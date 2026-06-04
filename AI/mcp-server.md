# Feature: MCP Server

## Overview

Python MCP сервер с 89 core registered tools для управления Unity Editor. FastMCP + ConnectionSlot + capability gating + 23 middleware layers. External plugins can add more tools dynamically.

## Architecture (для Architect)

```
server/src/unity_mcp/
├── server.py           # FastMCP instance, lifespan, tool registration
├── bridge.py           # UnityBridge (TCP, heartbeat, keepalive)
├── connection_slot.py  # ConnectionSlot (single connection)
├── lockfile.py         # Exclusive fcntl.flock per port, stale server cleanup
├── compile_state.py    # CompileStateProbe (heuristic Unity compile detection)
├── middleware.py        # 23 middleware layers (env-gated)
├── plugin_api.py       # Stable public API for external plugins
├── resources.py        # MCP Resources (4 URIs: hierarchy, console, editor, categories)
├── tools/
│   ├── __init__.py     # Tool module registry
│   ├── objects.py      # get_component/inspect/find/set_property/create/delete/manage_component/set_active/wire_event/unwire_event/set_material/set_parent/set_property_delta
│   ├── scene.py        # hierarchy, console, compile_errors, screenshot, recompile, run_tests, get_test_results, scene, search_scene, editor, checkpoint, fingerprint, scene_diff, save/load_session, screenshot_baseline/compare, get_changes
│   ├── code_intel.py   # find_references, compile_preflight, semantic_at, await_compile
│   ├── runtime.py      # invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest
│   ├── batch.py        # batch, references, validate_references + DRY serialization
│   ├── spatial.py      # spatial_query, validate_layout, get_spatial_context, scan_scene, check_colliders
│   ├── ui.py           # create_ui, set_rect, menu, shader
│   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   ├── skills.py       # save_skill, use_skill, list_skills, apply_template, save_template, list_templates
│   ├── animation.py    # animation, timeline, animator, particle
│   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings, get_enabled_tools
│   ├── connection.py   # list_connections, reconnect_unity
│   ├── autobatch.py    # setup_objects, set_properties, configure_objects
│   ├── gating.py       # Capability gating: TIER1 + category-based filtering
│   ├── do_tool.py      # NL intent → Haiku plan → batch execute
│   ├── ask_tool.py     # NL read-only question → route → Haiku summarize
│   ├── animator_intent_tool.py  # Domain-specific animator NL
│   ├── vfx_intent_tool.py       # Domain-specific VFX NL
│   ├── ui_intent_tool.py        # Domain-specific UI NL
│   ├── intent_common.py         # Shared intent infrastructure
│   ├── budget_tool.py           # budget_status tool (Haiku spend tracking)
│   ├── metrics_tool.py          # Performance metrics
│   └── schema_registry.py        # Tool schema lazy-loading
└── plugins/
    └── __init__.py              # 3-source auto-discovery (pkgutil, entry_points, UNITY_MCP_PLUGIN_DIRS)
```

### Tools (90 core)

**TIER1 — always visible (39 core):**

Core (24): get_hierarchy, get_component, inspect, set_property, create_object, delete_object, manage_component, set_parent, batch, get_console, get_compile_errors, screenshot, scene, editor, search_scene, run_tests, discover_tools, get_enabled_tools, setup_objects, set_properties, configure_objects, do, ask, get_metrics

Intent (3): animator_intent, vfx_intent, ui_intent

Code Intel (4): find_references, compile_preflight, semantic_at, await_compile

Runtime (8): invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest

### Compile-Tool Corroboration (v0.7.0+)

`get_compile_errors`, `await_compile`, `auto_fix`, and `ask` now cross-verify clean responses via `editor_log.py`: an out-of-band reader of Unity's `Editor.log` that catches cases where the in-plugin C# reporter is itself broken (stale bytecode, unsafe to trust). Only overrides when both signals agree: log shows errors AND dll is stale. Zero false positives (fresh dll trusted). Resolves P0 silent-blindness bug where plugin compile failures masked themselves.

**Ungated (always visible, not in TIER1 or categories — pass through as "unknown"):**

get_test_results, budget_status

**Category-gated (enabled via `discover_tools`):**

| Category | Tools |
|----------|-------|
| object | find_objects, get_object_detail, get_components_list, set_active, set_material, wire_event, unwire_event, set_property_delta |
| animation | animation, timeline, animator, particle |
| asset | asset, material, prefab, scriptable_object, project_settings |
| advanced | shader, references, validate_references, menu, checkpoint, recompile, execute_code, check_colliders, get_schema, scan_scene, spatial_query, auto_fix, smart_build, apply_template, save_template, list_templates, await_compile |
| ui | create_ui, set_rect, validate_layout, get_spatial_context |
| runtime | invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest |
| connection | list_connections, reconnect_unity |
| session | fingerprint, scene_diff, get_changes, save_session, load_session, screenshot_baseline, screenshot_compare, save_skill, use_skill, list_skills |

### Capability Gating (gating.py)

- TIER1 tools always visible to LLM
- Categories enabled per-session via `discover_tools(category, enable=True)`
- Double-filtered: Python gating × Unity-side MCPSettings (tool cache from `get_enabled_tools`)
- Unknown (plugin) tools pass through by default
- Plugin self-registration: `gating.register_tools("category", tools_set, tier1=subset)` lets plugins add to CATEGORIES + TIER1

### Server Startup

```python
# server.py
mcp = FastMCP("UnityMCP", lifespan=lifespan)
register_all(mcp, _send, _args, get_slot=lambda: slot, get_middleware=lambda: _middleware)
load_plugins(mcp, _send, _args)

@asynccontextmanager
async def lifespan(app):
    # 1. Auto-discover Unity port from ~/.unity-mcp/ports/*.port or UNITY_MCP_PORT env
    # 2. Acquire exclusive PID lockfile ~/.unity-mcp/server-{port}.lock
    # 3. Create ConnectionSlot, connect bridge
    # 4. Wire middleware layers (if UNITY_MCP_MIDDLEWARE=1)
    # 5. Wire ToolHinter (default on, disable with UNITY_MCP_HINTS=0)
    # 6. Wire budget tracking (default on, disable with UNITY_MCP_BUDGET=0)
    # 7. Wire optional layers: SceneBrief, SpeculativeLayer, Lessons, Watchdog, Inference
    # 8. Fetch enabled tools cache, start heartbeat, register reconnect callbacks
    yield
    # Shutdown: stop heartbeat, cancel watchdog, close bridge, release lock

def main():
    transport = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
    if transport == "http":
        port = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8765"))
        mcp.run(transport="streamable-http", host="127.0.0.1", port=port)
    else:
        mcp.run(transport="stdio")
```

### Bridge / Connection

- **ConnectionSlot**: single `UnityBridge` connection
  - `connect(port)`, `reconnect()`, `bridge` property
- **UnityBridge**: single TCP connection
  - Protocol: JSON over TCP, 4-byte big-endian length prefix
  - Socket: `TCP_NODELAY`, `SO_KEEPALIVE` (macOS: idle=60s, interval=10s, count=3)
  - Heartbeat: 15s interval, raw ping, 3 failures → close, 2s polling when disconnected (5s if compile busy)
  - Reconnect cooldown: MIN_RECONNECT_INTERVAL=2.0s, ping verification, fires callbacks
  - DomainReloadError on Unity `going_away` event frame

### Compile State Probe (compile_state.py)

Simplified detector for Unity C# compile/domain-reload:
- **State file**: reads `~/.unity-mcp/state/port-{port}.state` (ready/compiling/reloading)
- `is_process_dead()`: PID cross-check from port file

### Auto-Batch (autobatch.py)

- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value)
- All expand internally to `batch` commands

### Intent Meta-Tools

- `do(intent, dry_run)` — NL → Haiku plan → validate → batch execute
- `ask(question)` — NL read-only question → deterministic route → Haiku summarize
- `animator_intent`, `vfx_intent`, `ui_intent` — domain-specific NL intent tools (core)

### MCP Resources (resources.py)

4 resource URIs registered:
- `unity://scene/hierarchy` — current scene hierarchy summary
- `unity://console/errors` — recent console errors
- `unity://editor/state` — editor state (play mode, scene, selection)
- `unity://tools/categories` — available tool categories

### Plugin System

3-source discovery:
1. **pkgutil built-in**: discovers modules inside `plugins/` package
2. **entry_points**: `importlib.metadata.entry_points(group="unity_mcp.plugins")` for pip-installed packages
3. **UNITY_MCP_PLUGIN_DIRS**: env var pointing to filesystem directories with plugin modules

Each plugin implements `register(mcp, send_fn, args_fn)`. Plugin API facade (`plugin_api.py`) provides stable exports: `API_VERSION`, `RO`, `RW`, `RW_IDEM`, `DEL`, `SamplingService`, `strip_fences`, `sanitize_intent`, `register_dsl_tools()`, `register_read_cmds()`, `register_write_cmds()`, `register_tools()`, `register_features()`.

### Code Intel Tools (server 0.4.0)

**await_compile (NEW):** Read-only tool that blocks until Unity finishes C# compilation AND domain-reloading. Returns compile errors as plain text. Survives domain-reload disconnect via reconnect + re-query. `timeout=0` = instant snapshot. Replaces `sleep`-then-poll patterns.

- `find_references` — semantic search for usages of a symbol (method, property, class)
- `compile_preflight` — pre-compile validation + type inference for code edits
- `semantic_at` — AST analysis at a line:col position (type info, references, quick-fix suggestions)

### Deferred MCP Tool-Schema Loading (F4, server 0.3.0)

Non-core tools return a **stub inputSchema** `{"type":"object"}` from `list_tools` instead of full schemas. Full schemas are served lazily via a new meta-tool:

```
resolve_tool_schema(tools: "comma,separated,names") -> plain text
```

Returns a plain-text schema block (no JSON), one tool per section. Backwards-compatible: MCP dispatch doesn't validate against inputSchema, so stubbed tools execute normally. Environment escape hatch: `UNITY_MCP_FULL_SCHEMAS=1` disables stripping (default off).

**Token impact:** ~58-68% per-turn schema-token reduction. Enabled by default; discovery-gated tools show stub until explicitly enabled in session.

### Middleware (23 layers, `UNITY_MCP_MIDDLEWARE=1`)

Retry Watchdog, Confidence Decay (gated <0.5), Taint Tracking, Periodic State Injection (staleness-gated), Path Cache, Dead Write Elimination, Starvation Monitor, Blast Radius Tags, Incremental Verification, Workflow Phase FSM, Visual Verification (Haiku), Play Mode Auto-Routing, find_objects Cache Bypass, Batch Conflict Scan, Post-mutation Snapshot, Component Cache, Console Error Categorization, PrefetchCache (TTL 12s), HierarchyDiff, Distiller, Disambiguator, SchemaGuard, Asymmetric Reflection

### Additional Env-Gated Features

| Env Var | Default | Feature |
|---------|---------|---------|
| `UNITY_MCP_HINTS` | `1` (on) | ToolHinter — suggests underused tools. Set `=0` to disable |
| `UNITY_MCP_BUDGET` | `1` (on) | CostTracker/BudgetRouter — Haiku spend tracking. Set `=0` to disable |
| `UNITY_MCP_SCENE_BRIEF` | off | SceneBrief — injects scene context on first call |
| `UNITY_MCP_SPECULATION` | off | SpeculativeLayer — speculative prefetch |
| `UNITY_MCP_LESSONS` | off | LessonStore/LessonRecorder — learns from usage patterns |
| `UNITY_MCP_WATCHDOG` | off | ProactiveWatchdog — background validate_references + console scan |
| `UNITY_MCP_INFERENCE` | off | SessionContext/Inferrer — argument inference from session |
| `UNITY_MCP_DISTILL` | `1` (on) | ResponseDistiller — heuristic response compression (set in server.py via setdefault); strip_defaults now always applies to {get_component, inspect, get_object_detail} regardless of this flag (use `_no_strip=1` arg to opt-out) |
| `UNITY_MCP_FULL_SCHEMAS` | off | Deferred Schema Loading — set `=1` to disable schema stripping (return full inputSchema for all tools instead of stubs) |

## Implementation Notes (для Developer)

### Tool Pattern

```python
@mcp.tool()
async def tool_name(arg1: str, arg2: int = 10) -> str:
    """Short description under 20 tokens."""
    return await _send("cmd_name", {"arg1": arg1, "arg2": arg2})
```

`_send()` helper: raises ToolError on `!ok`, returns `data` or file path. Routes through middleware pipeline when `UNITY_MCP_MIDDLEWARE=1`. Per-command timeouts via `COMMAND_TIMEOUTS` dict (run_tests/run_playtest/fuzz_playtest: 120s, compile_preflight: 60s, batch: 60s, default: 30s).

### Consolidated Tool Pattern (action-based)

```python
@mcp.tool()
async def animation(action: str, path: str, clip: str = "", ...) -> str:
    """Animation CRUD. Actions: get|create|edit|add_key|remove_key|set_keys|set_loop|preview"""
    return await _send("animation", {"action": action, "path": path, "clip": clip, ...})
```

### Plugin Registration

```python
# plugins/my_plugin.py
def register(mcp, send_fn, args_fn):
    @mcp.tool()
    async def my_tool(arg: str) -> str:
        return await send_fn("my_cmd", {"arg": arg})
```

## Code Locations

- Server: `server/src/unity_mcp/server.py`
- Bridge: `server/src/unity_mcp/bridge.py`
- ConnectionSlot: `server/src/unity_mcp/connection_slot.py`
- Lockfile: `server/src/unity_mcp/lockfile.py`
- Compile probe: `server/src/unity_mcp/compile_state.py`
- Middleware: `server/src/unity_mcp/middleware.py`
- Schema Registry (deferred): `server/src/unity_mcp/tools/schema_registry.py`
- Tools: `server/src/unity_mcp/tools/`
- Plugins: `server/src/unity_mcp/plugins/`
- Plugin API: `server/src/unity_mcp/plugin_api.py`
- Resources: `server/src/unity_mcp/resources.py`
- Tests: `server/tests/`

## TDD Scenarios (для Developer)

Tests organized by module in `server/tests/`:
- `test_server.py` — tool registration, _send helper, ToolError handling
- `test_bridge.py` — TCP connection, circuit breaker, heartbeat, keepalive, DomainReloadError
- `test_connection_slot.py` — single connection slot management
- `test_lockfile.py` — exclusive lock, stale cleanup, PID liveness
- `test_compile_state.py` — probe signals, estimated remaining
- `test_middleware.py` — each middleware layer independently
- `test_gating.py` — tier filtering, category enable/disable
- `test_plugins.py` — plugin loader, skip env, error handling
- `test_tools_*.py` — per-tool argument validation and response parsing

## Review Checklist (для Reviewer)

- [ ] Tool descriptions < 20 tokens each
- [ ] All tools async
- [ ] ToolError for user-facing errors
- [ ] Logging to stderr, not stdout
- [ ] Type hints everywhere
- [ ] New tools added to gating.py (TIER1 or category)
- [ ] Plugin tools use `register(mcp, send_fn, args_fn)` pattern
- [ ] Middleware layers idempotent and env-gated

## Deployment Notes

- **Python changes** (F03, F04, F08, F12): take effect only after MCP server restart (`/mcp` command or process restart). Live MCP server will continue showing old behavior until restarted.
- **C# changes** (F02, F13, F18): live immediately after Unity recompile.
- **Pre-existing C# EditMode test failures**: 2 failures in `MCPPrefabTests.Revert_RevertsChanges` and `MCPValueParserTests.ValueParser_Enum_NegativeInt` are unrelated to Wave 0; their source files (prefab logic, ValueParser.cs) were not touched by any Wave 0 commit. C# NUnit tests for Wave 0 fixes written locally at `unity-test-project/Assets/Tests/Editor/MCPF02F13F18Tests.cs` (gitignored directory, not version-controlled).

## Related

- Skill: `.claude/skills/python-mcp.md`
- Bridge: `AI/tcp-bridge.md`
- Architecture: `AI/architecture.md`
- Batch: `AI/batch.md`
