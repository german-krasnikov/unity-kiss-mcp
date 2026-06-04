# Feature: Architecture Overview

## Overview

MCP-сервер для управления Unity Editor из Claude Code с минимизацией токенов (10-15x сжатие vs JSON).

## Architecture (для Architect)

```
Claude Code ←──stdio──→ Python MCP Server ←──TCP:9500──→ Unity Editor Plugin
     │                        │                                │
     │  MCP Protocol          │  Binary protocol               │  Unity API
     │  (JSON-RPC 2.0)        │  [4B len BE][JSON]             │  (main thread)
     │                        │                                │
     │                        ├─ ConnectionSlot (single)       ├─ CommandRouter (async)
     │                        ├─ Capability Gating (TIER1+cat) ├─ PluginRegistry (IMCPPlugin)
     │                        ├─ Plugin system (auto-discovery) ├─ CommandRegistry + ValueParser
     │                        │  - opt-in disable: env UNITY_MCP_SKIP_PLUGINS=prefix ├─ CommandSchema (validation)
     │                        ├─ Deferred Schema Loading       ├─ 7 Serializers
     │                        │  (stub schemas + lazy resolve) ├─ RefManager ($a-$zz)
     │                        ├─ 23-layer Middleware (opt-in)   ├─ PlaytestRunner + DSL
     │                        ├─ CompileStateProbe             ├─ RuntimeHelper (Play Mode)
     │                        ├─ PID Lockfile (exclusive)      ├─ MultiViewCapture (4-panel)
     │                        └─ Heartbeat (15s, reconnect)    ├─ CodeExecutor (Roslyn)
     │                                                         └─ Guards (compile/play/runtime/tool)
```

### Почему такая архитектура

- **Python MCP**: Claude Code запускает через stdio, зрелый SDK
- **TCP socket**: Переживает domain reload Unity (vs WebSocket)
- **Binary framing**: 4 байта длины BE + JSON, минимальный overhead
- **No cache**: All calls go directly to Unity via bridge.send (scene changes too frequently)

### Components

1. **MCP Server** (Python: 80+ modules total, including `server.py`, 23 tools modules + support)
   - **89 core MCP tools registered**. Gating: TIER1=38 core (hardcoded). External plugins can add more tools dynamically.
   - Transport: stdio (default) or streamable-http (`UNITY_MCP_TRANSPORT=http`)
   - FastMCP("UnityMCP", lifespan=lifespan)
   - Lifespan: auto-discover Unity port from `~/.unity-mcp/ports/*.port`, acquire exclusive PID lockfile, create ConnectionSlot, connect bridge, fetch disabled tools cache (`get_disabled_tools`), push Python-authoritative catalog (`_push_catalog`), start heartbeat, register reconnect callbacks, load_plugins()
   - Plugin system (3-source discovery: pkgutil built-in, entry_points, UNITY_MCP_PLUGIN_DIRS env): each plugin has `register(mcp, send_fn, args_fn)`. UNITY_MCP_SKIP_PLUGINS env (comma-separated prefixes) skips matching plugins.
   - _send() helper: sends to bridge via slot, raises ToolError on !ok
   - File-based output: checks `file` field in response → returns path string
   - Tool annotations: readOnlyHint, destructiveHint for MCP compliance
   - Dynamic tool filtering: patches `mcp._mcp_server.request_handlers[ListToolsRequest]` with gating + disabled-set subtraction (hide-disabled-set model, not allowlist)

2. **TCP Bridge** (Python: `bridge.py` + `connection_slot.py` + `lockfile.py` + `compile_state.py`)
   - **ConnectionSlot**: single UnityBridge connection (connect/reconnect/list)
   - **UnityBridge**: AsyncIO TCP client, 4-byte BE length prefix JSON
   - Socket: TCP_NODELAY, SO_KEEPALIVE (idle=60s, interval=10s, count=3 on macOS/Linux)
   - **Heartbeat**: 15s interval, raw ping, 3 consecutive failures → close, 2s polling when disconnected (5s when busy). Sole reconnect mechanism.
   - **CompileStateProbe**: heuristic compile/domain-reload detector (state file, PID check)
   - **DomainReloadError**: on Unity `going_away` event → immediate close + busy flag
   - **PID Lockfile**: `~/.unity-mcp/server-{port}.lock`, fcntl.flock exclusive, kills stale servers (SIGTERM→SIGKILL)
   - Reconnect: cooldown MIN_RECONNECT_INTERVAL=2.0s, ping verification, fires callbacks
   - Max message: 10MB, timeouts: 30s default, 60s compile_preflight/batch, 120s run_tests/run_playtest/fuzz_playtest

3. **Unity Plugin** (C#: 70+ files, ~13400 LOC)
   - **MCPServer.cs**: TCP listener port 9500, 4-byte BE framing, 10MB max, SO_KEEPALIVE, port discovery file, state file (`ready`/`compiling`/`reloading`), `going_away` event before domain reload, single client, client generation tracking
   - **CommandRouter.cs**: RegisterAll() → calls core commands + PluginRegistry.RegisterAllPlugins() for external plugins, data-driven IsMutatingCommand/IsRuntimeCommand
   - **PluginRegistry.cs**: Static registry for IMCPPlugin implementations. Plugins register via `[InitializeOnLoad]`. One-way asmdef dependency: external → public.
   - **IMCPPlugin.cs**: Interface — Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands
   - **CommandRegistry.cs**: Func<string,string> handlers, mutating + runtime flags
   - **CommandSchema.cs**: parameter validation with fuzzy did-you-mean suggestions (79 schemas)
   - **ValueParser.cs**: vectors, quaternions, colors, arrays, type-aware SetPropertyValue
   - **InputNormalizer.cs**: component/property/value normalization
   - **BatchHelper.cs**: multi-command text parser + executor (on_error=continue/stop)
   - **7 Serializers**: HierarchySerializer (tree, MAX_NODES=3000, incremental, summary), ComponentSerializer (key-value, UnityEvent expansion, PrefabStage-aware), AnimationSerializer, TimelineSerializer, AnimatorControllerSerializer, ParticleSerializer, ShaderSerializer
   - **RefManager**: short refs $a-$zz (702 slots), invalidated on scene change
   - **ErrorHelper**: contextual errors with did-you-mean hints

4. **Guards (C#)**
   - **Compile guard**: blocks all except ping, get_version, get_console, screenshot, get_enabled_tools, compile_status
   - **Play Mode guard**: blocks mutating commands (changes would be lost)
   - **Runtime guard**: runtime commands blocked outside Play Mode
   - **Tool enable guard**: MCPSettings per-tool toggle (ping/get_version/get_enabled_tools always allowed)
   - **Fast-path commands** (bypass main thread): ping, get_version, status, get_enabled_tools

5. **Per-command timeouts (C#)**: run_tests=130s, run_playtest=130s, batch=65s, wait_until/move_to/test_step=30s, default=25s

6. **Post-mutation features**: console error capture, SuggestNext (recommends verification tool), auto-return parent subtree after create/delete

## Tool Categories

### TIER1 (always visible, 38 core)

Core (38): 24 base + 3 intent + 3 code-intel + 8 runtime = get_hierarchy, get_component, inspect, set_property, create_object, delete_object, manage_component, batch, get_console, get_compile_errors, screenshot, scene, editor, search_scene, run_tests, discover_tools, get_enabled_tools, setup_objects, set_properties, configure_objects, set_parent, do, ask, get_metrics, animator_intent, vfx_intent, ui_intent, find_references, compile_preflight, semantic_at, invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest

### Category: object (8)
find_objects, get_object_detail, get_components_list, set_active, set_material, wire_event, unwire_event, set_property_delta

### Category: animation (4)
animation, timeline, animator, particle

### Category: asset (5)
asset, material, prefab, scriptable_object, project_settings

### Category: advanced (16)
shader, references, validate_references, menu, checkpoint, recompile, execute_code, check_colliders, get_schema, scan_scene, spatial_query, auto_fix, smart_build, apply_template, save_template, list_templates

### Category: ui (4)
create_ui, set_rect, validate_layout, get_spatial_context

### Category: runtime (8)
invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest

### Category: connection (2)
list_connections, reconnect_unity

### Category: session (10)
fingerprint, scene_diff, get_changes, save_session, load_session, screenshot_baseline, screenshot_compare, save_skill, use_skill, list_skills

## C# Commands (CommandRouter)

### Meta (non-mutating)
ping, get_version, get_enabled_tools, get_disabled_tools, set_tool_catalog

### Read (non-mutating)
get_hierarchy, get_component, get_components_list, get_object_detail, find_objects, inspect, get_console, get_compile_errors, compile_status, screenshot, search_scene, validate_references, validate_layout, get_spatial_context, fingerprint, scan_scene, check_colliders, get_schema, get_changes, scene_diff, run_tests, get_test_results, recompile, checkpoint

### Write (mutating)
create_object, delete_object, set_property, set_property_delta, set_active, wire_event, unwire_event, manage_component, set_parent, set_material, batch (mutating=false), execute_code

### Consolidated (action-based)
scene (new/open/save/discard), animation (get/create/edit/add_key/remove_key/remove_curve/set_keys/set_loop/preview), timeline (get/create/edit/add_track/remove_track/add_clip/remove_clip/set_binding/set_timing/mute/unmute/lock/unlock/preview), references (get/find_to/remap), editor (state/play/stop/pause/select/project_path), animator (get/add_param/add_state/add_transition/set_default/remove), particle (get/create/set/apply), shader (get/create/set/graph_get/graph_create/graph_node/graph_edge), asset (find/get_info/create/move/duplicate/delete/get_dependencies/import_settings/export_package/import_package), material (create/get/set/copy/list_properties), prefab (save/create_variant/apply/revert/get_overrides/unpack), scriptable_object (create/get/set/list_types/find), project_settings (get/set), spatial_query (nearest/in_front_of/objects_in_radius/bounds_info/raycast/spatial_map), create_ui, set_rect, menu (execute/list)

### Runtime (Play Mode only)
invoke_method, set_runtime_property, query_state, wait_until, move_to, test_step, run_playtest

## Key Systems

### Capability Gating (Python: `tools/gating.py`)
- **CORE tools** (22): locked, always visible, can only be hidden via `FORCE_VISIBLE` escape hatches (discover_tools, get_enabled_tools, do, ask, editor, get_console, get_compile_errors, reconnect_unity, list_connections). Example: `is_core("get_hierarchy")` → True
- **Themed catalog** (single source of truth): `get_catalog()` returns JSON with 14 categories + CORE list (public tools only, no NDA/plugin names). Categories: SCENE_EDIT, COMPONENTS, ANIMATION, SHADERS_MATERIAL, VFX, UI, SCREENSHOTS, UNIT_TESTS, RUNTIME, ASSETS, ADVANCED_CODE, SESSION_SKILLS, CONNECTION, META
- **Filtering pipeline**: (1) apply TIER1+session gating via `_apply_gating()`, (2) subtract disabled set from Unity MCPSettings via `_filter_tools()` (cache=None → gating-only fallback). Approach is "hide-disabled-set" (NOT allowlist — Python-only tools not in Unity's CSV wouldn't be wrongly hidden)
- **Sessions**: session-enabled via `discover_tools(category, enable)` (legacy CATEGORIES dict still works for back-compat)
- **Plugin self-registration**: `gating.register_tools("category", tools_set, tier1=tier1_subset)` lets plugins add to CATEGORIES + TIER1 without hardcoding
- **Push catalog**: `_push_catalog()` sends Python-authoritative catalog to Unity on connect/reconnect via `set_tool_catalog` command (TCP-only, silent on failure)
- **Cache model**: `_disabled_tools_cache` (refreshed on connect/reconnect); None ⇒ gating-only mode

### Plugin System

**Python** (`plugins/__init__.py`):
- 3-source discovery: (1) pkgutil built-in modules, (2) `importlib.metadata.entry_points(group="unity_mcp.plugins")` for pip-installed packages, (3) `UNITY_MCP_PLUGIN_DIRS` env var for filesystem paths
- Each plugin module: implements `register(mcp, send_fn, args_fn)` to self-register tools
- Disable via env: `UNITY_MCP_SKIP_PLUGINS=prefix1,prefix2` (comma-separated prefixes)
- Plugin API facade: `unity_mcp/plugin_api.py` — stable re-exports (RO, RW, RW_IDEM, DEL) + `register_dsl_tools()`, `register_read_cmds()`, `register_write_cmds()`, `register_tools()`, `register_features()`

**C#** (`IMCPPlugin.cs` + `PluginRegistry.cs`):
- `IMCPPlugin` interface: Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands
- `PluginRegistry.Register()` — called from plugin's `[InitializeOnLoad]` static constructor
- `PluginRegistry.RegisterAllPlugins()` — called from CommandRouter.RegisterAll()
- One-way asmdef dependency: plugin asmdef → UnityMCP.Editor

### Middleware (Python: `middleware.py` + `middleware_paths.py`, 23 layers, env UNITY_MCP_MIDDLEWARE=1)
1. Retry Watchdog — blocks identical write calls within 5s TTL
2. Confidence Decay — decreases on writes (-0.08), increases on reads (+0.15)
3. Taint Tracking — warns on ObjectReference write to unread paths
4. Periodic State Injection — auto get_hierarchy every 10 calls
5. Path Cache — hierarchy paths, fuzzy match via Levenshtein
6. Dead Write Elimination — warns overwrite without read
7. Starvation Monitor — detects 5 identical responses
8. Blast Radius Tags — warns on high-blast commands
9. Incremental Verification — checkpoint every 5 mutations
10. Workflow Phase FSM — warns after 3+ consecutive writes
11. Visual Verification — Haiku-based screenshot verification (sampling)
12. Play Mode Auto-Routing — reroutes set_property → set_runtime_property
13. find_objects Cache Bypass — serves from hierarchy cache
14. Batch Conflict Scan — detects duplicate writes, create+delete no-ops
15. Post-mutation Snapshot Verification — verifies prop=value in response
16. Component Cache — caches known components per path
17. Console Error Categorization — hints for NullRef, MissingComponent, FormatException
18. PrefetchCache — predicted reads after writes
19. HierarchyDiff — returns unified diff when <50% changed
20. Distiller — heuristic + Haiku background distillation of large responses
21. Disambiguator — resolves ambiguous paths via context clues
22. SchemaGuard — pre-flight argument validation
23. Asymmetric Reflection — compares write args vs read-back snapshot

### Additional env-gated features
- **ToolHinter** (`UNITY_MCP_HINTS`, default ON): suggests underused tools
- **SceneBrief** (`UNITY_MCP_SCENE_BRIEF`): injects scene context on first call
- **SpeculativeLayer** (`UNITY_MCP_SPECULATION`): speculative prefetch
- **LessonStore/LessonRecorder** (`UNITY_MCP_LESSONS`): learns from usage patterns
- **ProactiveWatchdog** (`UNITY_MCP_WATCHDOG`): background validate_references + console scan
- **SessionContext/Inferrer** (`UNITY_MCP_INFERENCE`): argument inference
- **CostTracker/BudgetRouter** (`UNITY_MCP_BUDGET`, default ON): Haiku spend tracking

### Auto-Batch (Python: `tools/autobatch.py`)
- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value per line)
- All expand internally to `batch` commands

### Intent Meta-Tools
- `do(intent, dry_run)` — NL → Haiku plan → validate → batch execute
- `ask(question)` — NL read-only question → deterministic route → Haiku summarize
- `animator_intent`, `vfx_intent`, `ui_intent` — domain-specific NL intent tools (core)

### Playtest System (C#: PlaytestRunner + PlaytestParser)
- DSL commands (21): MOVE, WAIT, WAIT_UNTIL, ASSERT, ASSERT_CONSOLE_CLEAN, ASSERT_BATCH, ASSERT_NEAR, TELEPORT, SNAPSHOT, INVOKE, SET, LOG, TIMESCALE, CAPTURE, ASSERT_CAPTURED, INVARIANT, ASSERT_CONSERVED, SIMULATE, MONITOR, TRACE_FLOW, ASSERT_CTA
- PlaytestState tracks state across steps
- PlaytestConfig ScriptableObject for project-specific config
- Monitor/Simulator registries for extensibility
- Global timeout 120s

### MultiView Screenshots (C#: MultiViewCapture)
- Camera modes: default, overview, overview_game, multi_view, single_view
- multi_view: 4-panel grid (Front, Left, Top, Isometric)
- Parameters: path, cellSize, supersample (1-4), custom angles, zoom, offset, fixed_size, highlight, show_colliders
- Returns file path + optional manifest (for highlight markers)

### Agent Chat Backends (C#: CliBackendBase + subclasses, v0.14.0+)
- **CliBackendBase** (abstract): Shared lifecycle host for CLI-based backends. 4 variation axes: (a) `BuildArgs` (spawn/resume argv + env-key-strip), (b) `ParseLine` (NDJSON line → ChatEvent[]), (c) `BinaryName` (CLI executable), (d) `IsPersistentProcess` (stdin loop vs spawn-per-turn). Owns spawn, drain, accumulate, SessionId, Stop, Dispose — subclasses override only the 4 axes.
- **ClaudeBackend** (ported onto base): Zero behavior change (−65 lines net). Persistent stdin loop (IsPersistentProcess=true), Claude NDJSON parser, `--resume <sessionId>` argv builder.
- **CodexBackend** (new): OpenAI Codex via spawn-per-turn model (IsPersistentProcess=false). Each turn disposes old process, spawns fresh with prompt baked into argv (`-c mcp_servers.*` flags). Stdin closed immediately.
- **CodexArgBuilder**: Constructs `codex exec --json` argv. Re-passes three `-c mcp_servers.*` flags every turn.
- **CodexStreamParser**: Codex NDJSON → ChatEvent (agent_message, mcp_tool_call, command_execution[aggregated_output/declined], file_change[changes], turn.completed usage; CostUsd=0).
- **BackendRegistry** & **BackendKind** enum: Factory dispatch. User selects Claude or Codex from dropdown.
- **PendingTurnState v3**: Now persists `BackendKind` for domain-reload survival (back-compatible with v1/v2).

### Per-Backend Settings (C#: BackendConfig + BackendSettingsForm, v0.15.0 F9)
- **BackendConfig.cs** — [Serializable] per-backend configs (model, permission_mode, timeout, extra_args)
- **BackendConfigStore.cs** — Loads/saves to `Library/MCP_ChatBackendConfig.json` (project-local, NOT global ~/.codex/config.toml)
- **BackendSettingsForm.cs** — UIToolkit foldout per backend with model/permission/timeout/extra-args dropdowns
- **Wiring:** `ClaudeArgBuilder` + `CodexArgBuilder` read from store, inject into argv construction
- **ArgTokenizer.cs** — Shell-style quote-aware split (double+single quotes, unbalanced trailing tolerated); centralizes whitespace+quote parsing for both builders; fixes silent corruption of quoted multi-word ExtraArgs values (e.g., `--append-system-prompt "be terse"`); +11 tests

### Typed Context Tags (C#: ChipKind + ResponseTagInliner, v0.15.0 F10)
- **ChipKindDetector.cs** — Pure `Detect()` method categorizes chips: Hierarchy, Scene, Script, Prefab, Material, Texture, ScriptableObject, Asset
- **ChipData.Kind** — Each chip carries a `ChipKind` enum
- **ChipConfig.cs** — Per-kind depth config (none|path|summary|full), persisted in BackendConfigStore
- **Send-side (input):** ChipContextResolver.EmitTyped() formats as `[hierarchy:/Player #123]`, `[script:PlayerController]`, `[scene:.../Main.unity]` for AI consumption; visual chips show left-side kind-prefix (color-coded)
- **Receive-side (response):** ResponseTagInliner.Apply() parses ONLY exact `[kind:ref]` format (conservative regex, no false positives on markdown/code/bare brackets); renders compact colored pills with `<link>` click-nav (symmetric with input)
- **Tests:** ChipKindDetector 13/13, ResponseTagInliner 17/17 (false-positive guards), EmitTyped 7/7, DepthFor 10/10, ChipConfig 3/3

### UX Features (v0.15.0 F1–F10)
- **F1 (Token Reset):** TokenResetTests ensure counters reset on backend/model switch
- **F2 (Cascade Restore):** TurnUndoTracker.RestoreFromIndex() reverts any earlier turn + all later turns (reverse order)
- **F3 (Approve Gate):** Button shows only when turn has real tool calls (_turnHasToolCalls flag)
- **F4 (Hierarchy #ID):** ChipContextResolver appends `#<instanceID>` to scene object refs (SelectionSummary.Summarize disambiguation)
- **F5 (Inline Chips):** InlineChipTracker + InlineChipOverlay (drag-drop, removable ✕, context menu "Add Selection")
- **F6 (Auto-Scroll Toggle):** EditorPref gate (default ON) for scroll behavior during streaming
- **F7 (Status Distinction):** ChatBackendProbe detects Chat-active vs CLI-listening (3-state: Down/Listen/ChatActive); domain-reload safe (per-call resolution)
- **F8 (No Beta Labels):** Removed "(Beta)" from chat toggle + settings foldout
- **F9 (Settings Form):** Per-backend config form → own JSON → CLI args (see BackendConfig above)
- **F10 (Typed Tags):** Kind-aware input/output chips with configurable depth (see Typed Context Tags above)

### Editor UI Windows (C#: UIToolkit)
- **MCPSettings Window** (MCPSettings.cs): Tool visibility toggles, organized by theme categories (CORE locked, others toggle/tri-state group masters), search bar, presets (Minimal/Full/No-visuals), dynamic Plugins section from PluginRegistry. Stylesheet: `MCPSettings.uss`.
- **MCPStatus Window** (MCPStatusWindow.cs): Connection status monitor. UIToolkit-based with breathing heartbeat pulsation (ECG beat when connected, gentle beat when listening, flatline when stopped). Centered orb (`.orb`) + halo ring (`.orb-halo`) with state-driven colors & USS class-triggered pulsation (border-width + opacity + background-color transitions, 2021.3-safe). Polling every 700ms for state. Buttons: Restart MCP / Kill MCP / Reimport. Stylesheet: `MCPStatus.uss`.
- **Stylesheet Helper** (MCPEditorUtils.LoadStyleSheet): Shared two-path loader for `.uss` files, called by both MCPSettings & MCPStatus windows (DRY; handles package-relative asset lookup).

### Code Execution (C#: CodeExecutor)
- Roslyn C# execution via `execute_code` command
- Sandboxed with blocklist
- Supports undo_label for undo grouping

### Undo Group Primitives (C#: UndoGroupHelper)
- **UndoGroupHelper** (`UndoGroupHelper.cs`, public core API): Reusable named-group rollback primitive with 4 methods: `OpenNamedGroup()`, `CloseNamedGroup()`, `RevertToBeforeGroup()`, `CanRevert()`.
- **F6 (Chat, v0.11.0):** `TurnUndoTracker` + `RestoreButton` consume this API to wrap each agent turn in an Undo group; Restore button reverts the turn's mutations. Only the last turn's button is active.
- **F27 (shipped v0.6.1):** Atomic batch rollback (opt-in `atomic=true` param) reuses the same primitive — reverts all prior ops on first failure via `OpenNamedGroup`/`RevertToBeforeGroup`. One unified rollback system across Chat (per-turn) and Batch (per-operation).

### Spatial Queries (C#: via spatial_query command)
- Actions: nearest, in_front_of, objects_in_radius, bounds_info, raycast, spatial_map

### Code Intelligence (Python: `tools/code_intel.py`)
- `find_references(symbol)` — semantic C# symbol search via Roslyn
- `compile_preflight(file_path, new_content)` — validates C# without disk write
- `semantic_at(file_path, line, col)` — type/symbol info at position

## Implementation Notes (для Developer)

### Data Flow
```
Claude → MCP tool call → TCP send → Unity dispatch → Serialize → TCP response → MCP return
```

### Key Constraints
- Unity API only on main thread
- TCP callback → ConcurrentQueue → EditorApplication.update
- Max message size: 10MB
- Default timeout: 25s (C# side)

### Wave 3: Tool-Gating Fix + Settings UI

**P0 — Hide-Disabled-Set Model (server.py + gating.py):**
- **Problem**: Unity MCPSettings form checkboxes saved zero tokens because `_filter_tools` kept any tool where `is_visible(name)` (true for all TIER1 ≈ every tool).
- **Solution**: Switched from "allow list" to "hide-disabled-set" approach:
  1. Unity reports disabled tools via `get_disabled_tools` CSV (per MCPSettings form state)
  2. Python `_filter_tools` applies gating (TIER1 + session-enabled), then subtracts disabled set
  3. Escape hatches: `FORCE_VISIBLE` set preserves connectivity tools (discover_tools, get_enabled_tools, reconnect_unity, list_connections, do, ask, editor, get_console, get_compile_errors)
  4. Cache model: `_disabled_tools_cache` refreshes on connect/reconnect; None → gating-only fallback (no TCP)
- **Why not allowlist**: Python-only tools aren't in Unity's CSV; allowlist would wrongly hide them

**P1 — Python-Authoritative Catalog + UIToolkit Settings (gating.py + MCPSettings.cs + 3 new files):**
- **Single Source of Truth**: `gating.get_catalog()` returns themed JSON with 14 categories (CORE, SCENE_EDIT, COMPONENTS, ANIMATION, SHADERS_MATERIAL, VFX, UI, SCREENSHOTS, UNIT_TESTS, RUNTIME, ASSETS, ADVANCED_CODE, SESSION_SKILLS, META) + public tools only
- **Push Mechanism**: `_push_catalog()` sends catalog to Unity via `set_tool_catalog` on connect/reconnect (TCP-only, silent on failure)
- **Persistence**: Unity saves to EditorPref `UnityMCP_Catalog`; MCPSettings queries via `GetCatalog()` / `SetCatalog(json)`
- **UIToolkit Rewrite**: `MCPSettings.cs` now uses UIToolkit (foldout groups, tri-state group masters, search, presets Minimal/Full/No-visuals, CORE locked, separate Plugins section)
- **New C# Files**: `CatalogParser.cs` (JSON→dict), `MCPSettingsUI.cs` (foldout builder), `MCPSettingsCategoryGroup.cs` (tri-state logic), `MCPSettings.uss` (styling)

### Wave 1 Hardening Fixes (Middleware Error-Dedup & Path Caching)

**F16 — Error-Dedup Gate (middleware.py):**
- **Problem**: Gated on whole-body substring scan (`raw_ok = not any(kw in result for kw in ("Failed","Error","err:"))`) that fired on SUCCESS payloads merely containing "Error" (e.g., `get_console` with Error-level logs, an object named "ErrorHandler"), truncating the 2nd identical read to 80 chars and poisoning hierarchy-diff cache. Same flag incorrectly fed `LessonRecorder.record`, so successful reads accrued bogus "fail" lessons.
- **Fix**: Gate on `protocol_err` (the protocol dict `ok` flag captured at dict-flattening step). Same flag now feeds both dedup logic AND LessonRecorder.
- **Also fixed**: `dedup_error` key collision (was `[:80]` → prefix collisions) now keys on FULL message. `_error_dedup` is a bounded `OrderedDict(256)` with LRU eviction to prevent unbounded growth.

**F17 — Negative-Path Cache Poison (middleware.py):**
- **Problem**: `resolve_path_live` cached "absent" paths for 10s TTL even on transient `search_scene` TCP failures, poisoning that path for the full duration. Any `create_object`/`rename` during that window would be blocked because the target was already marked "not found".
- **Fix**: No longer write negative-path cache when `search_scene` TCP call raised (guarded by `search_ok` flag). Additionally, any `WRITE_CMDS` command now clears the entire negative-path cache (a create/rename can make a previously-absent path resolvable).

**F05 — DRY Refactor (middleware.py):**
- **Problem**: `_read_cacheable` set was defined twice (line duplication).
- **Fix**: Hoist to module-level `_READ_CACHEABLE` frozenset.

## Test Infrastructure

### Python Tests: 1588 unit tests + 52 live tests
- Default: `pytest -m "not live"` — unit tests, $0 cost (1588 tests, includes test_catalog.py = 19)
- With Unity: `pytest -m "live"` — adds 52 live integration tests, $0 cost (sampling disabled)
- Real Haiku: `pytest -m "live and live_haiku"` — ~$0.001/run (visual regression, opt-in)
- Test order: unit → C# → live (live always last, occupies TCP)

### Live Test Isolation (server/tests/live/)
- **Session-scoped PlayMode**: `_play_mode_session` fixture enters PlayMode once, reuses across 16+ tests
- **GridTest scene auto-open**: `_ensure_gridtest_scene` auto-loads Assets/Scenes/GridTest.unity at session start
- **Per-test scene reload**: `_reload_scene()` uses EditorSceneManager.LoadSceneAsyncInPlayMode (~0.5s, full state isolation without restart)
- **Resettable collectibles**: GridPlayer.ResetState() resets MoveSpeed + re-enables all collectibles via SetActive(true)
- **Test ordering**: edit-mode (first) → play-mode (session reused) → destructive/reconnect (last)

### C# NUnit Tests: 756 tests (EditMode + PlayMode combined)
- 754 passed (2 pre-existing failures: `MCPPrefabTests.Revert_RevertsChanges`, `MCPValueParserTests.ValueParser_Enum_NegativeInt` — unrelated to Wave 1)
- Mixed edit/play mode tests in Unity Test Runner (independent of live tests, no mutex)

### Key Fixtures (conftest.py)
- `bridge_response(data, ok, err, file)` — factory fixture for mock bridge responses
- `mw` — shared Middleware() instance
- `send_fn` — shared AsyncMock
- `_isolate_home` — prevents ~/.unity-mcp/ pollution (autouse)
- `_reset_metrics` — resets METRICS singleton (autouse)
- `_clean_unity_env` — clears env var pollution (autouse)
- `_enable_validate` — guards SchemaGuard module-level mutation (autouse)

## Code Locations

**Python** (80+ modules):
- `server/src/unity_mcp/server.py` — MCP server setup, lifespan, dynamic filtering
- `server/src/unity_mcp/bridge.py` — UnityBridge TCP client, heartbeat
- `server/src/unity_mcp/connection_slot.py` — ConnectionSlot: single connection management
- `server/src/unity_mcp/lockfile.py` — PID lockfile with fcntl.flock
- `server/src/unity_mcp/compile_state.py` — CompileStateProbe heuristic
- `server/src/unity_mcp/middleware.py` — 23-layer middleware pipeline (core)
- `server/src/unity_mcp/middleware_paths.py` — PathResolverMixin extracted from middleware.py
- `server/src/unity_mcp/metrics.py` — MetricsRegistry singleton
- `server/src/unity_mcp/sampling.py` — SamplingService for visual verification
- `server/src/unity_mcp/tools/` — 23 tool modules (scene, objects, asset, animation, batch, codegen, skills, spatial, ui, connection, runtime, gating, autobatch, intent tools, code_intel, etc.)
- `server/src/unity_mcp/plugins/` — plugin auto-discovery (3-source loader)
- `server/src/unity_mcp/plugin_api.py` — stable public API for external plugins
- `server/src/unity_mcp/reflect/` — Asymmetric Reflection (rules_objects, rules_runtime, rules_batch)
- `server/src/unity_mcp/som/` — Set-of-Mark visual annotation
- `server/src/unity_mcp/screenshot_describe/` — semantic screenshot description
- `server/src/unity_mcp/budget/` — cost tracking with file lock
- `server/src/unity_mcp/hinter.py` — ToolHinter post-call patterns
- `server/src/unity_mcp/schema_guard.py` — pre-flight validation
- `server/src/unity_mcp/schema_cache.py` — LRU component schema cache
- `server/src/unity_mcp/clarifier.py` — Disambiguator
- `server/src/unity_mcp/distiller.py` — ResponseDistiller
- `server/src/unity_mcp/degrade.py` — Graceful Degradation helper
- `server/src/unity_mcp/visual_diff.py` — visual regression testing
- `server/src/unity_mcp/sampling_postproc.py` — Haiku output normalizer

**C#** (130+ files, 13400+ LOC):
- **Core** (50+ files): MCPServer, CommandRouter (3 partials), CommandRegistry/Schema, IMCPPlugin/PluginRegistry, ObjectManager, ValueParser, InputNormalizer, BatchHelper, HierarchySerializer, ComponentSerializer, RefManager, ErrorHelper, RuntimeHelper, PlaytestRunner (2 partials), PlaytestParser, MultiViewCapture, CodeExecutor, SearchHelper, SpatialHelper, AnimationHelper, TimelineHelper, AnimatorControllerHelper, ParticleHelper, ShaderHelper, ShaderGraphHelper, UIHelper, ReferenceHelper, AssetDatabaseHelper, ProjectSettingsHelper, MaterialHelper, PrefabHelper, ScriptableObjectHelper, MCPSettings, MCPSettingsUI, MCPSettingsCategoryGroup, CatalogParser, MCPStatusWindow, MCPStatusModel, MCPStatusBarWidget, MCPActions
- **Chat Module** (80+ files, optional behind UNITY_MCP_CHAT define):
  - **Backends:** CliBackendBase, ClaudeBackend, CodexBackend, CodexArgBuilder, CodexStreamParser, BackendRegistry, BackendConfig, BackendConfigStore, BackendSettingsForm
  - **UX Features:** MCPChatWindow (7 partials: Drain, FlowBar, Chips, InlineChips, Selector, Approve, Slash, Resize), RestoreButton, TurnUndoTracker, SelectionSummary, CompileAutoFix, EditorStateSnapshot, ToolPing
  - **Context & Tags:** ChipContextResolver, ChipKindDetector, ResponseTagInliner, InlineChipData, InlineChipOverlay, InlineChipKeyHandler
  - **Infrastructure:** ChatStreamParser, ChatEvent, ChatTranscript, IChatBackend, ChatBinaryResolver, ChatProcess, ChatMcpConfigWriter, PendingTurnState, ReloadGuard, SentTextCache, StderrRingBuffer
  - **Tools & Input:** ToolVerbMap, ToolCallAccumulator, ToolCallRecord, ToolChipGrouper, ToolDetailBuilder, ToolGroupState, ToolGroupSummary, UserTurnBuilder, UserToolResultParser
  - **UX/Formatting:** TokenFormat, EnterKeySend, EnterKeyLogic, ChatActivityState, ChatLabel, ChatRefAction, ChatRefResolver, CopyableText, CopyTextBuilder, InputHeightCalc, JsonArrayScan, ArgTokenizer, ArgQuoting
  - **Rendering:** Markdown/ (MdBlock, MarkdownParser, MarkdownParser.Blocks, MarkdownInline, IChatBlockRenderer, ChatBlockRendererRegistry, ChatBlockRendererFactory, MarkdownBlockRenderer, MarkdownBlockRenderer.Table, MarkdownBlockRenderer.List, ImageBlockRenderer, ChatLinkify), Mermaid/ (MermaidGraph, MermaidParser, MermaidLayout, MermaidLayout.Layers, MermaidBlockRenderer, MermaidView, MermaidEdgePainter)
  - **Test Suites** (25+ NUnit files): ChatStreamParserTests, CliBackendBaseTests, CodexArgBuilderTests, CodexStreamParserTests, ClaudeArgBuilderTests, ToolVerbMapTests, EnterKeySendTests, PendingTurnStateTests, SelectionSummaryTests, SentTextCacheTests, ApproveFlowTests, SlashRegistryTests, SlashPopupTests, InlineChipTrackerTests, ArgTokenizerTests, ArgQuotingTests, BackendConfigStoreTests, BackendRegistryTests, ChatActivityStateTests, ChatLinkifyTests, ChatMcpConfigWriterTests, ChatProcessTests, ChatBinaryResolverTests, ChipContextResolverTests, ChipKindDetectorTests, ResponseTagInlinerTests, TokenResetTests, RestoreButtonTests, TurnUndoTrackerTests, and Markdown/Mermaid render tests (66 cases total)
  - **Styling:** MCPChatWindow.uss
  - **Assembly:** UnityMCP.Editor.Chat.asmdef (one-way ref to core)

## TDD Scenarios (для Developer)

### Phase 0: TCP Skeleton
1. **test_tcp_connect**: client connects → connection established
2. **test_tcp_send_receive**: send bytes → receive echo
3. **Test_Server_AcceptsConnection**: listener starts → client connects

### Phase 1: Reading Scene
1. **test_get_hierarchy_returns_text**: call tool → text tree returned
2. **Test_Serialize_FormatsCorrectly**: scene objects → text format

## Review Checklist (для Reviewer)

- [ ] Token efficiency: text format, not JSON
- [ ] Thread safety: Unity API only on main thread
- [ ] Error handling: graceful degradation
- [ ] Reconnection: heartbeat-driven reconnect
- [ ] Guards: compile, play mode, runtime, tool enable

## Related

- Skills: `.claude/skills/`
- Changelog: `AI/changelog.md`
