# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.6.0] — 2026-06-03

- **Aura Status-Bar Pill with State-Driven Pulsation** (2026-06-03) — Redesign the AppStatusBar MCP pill as an opaque chip + colored border (fixes the low-contrast empty-box look) with a beacon dot and a faked halo. Pulsation by state: connected = radiating ring + dot heartbeat, waiting = in-place swell, stopped = static dimmed dot. Text pinned opaque for legibility; the whole chip opens the action menu. Palette extracted to a testable MCPStatusBarPalette class with NUnit EditMode tests.
- **Settings Window Native Theme** (2026-06-03) — Replaced hardcoded navy hex in MCPSettings.uss with `var(--unity-colors-*)` theme variables (window-background, default-border, label-text) so the settings panel blends with editor theme; stripped custom button/hover chrome. Matches MCPStatus.uss + MCPChatWindow.uss. 139→119 lines.
- **Chat UI Native Redesign: Header Removal + Bottom Footer + Token Readout + Track+Chip Animation** (2026-06-03) — Drop entire header/toolbar; replace cost badge with native tokens-only readout (↑ in ↓ out, new TokenFormat.Abbr pure helper, 6 NUnit tests); move agent/backend selector + Ask/Agent toggle (now native segmented control) + token readout into unified bottom footer bar. Native button fidelity (3px radius, no bold, pressed state via theme variables). Collapse redundant dividers to one (`.input-area` top border only, theme USS variables). Kill typing-dots indicator. Rework FlowBar activity animation from broken full-bar translate to fixed track + traveling chip with colour crossfade Sending→Receiving (950ms tick, smooth). MCP Status window: replace navy `#1a1a2e` + custom hex with Unity theme USS variables, semaphore orb colours kept. Bottom status-bar pill: LEFT placement (Insert(0), no overlap), self-heal persistence on dock/maximize/play-mode detach, calmer pulse (Up=steady 1.0, Listen=gentle breathe 0.85↔0.6, Down=dim 0.5; no server change). New files: TokenFormat.cs + TokenFormatTests.cs. Modified: MCPChatWindow.cs (split → .Drain.cs + .FlowBar.cs), MCPChatWindow.uss, MCPStatus.uss, MCPStatusBarWidget.cs. Theme: `var(--unity-colors-button-background-pressed)`, `--unity-colors-highlight-*`, `--unity-colors-label-text`, `--unity-colors-error-text`, etc. Plugin version 0.5.0→0.6.0.
- **Per-Tool Permission Gating in Agent Chat** (2026-06-03) — New Perms control in the chat footer opens a per-tool allow/deny popup (foldout per catalog category, Allow/Deny-All). Denied tools are withheld from the agent by enumerating only the allowed tool ids via `--allowedTools`; the default stays allow-all so existing behavior is unchanged (empty deny-set → compact `mcp__unity` blanket, not 88 enumerated ids). Per-tool ids use the live MCP server-key prefix `mcp__unity__` (matches ~/.claude/mcp.json key `unity`); blanket + per-tool prefix derive from one shared const so they can't drift. Deny-set persisted in EditorPrefs; catalog read live (incl. plugin tools) so newly added tools auto-allow. New: PermissionConfig + MCPChatWindow.Permissions partial; ClaudeArgBuilder gains an allowed-tools enumeration path. Tests: PermissionConfigTests (15) + ClaudeArgBuilderTests (13). Plugin version 0.5.0→0.6.0.
- **Chat Fixes: Verb-Label Prefix + Composer Anchoring + Enter Dedup + Themed Permissions Popup** (2026-06-03) — Four follow-up fixes within the v0.6.0 chat wave. (1) ToolVerbMap humanized labels used a stale `mcp__unity-mcp__` prefix that never matched live ids; all 20 keys now derive from the shared `PermissionConfig.MCP_TOOL_PREFIX` const so verb labels resolve and can't drift (drift-guard NUnit test added). (2) Composer now hugs the footer — the input area was given a min-height *floor* while its height was cleared, so `.chat-input` flex-grow had no definite parent size and the surplus became a dead gap; UpdateAutoHeight + ResetInputAreaHeight now set a definite height and clear min-height. (3) Enter sends without leaking a newline — Unity fires up to two KeyDownEvents per press (keyCode=Return, then character='\n') and the echo slipped past the keyCode-only check, sometimes inserting a stray newline after the field was cleared; new pure `EnterKeyLogic.DecideEnter`/`IsEnterChar` plus a dedup flag in EnterKeySend suppress every Enter event and act exactly once (Alt+Enter still inserts one newline), caret reset to 0 on send. (4) Tool Permissions popup restyled to match the Settings window via new tri-state `PermCategoryGroup` (reads/writes through PermissionConfig) + search field, reusing MCPSettings.uss classes through LoadStyleSheet. Tests: +8 pure tests (DecideEnter truth-table + IsEnterChar edges). No version bump (within v0.6.0).

## [v0.5.0] — 2026-06-03

- **Chat UX Polish Pass 2: Tool Grouping + Interactive Refs + Mermaid Layout Fix + Horizontal Scroll** (2026-06-03) — Tool-call chips grouped by ID (stop scatter per event), copyable text (Labels selectable via mouse-drag), interactive scene/script refs (syntax: `obj:/Path/To/Obj` and `script:Assets/MyScript.cs`); ChatRefResolver + ChatRefAction (click-navigate, Alt+click "Add to Context"). Mermaid layout distortion fixed: node width dynamic via MeasureNode (text lines + char width + bounds), eliminates hardcoded 120px. Chat horizontal scroll fixed (ScrollViewMode.Vertical, ScrollerVisibility.Hidden); FlowBar sweep indicator (800ms tick, visual progress). Markdown `<br/>` normalization in MarkdownInline. Input field auto-height (InputHeightCalc, height clamped min=96px max=200px via schedule); drag-drop reflow works now. New files: ChatActivityState, ChatLabel, ChatRefAction, ChatRefResolver, CopyTextBuilder, CopyableText, InputHeightCalc, JsonArrayScan. Modified Chat infrastructure: EntryKeySend rewrite (simplify), ClaudeArgBuilder adds `--disallowedTools AskUserQuestion` (prose-fallback for headless stream-json). JsonHelper gets ExtractFirstArrayObject (parse streaming tool results). NUnit tests: 17 suites / ~196 cases (render + backend + new interactivity), both Chat DLLs compile clean. Plugin version 0.4.0→0.5.0.

## [v0.4.0] — 2026-06-03

- **Extensible Chat Render Subsystem** (2026-06-03) — Markdown + native Mermaid flowcharts + inline images + Enter-to-send/removable chips. Registry seam (1 file + 1 line to add new renderers). Markdown: MarkdownParser→blocks, MarkdownInline rich-text (escape `<>` first, protect code spans), MarkdownBlockRenderer + Table/List partials, ImageBlockRenderer with texture lifecycle. Mermaid: MermaidParser (graph TD/LR/RL/BT, nodes rect/round/diamond, edges with labels, chained + self-loops), MermaidLayout (Kahn topo + longest-path, no Vector2), MermaidView (absolute nodes + edge overlay), MermaidEdgePainter (Painter2D + arrowheads, 2021.3-safe). Streaming→finalize strategy: accumulate raw text, re-render on TurnDone. Enter/Alt+Enter logic pure-testable. MCPChatWindow.uss +156 lines (md-*/mermaid-*/chip-✕ classes, house palette). 62 EditMode NUnit tests (MdBlock, MarkdownParser, MarkdownInline, MermaidParser, MermaidLayout, EnterKeySend) green. Version 0.3.0→0.4.0.
- **Editor Chrome Flattened: Menu + Status-Bar Widget** (2026-06-03) — Flattened "Tools/Unity MCP" menu → top-level "MCP/" (priority 0=Chat, 1=Status, 2=Settings). New MCPStatusBarWidget: injects status pill into Editor AppStatusBar via reflection + scheduled pulses (breathing animation). Extracted MCPActions class (Restart, Kill, Reimport) — shared by status window + widget. MCPStatusModel: pure state logic (no deps), maps (isRunning, isClientConnected) → display values (Down/Listen/Up states, labels, pill text). New Tests asmdef + MCPStatusModelTests (17 NUnit tests, all scenarios covered). MCPStatusWindow refactored to use MCPStatusModel + MCPActions (DRY).

## [v0.3.0] — 2026-06-03

- **Optional In-Unity Agent Chat Window** (2026-06-03) — New `MCPChatWindow` EditorWindow spawns the user's local `claude` CLI in headless stream-json mode; the CLI runs the existing `unity_mcp.server` as its MCP backend, reusing ~90 tools with zero new tool code. Isolated behind `UNITY_MCP_CHAT` scripting define in `UnityMCP.Editor.Chat.asmdef` (one-way reference to core via `InternalsVisibleTo` + `ChatSettingsHook` event). OFF by default; deleting `Chat/` folder leaves core untouched. Features: drag-drop object chips (with PingObject on click), screenshot attach (MultiView), Ask/Agent mode toggle, humanized tool card rendering, orphan-process cleanup on domain reload. Module: `ChatStreamParser` (stream-json→ChatEvent), `ClaudeArgBuilder` (--mcp-config generation), `ClaudeBackend` (Process lifecycle), `IChatBackend` abstraction (future plugin seams). macOS PATH resolution: spawn via `/bin/zsh -lc 'claude ...'` to inherit user shell config. JSON-only-at-boundaries principle (stdin/stdout/--mcp-config/--permission-mode; internal models plain C# structs + text). 4 NUnit suites for pure-logic testing. Plugin versions: 0.2.6→0.3.0, server 0.1.19→0.2.0.
- **Status Window UIToolkit Rewrite** (2026-06-02) — MCPStatusWindow IMGUI→UIToolkit migration with breathing heartbeat pulsation. `CreateGUI()` builds centered status orb (`.orb` solid disk + `.orb-halo` ring with USS class-driven pulsation). State polling every 700ms: ECG beat `Every(900)` when connected (green), gentle beat `Every(1500)` when listening (amber), flatline when stopped (red). USS transitions (border-*-width + opacity + background-color longhand) — no @keyframes, no transform, no box-shadow (2021.3-safe). Theme matches MCPSettings.uss (bg #1a1a2e, accent #e94560, btn #2a2a3e/#3a3a5e). New file `MCPStatus.uss` (112 lines). Extracted `MCPEditorUtils.LoadStyleSheet(filename)` helper (two-path package lookup, re-exported). `MCPSettingsUI.cs` delegates to `LoadStyleSheet("MCPSettings.uss")` (DRY; behavior identical). Buttons unchanged: Restart/Kill MCP/Reimport. Schedules auto-stop on window close.

## [v0.2.6] — 2026-06-02

- **Wave 3: Tool-Gating Fix + Settings UI** (2026-06-02) — APPROVED. P0+P1 shipped (2026-06-02, versions 0.1.19 + 0.2.6). P0 (Tool-Gating Fix): Fixed P1-regression where Unity form checkboxes saved zero tokens — `_filter_tools` kept any tool where `is_visible(name)` (true for all TIER1 ≈ every tool). Now: (1) Unity reports `get_disabled_tools` CSV, (2) Python `_filter_tools` applies tier/session gating THEN hides exactly that disabled set EXCEPT `FORCE_VISIBLE` escape hatches (discover_tools, get_enabled_tools, do, ask, editor, get_console, get_compile_errors, reconnect_unity, list_connections), (3) approach is "hide-disabled-set" NOT allowlist (Python-only tools absent from Unity CSV, would be wrongly hidden). `_disabled_tools_cache` refreshes on connect/reconnect; cache=None ⇒ gating-only fallback. Removed old `_enabled_tools_cache` side-channel. P1 (Python-Authoritative Catalog + UIToolkit Settings): `gating.get_catalog()` single source of truth (themed taxonomy: CORE locked, SCENE_EDIT, COMPONENTS, ANIMATION, SHADERS_MATERIAL, VFX, UI, SCREENSHOTS, UNIT_TESTS, RUNTIME, ASSETS, ADVANCED_CODE, SESSION_SKILLS, CONNECTION, META). Public tools only — plugin tools categorized dynamically Unity-side. `_push_catalog` sends catalog to Unity on connect/reconnect via `set_tool_catalog` (TCP-only, not in LLM context). Unity persists to EditorPref `UnityMCP_Catalog`. C#: `MCPSettings.cs` rewritten as UIToolkit (`CreateGUI`): foldout groups, tri-state group masters, search bar, presets (Minimal/Full/No-visuals), CORE locked, separate dynamic Plugins section (from `PluginRegistry`), animated `.uss` header. New files: `CatalogParser.cs` (deserialize JSON→dict), `MCPSettingsUI.cs` (foldout builder), `MCPSettingsCategoryGroup.cs` (tri-state logic), `MCPSettings.uss` (styling). `ExecGetDisabledTools` mirrors `ExecGetEnabledTools`, both in `IsAlwaysAllowed` + `IsAllowedDuringCompile`. EditorPref keys consolidated: `KeyPrefix` + `KeyAutoDiscard`. Tests: `pytest -m "not live"` = **1588 passed** (new test_catalog.py = 19 tests, drift-guard via `fn.__module__` public/external split). Versions: `server/pyproject.toml` 0.1.17→0.1.19, `unity-plugin/package.json` 0.2.4→0.2.6.

## Earlier history

### Wave 2: Architecture (2026-06-02)

APPROVED. Modular refactoring (6 commits, zero behavioral changes). F14: Extract `PathResolverMixin` from `middleware.py` (1104→945 lines) into new `middleware_paths.py` (168 lines); methods moved verbatim: `update_path_cache`, `validate_path`, `resolve_path`, `_get_disambig`, `resolve_path_live`, `find_from_cache`, `_levenshtein` (re-exported from middleware.py for schema_guard compat). F19 (C#): Split `PlaytestRunner.cs` (559→257 lines) via partial class; `ExecuteStep` (300-line 21-case dispatch) moved to `PlaytestRunner.Steps.cs` (313 lines). IL-identical, zero behavior risk. F19 (Python): Split `advanced.py` (351 lines, 22 unrelated tools) into 5 cohesive modules: `batch.py` (batch, references, validate_references + `_dsl_tools` set), `codegen.py` (execute_code, get_schema, auto_fix, smart_build), `skills.py` (save/use/list_skill, apply/save/list_template + `_skills_dir`), `spatial.py` (validate_layout, get_spatial_context, scan_scene, check_colliders, spatial_query), `ui.py` (create_ui, set_rect, menu, shader). `advanced.py` deleted. `server.py` re-exports all 22 names for back-compat; `plugin_api._dsl_tools` repointed to `batch`. CATEGORIES["advanced"] string-decoupled from module names. F15: Split `CommandRouter.cs` god-file into partial classes (CommandRouter.cs + CommandRouter.ObjectHandlers.cs + CommandRouter.MediaHandlers.cs). F06: Trimmed verbose TIER1 tool descriptions (screenshot, find_references, compile_preflight, semantic_at) for token savings; kept all enum values + run_playtest DSL grammar (anti-hallucination). New test `test_tool_descriptions.py` locks char budgets + required substrings. F07: `fields=` projection on get_component/inspect (already shipped). Python 1565 passed (all tests green). C# EditMode 754 passed + 2 pre-existing failures (same as Wave 1).

### Wave 1: Review Hardening (2026-06-02)

APPROVED. Adversarial code review of Wave 0 fixes found 15 confirmed issues; all resolved. F16 (error-dedup gate): fixed protocol_err gating (was whole-body substring scan firing on SUCCESS payloads), fixed dedup_error key collision (was [:80] → full message), bounded _error_dedup LRU (256 entries), gate LessonRecorder.record on same protocol_err flag (not raw_ok). F17 (path cache poison): no longer write negative-path cache on search_scene TCP raise, clear negative-path cache on any WRITE_CMDS command. F05 (DRY): hoist duplicated _read_cacheable to module-level _READ_CACHEABLE frozenset. F11 (nested batch): BatchHelper._batchDepth int counter (was bool), Physics.Sync fires once at outermost exit (--_batchDepth == 0). Python 1548 passed (+8 behavioral regression tests). C# EditMode 756 tests, 754 passed (2 pre-existing failures unrelated to fixes). New NUnit test NestedBatch_KeepsInBatch_For_OuterTail passes (gitignored unity-test-project).

### Wave 0: Performance Pass (2026-06-01)

APPROVED_WITH_MINOR. Quick wins (4h): F02 `QueuePlayerLoopUpdate()` after enqueue (500-12500ms/sess latency win), F13 float serialization `"G"`→`"G4"` (300-600 tokens), F18 MultiViewCapture reflection cache (2-8ms/call), F04 `mark_recompile_issued()` wiring (cosmetic), F08 `strip_defaults` unconditional for reads (1000-2000 tokens, _no_strip escape hatch), F12 confidence suffix gate <0.5 + staleness-gated AUTO STATE injection (1150 tokens). F03-ttl PrefetchCache TTL 0.5→12.0s (10-150ms win). Python pytest 1524 pass; C# EditMode 749/751 (2 pre-existing failures in Revert_RevertsChanges, ValueParser_Enum_NegativeInt unrelated to Wave 0). See audit: AI/performance-audit-2026-06.md. v0.1.10 (Python), v0.2.0 (C#).

### Cycle 20: 20-Architect Audit (2026-05-31)

APPROVED_WITH_MINOR. Security: CodeExecutor blocklist (Reflection.Emit, DynamicMethod, Activator, Expression). Server: lock lifecycle try/finally, fail-fast, reconnect callbacks via ConnectionSlot, global declaration fix. Middleware: CircuitBreaker HALF_OPEN probe, reset_session completeness, component cache + schema_cache guards. Intent: sanitize target, retry format, validate retry commands. Serialization: locale-invariant floats. Animation: multi-layer, Vec3 AddKeys merge, RemoveKey axis. ValueParser: null/enum/ID checks. Other: Undo registration, plugin isolation, concurrent playtest guard, ASSERT_BATCH END, compile error clearing, screenshot cleanup, cellSize clamp, autobatch parent paths, bridge exception chain, editor tool annotation. Docs: architecture.md DSL commands fix (21 actual vs 11 listed), mcp-server.md TCP keepalive/cooldown values corrected. v0.1.9.

### Post-Launch Audit (2026-05-31)

40-agent audit fixes: README rewritten, plugin docs (quickstart + API reference) polished, DRY cleanup in animation/advanced/scene tools, capability gating test added, ObjectManager/ErrorHelper/ComponentSerializer C# hardening, removed unity-test-project from public repo.

### Open-Source Migration (2026-05-30)

Created modular plugin architecture: C# (IMCPPlugin + PluginRegistry) and Python (3-source loader: pkgutil, entry_points, UNITY_MCP_PLUGIN_DIRS). Plugin API facade (plugin_api.py) provides stable exports. Generalized test fixtures (GridPlayer). 1475 Python tests passing. All plugin tools now load dynamically via external packages.

### Phase History

- Phase 0: TCP skeleton + binary framing + MCP server
- Phase 1+2: Scene reading (Hierarchy, Components) + Object CRUD + Undo/Redo
- Phase 3: Diagnostics (Console, Screenshot)
- Phase 4: TCP reconnection with exponential backoff
- Phase 5: Advanced features (get_object_detail, run_tests, MCPSettings)
- Phase 6: Scene management (new_scene, save_scene, auto-discard on quit)
- Phase 7: Animation support (get/create/edit/preview animation clips)
- Phase 8: Timeline support (get/create/edit/preview timeline assets)
- Phase 9: Scene search (search_scene with Unity-style query syntax)
- Phase 10-11: Batch commands (text-based format, execute multiple ops in one call, 80-95% token savings)
- Phase 12: Quick wins (contextual errors, hierarchy safety caps, compilation retry hint, prefab-aware, tool visibility)
- Phase 13: Reference analysis (get_references, find_references_to, remap_references + ObjectReference support)
- Phase 14: Token Optimization Sprint (tool consolidation 32→18, auto-include mutations, instructional errors v2, Python DRY helper, steering descriptions, modal state guards, editor control, tool annotations, port env var)
- Phase 15: File-Based Output (TEXT_THRESHOLD=80KB, auto-file for large text + screenshots, FileOutputHelper, Temp/MCP directory cleanup)
- Phase 16: Animator Controller + Sub-Action Flattening (consolidated animator tool with 6 actions, fixed animation/timeline edit sub-action routing)
- Phase 17: Particle System (consolidated particle tool with 4 actions, 11 modules, 10 presets)
- Phase 18: Particle System Test Coverage (8 Python + 8 C# scenario tests)
- Phase 19: Physics Test Coverage (20 Python + 17 C# tests for Rigidbody, colliders, joints, CharacterController)
- Phase 20: Shader Management (consolidated shader tool with 7 actions; ShaderSerializer + ShaderHelper + ShaderGraphHelper; 22 Python + 41 C# tests)
- Phase 21: Code Refactoring (DRY consolidation: AssetHelper + ParseFloats + conftest.py fixtures + ToolError + graceful startup; -706 lines)
- Phase 22: Live Test Verification (full verification of all 20 MCP tools across 18 scenarios; JsonHelper consolidation)
- Phase 23: Dynamic Tool Filtering (monkey-patch mcp.list_tools to query Unity's get_enabled_tools; 4-level fallback)
- Phase 24: Efficiency — Batch-First (skill file + batch description + inspect compound tool)
- Phase 25: New Features + Plugin System (compress_hierarchy, set_active, wire_event, validate_references, checkpoint, prefab instantiation, plugin system with auto-discovery)
- UnityEvent Reading + Wire Fix (ComponentSerializer now expands UnityEvent fields, wire_event validation fixed)
- Phase 26: Asset Pipeline & Project Tools (5 new tools: asset, project_settings, material, prefab, scriptable_object)
- Phase 27: Architecture Stabilization (ValueParser DRY extraction, CommandRegistry pattern, JsonHelper resilience, dead code cleanup)
- Phase 28: Multi-Unity Connection (BridgeManager, 6 new MCP tools: connect/disconnect/switch/list/transfer/copy)
- Phase 29: Architecture Cleanup (CommandRouter RegisterAll(), server.py split into 6 tool modules, _resolve_name fix)
- Phase 30: PID Lockfile for Zombie Prevention (lockfile.py with fcntl.flock + signal-based process cleanup)
- Phase 31a: Runtime Play Mode Control (RuntimeHelper, invoke_method, set_runtime_property, wait_until)
- Phase 31b: Optimization Sprint (BatchHelper per-command guards, query_state + test_step + move_to tools, validate_layout)
- Phase 31c: PlaytestRunner DSL (9 DSL commands, PlaytestConfig, PlaytestParser, PlaytestRunner, run_playtest MCP tool)
- Phase 32: Stability & Token Optimization (set_property read-back, hierarchy summary mode, InputNormalizer)
- Phase 31d: Runtime Validation DSL (16 new DSL commands, PlaytestState, IPlaytestSimulator, adaptive reports)
- Phase 33: Killer Features (Scene Refs, Capability Gating, MCP Resources, execute_code Roslyn, multi-view screenshot, visual regression, spatial queries, skill library, middleware 12 features, session save/load)
- Phase 34: SamplingService + Token/Perf Polish (gating ON by default, SamplingService for visual verification, 12 production fixes)
- Phase 35: Telemetry/Metrics System (MetricsRegistry, counters/observations/cost tracking, get_metrics TIER1 tool)
- Tier 2b: Cost Budget + Adaptive Routing (persistent daily budget, 13 features registry, 4-tier adaptive gate)
- Tier 2c: Set-of-Mark Visual Annotation (SoM layer for VLM grounding, Pillow overlays, hash-stable indices)
- Tier 2d: Asymmetric Reflection (server-side self-verification, registry pattern, 3 rule modules)
- Tier 2e: Graceful Degradation (unified fallback ladder, degrade.py, 3 production callers)
- Tier 2f: Discoverability/ToolHinter (6 hardcoded patterns, sliding deque history, adoption tracking)
- Tier 2g: Live Integration Tests (opt-in real-Unity suite, session-scoped PlayMode, GridTest scene)
- Cycle 6a: Recompile Resilience (CompileStateProbe, bridge retry contract, exponential backoff)
- Cycle 6b: Type Conversion Bundle (AnimatorController == parsing, ValueParser enum int fallback, InputNormalizer Python)
- Cycle 6c: Path/UX Bundle (search_scene empty-result context, delete_object accepts path, Physics.SyncTransforms)
- Cycle 6d: Component Edge Cases (empty serialize sentinel, duplicate add prevention, explicit response format)
- Cycle 6e: Audit & Cleanup (audit-only, marked 3 problems RESOLVED + 1 N/A)
- Cycle 7a: Resilience Bundle (sticky retry-cache fix, 3-tier timeout, ECONNREFUSED fast-fail, reconnect callbacks)
- Cycle 7b: TCP/OS Hardening (per-socket TCP options, lockfile PID-recycle defense, BridgeManager.close_all bounded)
- Test Cycle 1: Hygiene + Perf (62s → 10.14s, 6x speedup, 3 autouse fixtures)
- Test Cycle 2: DRY + Coverage (bridge_response factory, conftest.py dedup, hinter split)
- Test Cycle 3: Independent Live Tests + Resettable Collectibles (GridPlayer state reset, 52 live tests)
- Test Cycle 3a: Prophylactic Invariants (determinism, subprocess lifecycle, lifespan cleanup)
- Test Cycle 4a: Visual Pipeline Real Bugs (8 P0 production bugs found and fixed)
- Cycle 4b: Visual Pipeline Foundation (Haiku output normalizer, SoM index stability)
- Cycle 4c: visual_diff Polish + Critical Journeys (DIFF_PROMPTS golden test, pixel_threshold boundary)
- Cycle 4d: Budget Concurrency Hardening (asyncio.Lock, per-PID tmp, fcntl serialization)
- Cycle 4e: Live Tests Rewrite (Vacuous → Fixture-Based)
- Cycle 5a: Heuristic Performance (PrefetchCache, HierarchyDiff, Disambiguator, CoalescingBuffer)
- Cycle 5b: Response Distiller + Preimage Cache (ResponseDistiller, PrefetchCache.put_synthetic, _recent_focus)
- Cycle 5c: Roslyn Foundation (find_references, compile_preflight, semantic_at — 3 new TIER1 tools)
- Cycle 5d: Wire Dead Modules (Disambiguator + Distiller Haiku wiring)
- Cycle 10: Multi-View Anti-Hallucination (visibility manifest + colored bounding-box overlays)
- Cycle 11: Stability Protocol — State File + Adaptive Circuit Breaker
- Cycle 12: MCP Stability Fixes — Reconnect Success Rate 88%
- Cycle 13: TCP Client Race + Shutdown Guard (per-client CancellationToken, atomic SendAsync)
- Cycle 13 Phase B: Crash Detection & PID Liveness
- Cycle 13 Simplification: TCP Layer Revert (going_away ordering, keepalive reverted)
- Cycle 14: Multi-Process Stability — Exclusive Lockfile + Heartbeat Probe
- Cycle 15: Reconnect Regression Hardening (auto-reconnect 88% → 98%+)
- Cycle 16: Reference Fixes + Type Support + unwire_event + PlayMode Test Persistence
- Cycle 16b: Domain Reload TCP Self-Healing (bind retry, watchdog, state file)
- TCP Connection Lifecycle Hardening (CLOSE_WAIT fix, reconnect race fix)
- feat: set_parent tool (fixes duplication bug)
