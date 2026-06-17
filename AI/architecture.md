# Feature: Architecture Overview

## Overview

MCP-сервер для управления Unity Editor из Claude Code с минимизацией токенов (10-15x сжатие vs JSON).

## Architecture (для Architect)

```
Claude Code ←──stdio──→ Python MCP Server ←──TCP:PORT[+CHAT]──→ Unity Editor Plugin
     │                        │                                  │
     │  MCP Protocol          │  Binary protocol                │  Unity API
     │  (JSON-RPC 2.0)        │  [4B len BE][JSON]              │  (main thread)
     │                        │                                  │
     │                        ├─ ConnectionSlot (dual: CLI+Chat) ├─ CommandRouter (async)
     │                        ├─ Capability Gating (TIER1+cat)   ├─ PluginRegistry (IMCPPlugin)
     │                        ├─ Plugin system (auto-discovery)  ├─ CommandRegistry + ValueParser
     │                        │  - opt-in disable: env UNITY_MCP_SKIP_PLUGINS=prefix ├─ CommandSchema (validation)
     │                        ├─ Deferred Schema Loading         ├─ 7 Serializers
     │                        │  (stub schemas + lazy resolve)   ├─ RefManager ($a-$zz)
     │                        ├─ 23-layer Middleware (opt-in)    ├─ PlaytestRunner + DSL
     │                        ├─ CompileStateProbe               ├─ RuntimeHelper (Play Mode)
     │                        ├─ PID Lockfile (exclusive)        ├─ MultiViewCapture (4-panel)
     │                        ├─ Port discovery (CWD-based)      ├─ CodeExecutor (Roslyn)
     │                        └─ Heartbeat (15s, reconnect)      ├─ PortResolver (dual-port)
     │                                                           └─ Guards (compile/play/runtime/tool)
```

### Почему такая архитектура

- **Python MCP**: Claude Code запускает через stdio, зрелый SDK
- **TCP socket**: Переживает domain reload Unity (vs WebSocket)
- **Binary framing**: 4 байта длины BE + JSON, минимальный overhead
- **No cache**: All calls go directly to Unity via bridge.send (scene changes too frequently)

### Components

1. **MCP Server** (Python: 80+ modules total, including `server.py`, 23 tools modules + support)
   - **89 core MCP tools registered**. Gating: TIER1=38 core (hardcoded). External plugins can add more tools dynamically.
   - **CodeExecutor.SecurityScan (v0.31.0)**: Hardened pipeline — (1) strip C# comments via regex (2) whitespace densification (3) OrdinalIgnoreCase matching (4) 11 new blocked patterns (EditorApplication.Exit, Application.Quit, Environment.FailFast, ExportPackage, ImportPackage, OpenProject, ProjectWindowUtil, using-aliases for System.IO/Diagnostics/Net/Reflection)
   - **In-Unity Chat Backends** (v0.29.2+): Four CLI providers with auto-discovery via TypeCache:
     * **ClaudeBackend** — Claude CLI with --permission-prompt-tool, MCP elicitation, stream-json protocol
     * **CodexBackend** — Codex CLI (no permission prompts), experimentalApi: true, tool/requestUserInput support
     * **GeminiBackend** (v0.30.1) — Gemini gcloud-cli with .gemini/settings.json smart-merge, stream-json 6-event protocol (init/message/tool_use/tool_result/error/result), filters prompt echo + internal tools (no --permission-prompt-tool support)
     * **KimiBackend** (v0.34.0) — Kimi K2 CLI with role-based NDJSON protocol (system→user→assistant), model autoconfig via ~/.kimi-code/models.json, binary resolver sources kimi PATH via zsh -lic
   - Transport: stdio (default) or streamable-http (`UNITY_MCP_TRANSPORT=http`)
   - FastMCP("UnityMCP", lifespan=lifespan)
   - Lifespan: auto-discover Unity port from `~/.unity-mcp/ports/*.port`, acquire exclusive PID lockfile, create ConnectionSlot, connect bridge, fetch disabled tools cache (`get_disabled_tools`), push Python-authoritative catalog (`_push_catalog`), start heartbeat, register reconnect callbacks, load_plugins()
   - **MCP SDK Version (v0.31.0)**: Pinned `mcp>=1.27.1,<2` — v2.0 ships 2026-07-28 with breaking changes (e.g., `response.content` structure). Upper bound prevents silent breakage.
   - Plugin system (3-source discovery: pkgutil built-in, entry_points, UNITY_MCP_PLUGIN_DIRS env): each plugin has `register(mcp, send_fn, args_fn)`. UNITY_MCP_SKIP_PLUGINS env (comma-separated prefixes) skips matching plugins.
   - _send() helper: sends to bridge via slot, raises ToolError on !ok
   - File-based output: checks `file` field in response → returns path string
   - Tool annotations: readOnlyHint, destructiveHint for MCP compliance
   - Dynamic tool filtering: patches `mcp._mcp_server.request_handlers[ListToolsRequest]` with gating + disabled-set subtraction (hide-disabled-set model, not allowlist)

2. **TCP Bridge** (Python: `bridge.py` + `connection_slot.py` + `lockfile.py` + `compile_state.py` + `server_filtering.py`)
   - **ConnectionSlot**: dual per-project connections (CLI main + Chat agent-only) with project-based discovery
   - **Port Discovery** (`server_filtering.py:read_unity_port`, v0.23.0): CWD-based project matching → ~/.unity-mcp/ports/*.port files → env UNITY_MCP_PORT → default 9500. **v0.23.0: TCP probe** filters stale discovery files (port written but not listening). Candidates ranked by project path match (CWD), then mtime. PermissionError (cross-user processes) skipped gracefully, live .port files preserved.
   - **Fail-Fast Lockfile** (`lockfile.py`): RuntimeError raised on live process (instead of SIGTERM) to let Python server handle reconnection logic cleanly. **v0.23.0: Zombie detection** — `_is_zombie(pid)` check prevents treating defunct processes as "live", allowing fast server startup without waiting for cleanup.
   - **UnityBridge**: AsyncIO TCP client, 4-byte BE length prefix JSON
   - Socket: TCP_NODELAY, SO_KEEPALIVE (idle=60s, interval=10s, count=3 on macOS/Linux)
   - **Heartbeat**: 15s interval, raw ping, 3 consecutive failures → close, 2s polling when disconnected (5s when busy). Sole reconnect mechanism.
   - **Port Re-Discovery on Reconnect (v0.24.1)** — `UnityBridge` accepts optional `port_discoverer` callable (typically `read_unity_port`), invoked during `_reconnect()` before TCP connect to detect if Unity moved to a new port. If discoverer returns different port, bridge updates `_port` and recreates CompileStateProbe. Gracefully handles discoverer exceptions (falls back to current port). ConnectionSlot threads discoverer through and adds `_sync_port()` callback to sync port back to slot + trigger server-side lockfile swap (`_on_port_change`). Backward-compatible: no discoverer → normal reconnect.
   - **CompileStateProbe**: heuristic compile/domain-reload detector (state file, PID check)
   - **DomainReloadError**: on Unity `going_away` event → immediate close + busy flag
   - **PID Lockfile**: `~/.unity-mcp/server-{port}.lock`, **cross-platform locking**:
     * **macOS/Linux**: `fcntl.flock` (advisory, whole-file lock)
     * **Windows**: `msvcrt.locking` on sentinel byte at offset 1024 (non-blocking, avoids mandatory lock of PID data at bytes 0-31)
     * Kills stale servers: SIGTERM→SIGKILL (Unix), TerminateProcess (Windows)
     * **v0.23.0: Zombie detection** via `_is_zombie()` prevents stale defunct processes from blocking reconnection
   - **SIGPIPE handling**: guarded with `hasattr(signal, "SIGPIPE")` since Windows lacks SIGPIPE. Suppressed on Unix to prevent server crash on client disconnect.
   - **Reconnect (v0.30.3)**: cooldown MIN_RECONNECT_INTERVAL=5s (was 2s), heartbeat debounce=30s (was 5s). send() reconnect no longer fires callbacks (only heartbeat does) — breaks reconnect feedback loop. push_catalog skips if already locked.
   - Max message: 10MB, timeouts: 30s default, 60s compile_preflight/batch, 120s run_tests/run_playtest/fuzz_playtest

3. **Unity Plugin** (C#: 130+ files, ~14000 LOC)
   - **MCPServer.cs**: Dual TCP listeners (main port 9500-9599 + chat port auto-assigned, separate), 4-byte BE framing, 10MB max, SO_KEEPALIVE, **v0.23.0: SO_REUSEPORT** (macOS/Linux) for rapid reconnect recovery, auto-assigns free ports via `PortResolver.FindFreePort()`, persists to Library/MCP_Port.json, state file (`ready`/`compiling`/`reloading`), `going_away` event before domain reload, ClientSlot pattern isolates CLI and Chat connections
   - **PortResolver.cs**: Pure testable helpers (ResolvePort, ResolveChatPort, FindFreePort, SavePorts, IsValidPort, ParsePortFromJson) with 25 NUnit tests. Validates 1024–65535 range, skips reserved ports, fallback to OS-assigned via port 0
   - **CommandRouter.cs**: RegisterAll() → calls core commands + PluginRegistry.RegisterAllPlugins() for external plugins, data-driven IsMutatingCommand/IsRuntimeCommand
   - **PluginRegistry.cs**: Static registry for IMCPPlugin implementations. Plugins register via `[InitializeOnLoad]`. One-way asmdef dependency: external → public.
   - **IMCPPlugin.cs**: Interface — Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands
   - **CommandRegistry.cs**: Func<string,string> handlers, mutating + runtime flags
   - **CommandSchema.cs**: parameter validation with fuzzy did-you-mean suggestions (79 schemas)
   - **ValueParser.cs**: vectors, quaternions, colors, arrays, 100+ types (Rect/Bounds/RectInt/BoundsInt/LayerMask + Int64/Double precision), type-aware SetPropertyValue
   - **InputNormalizer.cs**: component/property/value normalization
   - **BatchHelper.cs**: multi-command text parser + executor (on_error=continue/stop)
   - **7 Serializers**: HierarchySerializer (tree, MAX_NODES=3000, incremental, summary), ComponentSerializer (key-value, UnityEvent expansion, PrefabStage-aware, **v0.23.0: #instanceID in all path tools**), AnimationSerializer, TimelineSerializer, AnimatorControllerSerializer, ParticleSerializer, ShaderSerializer
   - **ScenePathParser (v0.31.0)**: Shared struct for multi-scene path parsing (`"SceneName:/"` prefix extraction). Used by SceneObjectFinder + ComponentSerializer.Finder. Replaces inline string parsing, prevents multi-scene reference bugs.
   - **ObjectManager (v0.23.0 fixes)**: Properties.cs auto-redirects `set_property("active")` to SetActive. Lookup.cs adds FindType + short-name fallback for custom components.
   - **FileOutputHelper (v0.23.0)**: ScreenshotsDir now `<ProjectRoot>/ScreenShots/` (project-local, not shared cache)
   - **RefManager**: short refs $a-$zz (702 slots), invalidated on scene change
   - **ErrorHelper**: contextual errors with did-you-mean hints

4. **Guards (C#)**
   - **Compile guard**: blocks all except ping, get_version, get_console, screenshot, get_enabled_tools, compile_status
   - **Play Mode guard**: blocks mutating commands (changes would be lost)
   - **Runtime guard**: runtime commands blocked outside Play Mode
   - **Tool enable guard**: MCPSettings per-tool toggle (ping/get_version/get_enabled_tools always allowed)
   - **Fast-path commands** (bypass main thread): ping, get_version, status, get_enabled_tools

5. **Per-command timeouts (C#)**: run_tests=130s, run_playtest=130s, batch=65s, wait_until/move_to/test_step=30s, default=25s

**run_tests Fire-and-Forget (v0.32.0)** — `run_tests(mode="EditMode"|"PlayMode", filter=None)` returns immediately with message `"tests-started|{mode}|poll get_test_results every 5s for up to 2min"`. Does NOT poll internally. User/caller must poll `get_test_results()` externally. **Why:** avoids TCP blocking on domain reload (Editor.log clears "compiling" status before port 9700 restored). Initial send() uses short 8s timeout (fire-and-forget). If `DomainReloadError` caught, returns immediately. Bridge resilience: when `DomainReloadError` occurs, pins `domain_reload_in_progress=True` for all subsequent retries within that send() call, preventing `_probe_busy()` from returning False too early. `get_test_results` allowed during compile (added to CommandRouter.IsAllowedDuringCompile, v0.31.1 P1 fix). **External polling pattern:**
```python
result = await run_tests(mode="EditMode")  # → "tests-started|EditMode|..."
# Now poll externally:
import asyncio
for _ in range(24):  # 2min @ 5s intervals  
    await asyncio.sleep(5)
    result = await get_test_results()
    if result not in ("pending", "none"):
        return result
```

6. **Post-mutation features**: console error capture, SuggestNext (recommends verification tool), auto-return parent subtree after create/delete

7. **In-Unity Chat Session Control (v0.19.0, F20–F30)**
   - **Stop button (F20)**: CancelTurn() method in MCPChatWindow + backend handlers. Sends `{ "stop_reason": "end_turn" }` to Claude stdin or terminates Codex process. Esc hotkey also triggers cancel. Button UI swaps from Send→Stop during streaming.
   - **Transcript reload survival (F21)**: TranscriptSerializer.cs persists chat history to plain-text format at Library/MCP_ChatTranscript.txt (alongside PendingTurnState). On domain reload, history restored, preserving user/assistant/tool-call entries + styling. `_entries` tracking + SessionState persistence gate.
   - **Settings persistence (F22–F24)**: AutoScroll toggle persisted in EditorPrefs, dropdown selections (Backend, Model) cached, all restore on domain reload / window reopen.
   - **Chip correctness (F24–F26)**: @Object duplicate fix via global forward search instead of narrow offset window. Direct Clear dialog (no submenu). Drag-drop MonoScript creates dual-chip (@Object + @Script).
   - **Domain reload trigger (F27)**: `_needsRefresh` flag set when code-editing tool result arrives; consumed in DrainAndRender to call `AssetDatabase.Refresh(ForceUpdate)` once per drain cycle. Ensures .cs edits via chat backend trigger recompilation.
   - **Backend simplification (F28)**: Removed spawn-per-turn CodexBackend. BackendKind now 2 entries (Claude, Codex). BackendKind.Codex always creates CodexAppServerBackend (persistent JSON-RPC sessions, matches Claude one-per-chat model).
   - **External drag/drop (F29)**: FolderChipProvider accepts files/folders from Finder. ProcessExternalPath() static method routes DragAndDrop.paths into chip context.
   - **Input height (F30)**: Default input field height 4 lines (CompactH=117f). Compute() clamps via minH=min(CompactH, maxH) to prevent degenerate clamp in tiny windows.

9. **Per-Backend Model Selection + Token Cost Display + Multi-Scene Chat Refs (v0.30.4, expanded v0.30.5)**
   - **Model Selector (Plugin v0.30.5)**: **MCPChatWindow.Selector.cs** with expanded presets per backend: **Claude** (Default, Fable 5, Opus 4.8/4.7/4.6, Sonnet 4.6, Haiku 4.5, Custom), **Codex** (Default, GPT-5.5, GPT-5.4/5.4-Mini, o3-pro, o3, o4-mini, GPT-4.1/4.1-Mini, Custom), **Gemini** (Default, 3.5 Flash, 3.1/3 Pro Preview, 3 Flash Preview, 2.5 Pro/Flash/Flash Lite, Custom). **ModelPresets.cs (NEW, v0.30.5)** extracted from BackendConfig: ModelPresetEntry, ModelPresetsConfig, ModelPresetDefaults.All (hardcoded fallbacks per BackendKind). **BackendConfigStore.GetPresetsForKind()** looks up Library/MCP_ChatBackendConfig.json ModelPresets field; not found → falls back to hardcoded defaults. **Result**: users can override model lists via config file without recompile. Dropdown rebuilt on backend switch. EditorPrefs persistence: `MCPChat.SelectedModel.{BackendKind}` (per-backend state). Custom model entry via text field. **Tests**: 44 new BackendConfigStoreTests (preset lookup, fallback, merge), 231 ModelSelectorTests (dropdown state, preset selection, custom entry, persistence across domain reload).
   - **Token Cost Display (Plugin)**: **TokenFormat.cs** extended with `FormatReadout()` → displays session cost (`$0.0020`) alongside token counts. Computes cost via `EstimatedCost(input_tokens, output_tokens)` with configurable $/1k rates (per backend). **Null-safe**: guards missing token data, avoids division-by-zero. **Tests**: 12 TokenFormatTests verify cost calculation, zero-token safety, missing data handling.
   - **Asset validate_move (Server v0.8.2)**: New `asset(action="validate_move", src="...", dst="...")` dry-run validation before asset move operations. Checks path existence, destination writability, conflict detection. Returns `{"ok":true}` or error details. Prevents silent failures on renames/refactors. **Tests**: 15 test_server_asset.py new scenarios.
   - **Multi-Scene Chat Reference Fix (Plugin + Server v0.8.2)**: Fixed scene-qualified object paths in chat. **IsAssetPath** now strict: returns false for "Scene:/" prefix (asset paths only "Assets/" prefix). **SceneObjectFinder** parses `"SceneName:/"` to extract scene name + path separately. Chips display `[Scene] name` for multi-scene objects. **Tests**: 74 MultiSceneChipTests (parsing, display, navigation).
   - **Ask↔Agent Session Persistence (Plugin)**: Switching backend mode preserves chat session via `--resume` flag. **SetMode.cs** captures `SessionId` on mode switch, passes to new backend launch. **Tests**: 120 SetModeTests (mode switch, persistence, restart).

12. **Plugin Extensibility API + Image Drag-Drop + Asset Viewers (v0.34.0)**
   - **Plugin Extensibility (Settings/Toolbar/Panels, CLI v0.34.0)**: New public seam interfaces for plugins to extend chat UI without core edits:
     * **ISettingsProvider**: Plugins register custom settings UI pages (e.g., `OnBuildUI()` returns VisualElement foldout, `SectionName`/`Priority`)
     * **IToolbarButtonProvider**: Plugins add toolbar buttons with click handlers and icon
     * **IPanelProvider**: Plugins register side panels (dock + overlay support)
     * **Registry classes**: `SettingsProviderRegistry`, `ToolbarButtonRegistry`, `PanelProviderRegistry` — all use `Register()` + discovery via `[InitializeOnLoad]` pattern
     * **MCPChatWindow hook points**: Settings foldout + toolbar + left/right panels all query registries on window open, render provider content dynamically
     * **Tests**: 72 PluginSettingsInjectionTests, 105 PluginToolbarButtonTests (button state, click handlers, lifecycle)
   
   - **Image Drag-Drop + Clipboard Paste (CLI v0.34.0)**:
     * **ClipboardImageReader.cs** (142 LOC): Platform-specific clipboard image read (macOS: NSPasteboard Foundation PInvoke, Windows: CF_DIB check stub, Linux: xclip subprocess). Returns PNG bytes or null, never throws.
     * **ImageAttachmentStore.cs** (96 LOC): Stores pasted/dropped images with temp file lifecycle. `AttachImage(bytes)` → saves to Library/.unitymcp_images/, returns relative path. `GetAttachedPaths()` → list of stored images. `Cleanup()` → removes stale files on session end.
     * **MCPChatWindow.ClipPaste.cs** (partial): Wires clipboard paste via Ctrl+V in input field. Detects image mime-type, attaches, emits chat event with image reference.
     * **MCPChatWindow.Chips.cs** (partial): DragAndDrop.paths routing — external files/folders (Finder drag) detected, filtered for images, attached same as paste.
     * **UserTurnBuilder.cs** extended: Embeds image references in user turn JSON as `image_url` blocks (Claude SDK protocol).
     * **Tests**: 37 ClipboardPasteTests (platform detection, mime-check, file write), 154 ImageDragDropTests (path filtering, attachment, multiple images), 76 UserTurnBuilderImageTests (turn JSON serialization with images)
   
   - **Inline Image Thumbnails in Chat (View v0.34.0)**:
     * **InlineImageThumbnail.cs** (70 LOC): Renders thumbnail strips in chat paragraphs (max 100px height, click→full viewer)
     * **MixedParagraphRenderer** extended: Detects `[img src="..."]` markdown, calls InlineImageThumbnail for rendering
     * **Tests**: 116 InlineImageThumbnailTests (sizing, fallback on missing image, click navigation)
   
   - **Prefab Preview Window (View v0.34.0)**:
     * **PrefabViewerWindow.cs** (151 LOC): EditorWindow displaying prefab 3D preview (camera orbit, zoom controls)
     * **PrefabPreviewLoader.cs** (82 LOC): Instantiates prefab in temporary scene, loads preview scene, destroys on close
     * **Wired**: Asset chip right-click "View" or MCPChatWindow chip click (via BuiltInChipProviders.ViewerLauncher seam) routes to PrefabViewerWindow.Open()
     * **Tests**: 198 PrefabViewerWindowTests (window lifecycle, prefab loading, camera controls, cleanup)
   
   - **3D Asset Viewers (View v0.34.0)**:
     * **AssetViewerFactory.cs** (83 LOC): Registry + factory for extensible media viewers. Wires WindowType → `IAssetViewer` implementations
     * **ModelViewerWindow.cs** (151 LOC): Displays .fbx/.obj/.blend/.dae models (instant load via import settings, camera orbit/zoom)
     * **SpriteViewerWindow.cs** (78 LOC): Displays sprite textures with grid overlay (100% zoom default, fit-to-window toggle)
     * **AudioViewerWindow.cs** (142 LOC): Plays audio clips (play/pause/loop, duration display, waveform placeholder)
     * **AudioUtilProxy.cs** (66 LOC): Wrapper for `AudioUtil.GetDurationInSamples()` (Editor-only API, reflection-based fallback for older Unity)
     * **IAssetViewer interface**: Plugins implement to add custom viewers (e.g., video player, shader preview)
     * **BuiltInChipProviders extended**: `AssetChipProviderBase.ViewerLauncher` seam — wired by AssetViewerFactory [InitializeOnLoad]. Chip Navigate() checks `ViewerLauncher?.Invoke(path)` first; if true, viewer handled; else falls back to ping
     * **Tests**: 224 AssetViewerFactoryTests (factory dispatch, plugin registration, viewer lifecycle), 198 PrefabViewerWindowTests (see above)
   
   - **New CLI Backends: Kimi K2 + OpenCode (CLI v0.34.0)**:
     * **Kimi K2 Backend**:
       - **KimiArgBuilder.cs** (120 LOC): Constructs `kimi` subprocess argv with role-based NDJSON protocol (system→user→assistant messages)
       - **KimiParser.cs** (74 LOC): Parses Kimi NDJSON response stream (newline-delimited events, tool calls, streaming tokens)
       - **KimiBackend.cs** (35 LOC): CliBackendBase subclass — spawns `kimi` process, pipes turn JSON
       - **KimiProvider.cs** (21 LOC): IBackendProvider Kimi implementation, auto-discovered via TypeCache
       - **Tests**: 214 KimiArgBuilderTests (role mapping, token streaming, tool call parsing), 243 KimiParserTests (event parsing, multi-line tool results, error recovery)
     
     * **OpenCode Backend**:
       - **OpenCodeArgBuilder.cs** (132 LOC): Constructs `opencode` CLI command with multi-provider model selection (Claude/GPT/Gemini). Wires models via `-m model-name` flag with format conversion (e.g., "anthropic/claude-sonnet-4" for OpenCode's provider syntax)
       - **OpenCodeParser.cs** (92 LOC): Parses OpenCode stream-json (compatible with Claude SDK format)
       - **OpenCodeBackend.cs** (49 LOC): CliBackendBase subclass — persists OpenCode process across turns (stdin loop)
       - **OpenCodeProvider.cs** (21 LOC): IBackendProvider OpenCode implementation, auto-discovered via TypeCache
       - **Tests**: 222 OpenCodeArgBuilderTests (model name mapping, provider formats, arg ordering), 273 OpenCodeParserTests (stream parsing, error handling, tool routing)
     
     * **BackendKind enum expanded**: Now includes Kimi + OpenCode (was Claude/Codex/Gemini). **BackendRegistry.cs**, **BackendConfig.cs**, **BackendProviderRegistry.cs** all updated
     * **KimiBackendConfig + OpenCodeBackendConfig**: New [Serializable] config classes in Library/MCP_ChatBackendConfig.json
   
   - **Chip Kind Extensions (View v0.34.0)**:
     * **ChipKindKeys extended**: Added Image, Model, Audio (beyond existing Hierarchy/Scene/Script/Prefab/Material/Texture/ScriptableObject/Asset/Folder)
     * **BuiltInChipProviders extended**: `ModelChipProvider` (priority 450, handles .fbx/.obj/.blend/.dae), `AudioChipProvider` (priority 550, handles .wav/.mp3/.ogg/.aiff), `ImageChipProvider` (priority 50, handles external .png/.jpg/.bmp/.gif/.webp/.tiff — obj==null only)
     * **Tests**: 84 new tests for new providers (MdBlock rendering, chip detection)
   
   - **ProviderRegistry Consolidation (CLI v0.34.0)**:
     * **ProviderRegistry.cs** (82 LOC, new): Base class for extensible provider registries (DRY consolidation across Settings/Toolbar/Panel registries). Single `Register()` + `Resolve()` pattern, optional priority ordering
     * **KeyRegex hoisting**: Moved `_KeyRegex` to non-generic companion to avoid static-in-generic reflection issues (C# generic type safety)
     * **Tests**: 57 ProviderRegistryTests (concurrent registration, key uniqueness, priority ordering)
   
   - **Tests Summary (v0.34.0)**:
     * Python: No new tests (0 changes to server/)
     * C#: 1402 new tests across CLI + View assemblies
       - CLI: 214 KimiArgBuilder + 243 KimiParser + 222 OpenCodeArgBuilder + 273 OpenCodeParser + 214 BuiltInChipProviders + 57 ProviderRegistry + 188 ImageAttachmentStore = 1411 tests
       - View: 224 AssetViewerFactory + 198 PrefabViewerWindow + 154 ImageDragDrop + 116 InlineImageThumbnail + 105 PluginToolbar + 72 PluginSettings + 37 ClipboardPaste = 906 tests
       - Total EditMode: ~3000+ green (was 2623)

10. **Sprint 1B: Assembly Split + Interactive Permissions (v0.29.2)**
   - **Chat Assembly Split (asmdef)**: UnityMCP.Editor.Chat split into two: `UnityMCP.Editor.Chat.CLI` (protocol, parsing, backends, control flow) and `UnityMCP.Editor.Chat.View` (UI windows, rendering, cards). CLI assembly compiles when main plugin is broken (zero View dependencies); View always depends on CLI. Enables frontend reload before backend fully healthy. Asmdef references one-way: View → CLI → Editor core.
   - **Interactive Permission Prompts (v0.29.2→v0.29.11 fix)**: **Original (v0.29.2)** used non-functional `--permission-prompt-tool stdio` arg expecting `sdk_control_request` events. **Fixed (v0.29.11, Sprint 1C)** implements correct CLI v2.1.177+ protocol: (1) `CliBackendBase` sends `InitializeRequest()` handshake after spawn with `PreToolUse` hooks wired to hook_0; (2) backend emits `control_request` (not `sdk_control_request`) with `subtype:hook_callback` containing tool info; (3) `ChatStreamParser` routes to `PermissionPrompt` event; (4) `ControlResponseBuilder` serializes approvals as `{"continue":true/false}` (not `{"behavior":"allow"}`) + reason field; (5) legacy `sdk_control_request`/`permission` subtype still supported for backward compat. **ToolApprovalCard** (Allow/Deny/Session/Always + RiskClassifier + SessionAllowlist) and **AskUserCard** (radio/checkbox/freetext inputs). Response flows back to backend via stdout for tool call resume.
   - **IBackendProvider + TypeCache**: Extensible backend registration without core edits. **BackendProviderRegistry** auto-discovers IBackendProvider implementations via TypeCache. Each backend plugin = 1 file with `[InitializeOnLoad]` static ctor calling `BackendProviderRegistry.Register()`. **ClaudeProvider** + **CodexProvider** (built-in, zero delta). New plugins use same pattern.
   - **Control Protocol (Python side)**: `ChatStreamParser` now handles both new (`control_request`/`hook_callback`) and old (`sdk_control_request`/`permission`) events, routes to interactive card UI via MCPChatWindow.Approve partial. Response serialized by `ControlResponseBuilder` and written to process stdin.

10. **Chat Resilience Sprint (v0.30.5): Codex Silent Abort + Inactivity Watchdog**
   - **Codex Silent Abort Fix**: Codex sets `status:"completed"` on tool errors; real indicator is `result.isError:true` (no space in JSON). **CodexAppServerParser (v0.30.5)** now detects errors via `!resultObj.Contains("\"isError\":true")` pattern-match instead of checking status. Extracts result text regardless of isError flag; if error with empty text, appends `"[MCP tool error]"` placeholder. Emits `ChatEvent.Heartbeat()` on "reasoning" events (proof-of-life during o3/o3-pro silent thinking). **Tests**: 6 new error scenario tests.
   - **Inactivity Watchdog (v0.30.5)**: Codex reasoning (o3, o3-pro) can think silently for 2–5 minutes. Old code assumed event silence = dead process, aborting in-flight work. New: **MCPChatWindow.Drain.cs** tracks `_lastEventTime` (timestamp of last drained event). DrainAndRender() checks if `EditorApplication.timeSinceStartup - _lastEventTime > InactivityTimeoutSec` (300s for Codex, 90s for Claude/Gemini) while backend running; if true, emit failure card, finalize turn, call OnTurnFailed(). Resets on every OnSend (turn start) and every event drain. Result: long reasoning completes, failures timeout gracefully. **Tests**: 2 new timeout scenarios.

11. **Sprint 1D: Claude AskUserQuestion + Codex requestUserInput (v0.29.37–v0.29.38)**
   - **Claude Interactive User Input (v0.29.37)** — Claude CLI `AskUserQuestion` events now route through new MCP tool `permission_prompt_tool` → TCP `ask_user` command → Unity `AskUserCard` UI (radio/checkbox/freetext) → user input → response returned to Claude for tool call completion. **permission_prompt_tool.py**: Registers as MCP handler for `--permission-prompt-tool mcp__unity_mcp__permission_prompt` CLI flag. Receives AskUserQuestion payload, routes to TCP bridge, awaits user response. Auto-allows non-AskUser tools (unchanged behavior for other tool requests). **ClaudeArgBuilder**: Automatically injects `--permission-prompt-tool` flag into Claude CLI args (user's `--permission-prompt-tool` config irrelevant; plugin handles it). **AskUserCard Redesign (v0.29.37)**: Extracted inner `QuestionRow` class → new file `AskUserQuestionRow.cs` (217 LOC, pill-button UI). SingleSelect now auto-submits on pill click (no separate Submit button needed). Hover animation (200ms transition, 1.03x scale). Vertical full-width layout. Fixed `Toggle.text` → `Toggle.label` bug (Unity BaseBoolField nulls .text in constructor). Other field returns answers-map JSON, not raw text. **FlowBar Enhancement**: `_askPending` flag hides Stop button + progress bar during user input (prevents cancellation mid-prompt). **Gating**: `permission_prompt` added to `CORE_TOOLS` and `TIER1` (always visible).
   - **Codex requestUserInput Integration (v0.29.38)** — Codex CLI can now show same interactive `AskUserCard` via JSON-RPC `tool/requestUserInput` and `item/tool/requestUserInput` requests. **CodexAppServerParser**: Detects both request types, extracts numeric `id` field, prefixes response with `"codex:"` for reply routing. **CodexAppServerBackend**: Advertises `experimentalApi: true` in initialize capabilities. **ControlResponseBuilder**: New `CodexUserInputResponse()` method formats JSON-RPC response with `int.TryParse(id)` guard: numeric id → unquoted, string → quoted for safety. **AskUserCard**: Detects `"codex:"` prefix in `Submit()`, formats positional answers array `[{"answer":"..."}]` matching Codex protocol (vs. `{"answer":"..."}` for Claude). Same interactive UI experience across both backends.
   - **Tests**: v0.29.37 added 6 Python tests (permission_prompt_tool), 68 C# tests (AskUserCard redesign). v0.29.38 added 7 C# tests (CodexAppServerParser, ControlResponseBuilder, AskUserCard Codex protocol). Total: 2413 Python tests passed, 2623+ C# EditMode green.

## Tool Categories

**Update v0.30.4**: validate_move added to asset category (6 tools total). Test marker `live_haiku` → `live_cli` (semantic only, backward compatible).

### TIER1 (always visible, 38 core)

Core (38): 24 base + 3 intent + 3 code-intel + 8 runtime = get_hierarchy, get_component, inspect, set_property, create_object, delete_object, manage_component, batch, get_console, get_compile_errors, screenshot, scene, editor, search_scene, run_tests, discover_tools, get_enabled_tools, setup_objects, set_properties, configure_objects, set_parent, do, ask, get_metrics, animator_intent, vfx_intent, ui_intent, find_references, compile_preflight, semantic_at, invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest

### Category: object (8)
find_objects, get_object_detail, get_components_list, set_active, set_material, wire_event, unwire_event, set_property_delta

### Category: animation (4)
animation, timeline, animator, particle

### Category: asset (6)
asset, material, prefab, scriptable_object, project_settings, validate_move (v0.30.4)

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
scene (new/open/save/discard), animation (get/create/edit/add_key/remove_key/remove_curve/set_keys/set_loop/preview), timeline (get/create/edit/add_track/remove_track/add_clip/remove_clip/set_binding/set_timing/mute/unmute/lock/unlock/preview), references (get/find_to/remap), editor (state/play/stop/pause/select/project_path), animator (get/add_param/add_state/add_transition/set_default/remove), particle (get/create/set/apply), shader (get/create/set/graph_get/graph_create/graph_node/graph_edge), asset (find/get_info/create/move/duplicate/delete/validate_move/get_dependencies/import_settings/export_package/import_package), material (create/get/set/copy/list_properties), prefab (save/create_variant/apply/revert/get_overrides/unpack), scriptable_object (create/get/set/list_types/find), project_settings (get/set), spatial_query (nearest/in_front_of/objects_in_radius/bounds_info/raycast/spatial_map), create_ui, set_rect, menu (execute/list)

### Runtime (Play Mode only)
invoke_method, set_runtime_property, query_state, wait_until, move_to, test_step, run_playtest

## Key Systems

### Capability Gating (Python: `tools/gating.py`)
- **CORE tools** (22): locked, always visible, can only be hidden via `FORCE_VISIBLE` escape hatches (discover_tools, get_enabled_tools, do, ask, editor, get_console, get_compile_errors, reconnect_unity, list_connections). Example: `is_core("get_hierarchy")` → True
- **Themed catalog** (single source of truth): `get_catalog()` returns dict with 14 categories (CORE as category, not separate key); public tools only, no NDA/plugin names. Format simplified for token economy (CORE → categories["CORE"]).
- **Catalog serialization (v0.18.0+)**: Plain-text format sent to C# (`set_tool_catalog`): `CORE:tool1,tool2\nSCENE_EDIT:tool3,tool4\n...` via `CatalogParser.Parse()` (no JSON encoding). Reduces ~40% wire size vs JSON + eliminates C# JSON deserializer cost.
- **Filtering pipeline**: (1) apply TIER1+session gating via `_apply_gating()`, (2) subtract disabled set from Unity MCPSettings via `_filter_tools()` (cache=None → gating-only fallback). Approach is "hide-disabled-set" (NOT allowlist — Python-only tools not in Unity's CSV wouldn't be wrongly hidden)
- **Sessions**: session-enabled via `discover_tools(category, enable)` (legacy CATEGORIES dict still works for back-compat)
- **Plugin self-registration**: `gating.register_tools("category", tools_set, tier1=tier1_subset)` lets plugins add to CATEGORIES + TIER1 without hardcoding
- **Push catalog**: `_push_catalog()` sends Python-authoritative catalog to Unity on connect/reconnect via `set_tool_catalog` command (plain-text, TCP-only, silent on failure)
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
20. Distiller — heuristic + Haiku background distillation of large responses (**v0.23.0: full param + cache key fix**)
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

### v0.23.0 Tool Fixes
- **compressor.py**: `_FIELD_ALIASES` dict for field projection (bypass distill)
- **objects.py**: `full` param to bypass distill filtering
- **scene.py**: `full: bool = False` parameter for scene tools
- **middleware_async.py**: distill cache key collision fix (include full flag)

### Auto-Batch (Python: `tools/autobatch.py`)
- `setup_objects(specs)` — create+configure multiple objects (one per line DSL)
- `set_properties(path, props)` — set multiple properties (component.prop=value)
- `configure_objects(config)` — configure multiple objects (/Path component.prop=value per line)
- All expand internally to `batch` commands

### Intent Meta-Tools
- `do(intent, dry_run)` — NL → Haiku plan → validate → batch execute
- `ask(question)` — NL read-only question → deterministic route → Haiku summarize
- `animator_intent`, `vfx_intent`, `ui_intent` — domain-specific NL intent tools (core)

### Test Infrastructure (C#: TestRunner + MultiSceneTestBase, v0.25.0)
- **TestRunner.cs**: Wraps Unity Test Framework API with SessionState-based pending tracking. Exposes Execute(mode, onComplete, group, **filter**) with pipe-separated test class filtering. **filter="Class1|Class2"** runs ONLY matching groupNames (~2s vs ~65s full suite)
- **Filter.groupNames** conversion: `filter.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)` parses pipe-separated class names into UTF framework groupNames array
- **MultiSceneTestBase** (v0.25.0): Shared DRY base for multi-scene test suites. Saves/restores additive scenes before AddScene() to preserve real scene names (vs Unity temporary names). Captures main scene name in SetUp to unblock NewScene scene-change behavior. Eliminates test-file duplication across MultiSceneFinderTests, MultiSceneHierarchyTests, MultiSceneOperationsTests
- **ObjectDiffHelper** (v0.25.0): Now compares Transform properties (Position, Rotation, Scale, LocalScale) alongside all other components. Improves object diff accuracy for verification gates
- **Compile Check Gate** (MANDATORY before NUnit): TCP `get_compile_errors` must pass before running NUnit tests. Unity runs stale DLL on compilation failure; tests invalid against old code. Editor.log unreliable. Implement as: `run_tests(mode="EditMode")` catches compile via early test failure, OR manual `await get_compile_errors()` in Python

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
- **CliBackendBase** (abstract): Shared lifecycle host for CLI-based backends. 4 variation axes: (a) `BuildArgs` (spawn/resume argv + env-key-strip), (b) `ParseLine` (NDJSON line → ChatEvent[]), (c) `BinaryName` (CLI executable), (d) `IsPersistentProcess` (stdin loop vs spawn-per-turn). Injects `UNITY_MCP_PORT` env var when spawning child process (reads MCPServer.ServerChatPort from parent). Owns spawn, drain, accumulate, SessionId, Stop, Dispose — subclasses override only the 4 axes.
- **ClaudeBackend** (ported onto base): Zero behavior change (−65 lines net). Persistent stdin loop (IsPersistentProcess=true), Claude NDJSON parser, `--resume <sessionId>` argv builder. Uses `ChatMcpConfigWriter.GetOrCreateConfigPath()` to generate temporary JSON config + `--mcp-config <path> --strict-mcp-config` flags for isolated MCP server isolation.
- **CodexAppServerBackend** (only Codex option, v0.14.0+): Persistent Codex session via `codex app-server` (direct stdio, JSON-RPC 2.0). One process per chat session (IsPersistentProcess=true). Protocol: `initialize` → `thread/start` → repeated `turn/start` with `mcpToolCall` items + real token streaming via `item/agentMessage/delta` (240+ deltas/turn). MCP injection via `-c mcp_servers.*` flags at session init. Spike-verified with codex 0.137.0.
- **CodexAppServerParser**: JSON-RPC 2.0 notification/response parser → ChatEvent (notification item types: mcpToolCall camelCase, agentMessage/delta token stream, turn/completed with usage; thread_id at result.thread.id).
- **CodexArgBuilder**: Constructs `codex app-server` argv + session init args. Re-passes three `-c mcp_servers.*` flags at initialization.
- **BackendRegistry** & **BackendKind** enum: Factory dispatch. User selects Claude (persistent stdin) or Codex (persistent JSON-RPC app-server) from dropdown. BackendKind = {Claude, Codex} (v0.20.0 simplified from 3 entries by removing spawn-per-turn CodexBackend).
- **PendingTurnState v3**: Now persists `BackendKind` for domain-reload survival (back-compatible with v1/v2).
- **Binary Resolution (cross-platform v0.21.0+):**
  * **ChatBinaryResolver.cs** — Cross-platform binary resolution: (1) Check EditorPrefs override key per backend (e.g., `UnityMCP_Chat_ClaudePath`, `UnityMCP_Chat_Path_codex`), (2) Query login shell via platform-specific methods:
    - **Windows:** `where.exe <binary>` with `NoDefaultCurrentDirectoryInExePath=1` (MITRE T1574.008 CWD-hijack mitigation). Parses multi-line output: prefers `.exe` over `.cmd` lines.
    - **macOS:** `/bin/zsh -lc 'command -v "$1"' zsh <binary>` via LoginShellCommand. Rejects multi-line output (banner contamination).
    - **Linux:** `/bin/bash -lic 'command -v "$1"' bash <binary>` (bash preferred over sh). Reads stderr into drain (suppress "no job control" warning). Parses last line starting with `/` (real path after banner).
  * **ChatMcpConfigWriter.cs** — Python command resolution (DRY with CodexArgBuilder): (1) Windows `.venv/Scripts/python.exe`, (2) Unix `.venv/bin/python`, (3) `uv` binary (Claude only, Codex passes null), (4) fallback `python` (Windows) or `python3` (Unix). Checks File.Exists for venv paths (cross-platform check). Generates temp JSON config with resolved command + `-m unity_mcp.server` args.

### Per-Backend Settings (C#: BackendConfig + BackendSettingsForm, v0.15.0 F9)
- **BackendConfig.cs** — [Serializable] per-backend configs (model, permission_mode, timeout, extra_args)
- **BackendConfigStore.cs** — Loads/saves to `Library/MCP_ChatBackendConfig.json` (project-local, NOT global ~/.codex/config.toml)
- **BackendSettingsForm.cs** — UIToolkit foldout per backend with model/permission/timeout/extra-args dropdowns
- **Wiring:** `ClaudeArgBuilder` + `CodexArgBuilder` read from store, inject into argv construction
  - **ClaudeArgBuilder.cs** — builds `claude` subprocess argv with stdin pipe + MCP config path + `--strict-mcp-config` flag (prevents secondary MCP server registration from `~/.mcp.json`, keeping only the in-editor config)
- **ArgTokenizer.cs** — Shell-style quote-aware split (double+single quotes, unbalanced trailing tolerated); centralizes whitespace+quote parsing for both builders; fixes silent corruption of quoted multi-word ExtraArgs values (e.g., `--append-system-prompt "be terse"`); +11 tests

### Typed Context Tags (C#: ChipKind + ResponseTagInliner, v0.15.0 F10)
- **ChipKindDetector.cs** — Pure `Detect()` method categorizes chips: Hierarchy, Scene, Script, Prefab, Material, Texture, ScriptableObject, Asset
- **ChipData.Kind** — Each chip carries a `ChipKind` enum
- **ChipConfig.cs** — Per-kind depth config (none|path|summary|full), persisted in BackendConfigStore
- **Send-side (input):** ChipContextResolver.EmitTyped() formats as `[hierarchy:/Player #123]`, `[script:PlayerController]`, `[scene:.../Main.unity]` for AI consumption; visual chips show left-side kind-prefix (color-coded)
- **Receive-side (response):** ResponseTagInliner.Apply() parses ONLY exact `[kind:ref]` format (conservative regex, no false positives on markdown/code/bare brackets); renders compact colored pills with `<link>` click-nav (symmetric with input)
- **Tests:** ChipKindDetector 13/13, ResponseTagInliner 17/17 (false-positive guards), EmitTyped 7/7, DepthFor 10/10, ChipConfig 3/3

### Extensible Chip-Kind Registry + Composed Inline Field (v0.15.8 F11 + v0.16.0 F12)
- **IChipKindProvider** — Public interface for third-party plugins: Key, Priority, CanHandle, Create, IconName, HexColor, FormatPayload, DefaultDepth, Navigate
- **ChipKindRegistry** — Public static registry; plugins call `Register(provider)` from `[InitializeOnLoad]`. Detection: `Resolve(obj, assetPath)` returns first provider where `CanHandle` true (sorted by Priority). Supports dynamic Unregister + per-key lookup
- **Priority Convention:** <100 overrides built-in type, 100–800 built-ins, >800 extends (new kinds). 8 built-in providers: Hierarchy/Scene/Script/Prefab/Material/Texture/ScriptableObject/Asset
- **Inline Rendering (F12 refactor):** Replaced overlay stack (InlineChipOverlay/UitkCharRect/NbspReservation/TokenSpan) with **composed `InlineChipField`** — a flex-row VisualElement with pill children + trailing TextField. Pills are layout children, not overlays, eliminating mis-positioning and vanish-on-type bugs. Atomic backspace-at-0 removes last chip (standard tag-input UX). `InlineChipModel` is pure headless data (no rendering). `ChipPillFactory` builds pills shared by input field and response rendering.
- **Chip Display Overrides (F12 P4):** `ChipDisplayOverride` struct + parallel arrays in `ChipConfig` support per-kind LLM-payload depth (none/path/summary/full) and graphical color customization. Settings form enumerates all registered kinds (built-in + 3rd-party) dynamically with depth dropdown + color field. `ChipPillFactory.ColorResolver` static seam (set once on window open, consulted by both input and response pills). Zero core edits needed for 3rd-party customization.
- **Show LLM Payload:** Right-click context menu reveals exact byte-for-byte AI payload (symmetry test enforces match)
- **Reload Survival:** `PendingTurnState v5` serializes `KindKeys[]` parallel to chip paths; on resume, re-binds by key (fallback: re-detect if provider not registered yet)
- **Breaking Change (BUG B):** `ChipConfig` default depth `"summary"` → `"path"` (token-minimal). Restore via F9 settings form. Marked in-code: `// BREAKING (v0.16.0)`.
- **Response Pills (F12 P7):** `ResponseTagInliner.Split()` + `MixedParagraphRenderer` render response-side `[kind:ref]` tags as graphical pills (leaf name, click→ping/select, tooltip=full ref) in paragraphs and lists. `RefParser` (inverse of FormatChipRef) strips ` #id` from hierarchy refs before lookup. Pills colored via shared `ChipPillFactory.ColorResolver` (live-updated on settings change).
- **No Auto-Selection (F12 P3+P5):** Removed legacy auto-prepend of SelectionSummary. Context flows exclusively through explicit typed chips (prevents duplicate/verbose context). SelectionSummary class kept for depth="summary" resolution in ChipContextResolver.
- **Tests:** ChipKindRegistryTests, InlineChipModelTests, ChipPillFactoryTests, InlineChipFieldTests, ChipDisplayOverrideTests, ResponseTagInlinerTests, MixedParagraphRendererTests, NewSessionTests — 1581/1586 EditMode pass (5 pre-existing reds)

### Bare-Name Normalization + Add-to-Context (v0.17.14 F14)
- **BareNameNormalizer.cs** — Converts bare scene object names in LLM responses to `[kind:path #id]` bracket tags (F14a). Mirrors longest-first scan logic: protected ranges include existing `[kind:ref]` tags + triple-backtick fenced code blocks (never re-tagged). Word-boundary rules prevent partial matches. Handles ambiguous names gracefully (skips single-char, allows case-insensitive match with word bounds check).
- **ChipPillFactory.AddToContextAction** — Right-click "Add to context" seam on response pills. MCPChatWindow wires via `OnEnable`/`OnDisable` to attach the menu to response pill segments. Preserves full ChipData (kindKey + instanceID) instead of re-deriving via path-only resolution. Injects chip directly into `InlineChipField.AddChip()`.
- **Display + LLM Text Split (ChipTextInterleaver):** UserMessage send path now splits `rawText` (TextField with @mentions, displayed in bubble as chip strip) from `llmText` (with `[kind:ref]` tags, sent to AI). `ToDisplayText()` emits @DisplayName with spacing (leading space if needed, trailing space, Trim). `ToLlmPayload()` reuses ToDisplayText then appends chip context block. `BuildFromRaw()` strips @mentions before building clean segments.
- **InlineChipField @mention Injection:** `AddChip()` inserts "@DisplayName " at cursor; `RemoveChipAt()` strips corresponding @mention text. `InlineChipModel.AdjustOffsetsAfterTextChangeInclusive` adjusts chip offsets for TextField mutations. MCPChatWindow.Send.cs uses BuildFromRaw instead of Build.
- **Tests:** 201 BareNameNormalizerTests (16/17 fenced-block edge cases + lowercase match + bare-name cycle tests). ChipTextInterleaverTests expanded to 186 tests (R1–R5 BuildFromRaw coverage + @mention spacing edge cases). AssistantBubbleNormalizationTests (68 tests for frozen-bubble normalization flow). PillContextMenuTests (93 tests for right-click injection). New E2E chips integration: M1–M10 (interleaver), E2E_1–E2E_3 (normalization in bubble). 1586/1591 EditMode pass (5 pre-existing reds).

### Context Menus + Unified @-Mention Path (v0.17.17 F15a-F19, v0.20.0 Phase 1)
- **HierarchyContextMenu.cs** — Menu item `GameObject/Add to Chat Context`. Right-click any GameObject in the Hierarchy window, option appears to inject the selected object as a chip into the chat input. Includes validation to ensure the object is valid before injection.
- **ComponentContextMenu.cs** — Menu item `CONTEXT/Component/Add to Chat Context`. Right-click any Component in the Inspector, option appears to inject the parent GameObject as a chip into the chat input. Includes validation before injection.
- **Unified Chip Rendering Path (v0.20.0 Phase 1, P0 fix):** All scene object refs now route through ONE path: AtMention/BareName → `[kind:ref]` → ResponseTagInliner → MixedParagraph → ChipPillFactory pill. Deleted the secondary SceneNameLinker.Linkify path (static mutable `MarkdownInline.Linker` seam) which was rendering refs as `<link><u>Name</u></link>` between pills. Gated the scene-wide BareNameNormalizer pass behind `MCPChat.DisableSceneNameNorm` kill-switch to disable if needed. RefreshResolver (renamed from RefreshLinker) called before FinalizeAssistant in Drain TurnDone so objects created mid-turn are visible to normalization.
- **Leading-Space Guard (F15c):** Consolidated space-handling in `InlineChipField.AddChip()`, `InsertChipAt()`, `InjectMentionAt()` via `prependSpace` parameter. Chips no longer glue to surrounding text; @mention format preserved on round-trip (space before chip, no space after).
- **Tool-Detail CSS (F19):** Response tool cards now render with correct flex-layout: `tool-chip--expanded { flex-direction: column }` stacks details vertically; `tool-detail { flex-shrink: 0 }` prevents content collapse during overflow.
- **Tests:** BuildFromRawDefensiveTests (65), ContextMenuTests (102), F15bScenePillPipelineTests (104), F15cSpaceAfterChipTests (76), F19ToolDetailTests (54), NormalizationPipelineTests (7, v0.20.0), MixedParagraphBreakTests additions, SceneObjectNormalizationTests assertions fixed. Total 32 new tests.

### UX Features (v0.15.0 F1–F10 + v0.15.8 F11 + v0.16.0 F12)
- **F1 (Token Reset):** TokenResetTests ensure counters reset on backend/model switch
- **F2 (Cascade Restore):** TurnUndoTracker.RestoreFromIndex() reverts any earlier turn + all later turns (reverse order)
- **F3 (Approve Gate):** Button shows only when turn has real tool calls (_turnHasToolCalls flag)
- **F4 (Hierarchy #ID):** ChipContextResolver appends `#<instanceID>` to scene object refs (SelectionSummary.Summarize disambiguation)
- **F5 (Inline Chips):** InlineChipField composed control (flex-row of pill children + TextField) replacing overlay stack; drag-drop, removable ✕ button, context menu "Add Selection"
- **F6 (Auto-Scroll Toggle):** EditorPref gate (default ON) for scroll behavior during streaming
- **F7 (Status Distinction):** ChatBackendProbe detects Chat-active vs CLI-listening (3-state: Down/Listen/ChatActive); domain-reload safe (per-call resolution)
- **F8 (No Beta Labels):** Removed "(Beta)" from chat toggle + settings foldout
- **F9 (Settings Form):** Per-backend config form → own JSON → CLI args; includes per-kind chip depth/color overrides (see BackendConfig above)
- **F10 (Typed Tags):** Kind-aware input/output chips with configurable depth (see Typed Context Tags above)
- **F11 (Extensible Registry + Inline Render):** IChipKindProvider public interface + ChipKindRegistry for third-party chip kinds (see Chip-Kind Registry above)
- **F12 (Chip UX Overhaul):** Composed inline-chip field (P1+P2), removed auto-selection (P3+P5), per-kind display settings (P4), response scene-object pills (P7), new-session/clear button (P6). See Extensible Chip-Kind Registry + Composed Inline Field above for details.
- **F13 (Chip Input/Display Architecture Fix):** Unified inline-chip architecture (flex-row composite field), @mention display injection, offset-drift fix, comprehensive TDD coverage. Send path splits rawText (display) from llmText (AI). Re-render after normalization preserves sent state. API cleanup (PositionedChip, ChipTextInterleaver API). Test DRY (ChipTestHelpers).
- **F14 (Bare-Name Normalizer + Context Menu):** LLM response bare object names converted to `[kind:ref]` tags (BareNameNormalizer, fenced-code protected). Right-click "Add to context" on response pills (ChipPillFactory.AddToContextAction). Full chip data preserved (kindKey+instanceID). See Bare-Name Normalization + Add-to-Context above for details.
- **F23 (Settings Windows Split):** Monolithic MCPSettings EditorWindow refactored into 3 focused UI windows: `MCPToolSettingsWindow` (Tool Settings menu), `MCPPermissionsWindow` (Permissions menu), `MCPConnectionWindow` (Connection menu). MCPSettings becomes pure static data class (API preserved). Chat assembly decoupled via event hook `ChatSettingsHook.OnBuildConnection` — `ChatConnectionSection` subscriber `[InitializeOnLoad]` injects Chat content without core edits. Dead code paths (OnBuild/Invoke/AppendSection) removed. 5 new tests.

### Editor UI Windows (C#: UIToolkit)
- **MCPSettings** (MCPSettings.cs): Pure static data class — catalog persistence, EnabledTools state, no EditorWindow. Public API preserved for backward compatibility.
- **MCPToolSettingsWindow** (MCPToolSettingsWindow.cs, `MCP/Tool Settings` menu): Per-tool enable/disable toggles, organized by theme categories (CORE locked, others toggle/tri-state group masters), search bar, presets (Minimal/Full/No-visuals), dynamic Plugins section from PluginRegistry. UIToolkit.
- **MCPPermissionsWindow** (MCPPermissionsWindow.cs, `MCP/Permissions` menu): Agent tool deny-set configuration (permissions deny-list UI). UIToolkit.
- **MCPConnectionWindow** (MCPConnectionWindow.cs, `MCP/Connection` menu): CLI binary path, auth settings, backend selection, chips. Uses event hook `ChatSettingsHook.OnBuildConnection` for Chat assembly to inject settings content without core edits (extensible pattern).
- **MCPStatus Window** (MCPStatusWindow.cs): Connection status monitor. UIToolkit-based with breathing heartbeat pulsation (ECG beat when connected, gentle beat when listening, flatline when stopped). Centered orb (`.orb`) + halo ring (`.orb-halo`) with state-driven colors & USS class-triggered pulsation (border-width + opacity + background-color transitions, 2021.3-safe). Polling every 700ms for state. Buttons: Restart MCP / Kill MCP / Reimport. Stylesheet: `MCPStatus.uss`.
- **Stylesheet Helper** (MCPEditorUtils.LoadStyleSheet): Shared two-path loader for `.uss` files, called by windows (DRY; handles package-relative asset lookup).

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

## Dual-Channel Reload Recovery (v0.27.4)

**Reload Package:** Independent UPM package `com.unity-mcp.reload` (separate asmdef, references:[]) runs background mini-server on port 9600+ (SO_REUSEADDR bind-retry). Persists discovered port to `Library/MCP_Port.json`. AssetImportWorker gate prevents interference with import pipeline. **Rationale:** When main plugin compilation breaks, domain reload is blocked; the reload package compiles independently and provides recovery channel.

**Recovery Ladder (Python: `reload_ladder.py` T0-T5):**
- **T0 (baseline):** Synchronous diagnose check, 1 poll
- **T1 (force_refresh):** Call C# force_refresh + poll main MVID (30 polls × 15s = 7.5min timeout)
- **T2 (AssetDatabase.Refresh):** Out-of-band full refresh via reload port + poll (3s sleep before poll)
- **T3 (RequestScriptCompilation):** Out-of-band compile request via reload port + poll
- **T4 (reimport fallback):** Last attempt, 20s polls (no max)
- **T5 (play mode fallback):** Enter/exit Play mode to force compile via main thread, 2s wait

**Sole Healing Proof:** MVID-delta (`main_mvid` before/after each tier). Frozen MVID + compile error = BROKEN_DOMAIN sentinel (domain stuck, manual reimport needed).

**Integration:** `sync.py _attempt_recovery()` calls `run_ladder(start_tier=2)` on REIMPORT-NEEDED verdict.

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
- **Core** (55+ files): MCPServer, CommandRouter (3 partials), CommandRegistry/Schema, IMCPPlugin/PluginRegistry, ObjectManager, ValueParser, InputNormalizer, BatchHelper, HierarchySerializer, ComponentSerializer, RefManager, ErrorHelper, RuntimeHelper, PlaytestRunner (2 partials), PlaytestParser, MultiViewCapture, CodeExecutor, SearchHelper, SpatialHelper, AnimationHelper, TimelineHelper, AnimatorControllerHelper, ParticleHelper, ShaderHelper, ShaderGraphHelper, UIHelper, ReferenceHelper, AssetDatabaseHelper, ProjectSettingsHelper, MaterialHelper, PrefabHelper, ScriptableObjectHelper, MCPSettings (data class), MCPToolSettingsWindow, MCPPermissionsWindow, MCPConnectionWindow, MCPStatusWindow, MCPStatusModel, MCPStatusBarWidget, MCPActions, ChatSettingsHook (event hook)
- **Chat Module** (130+ files, optional behind UNITY_MCP_CHAT define, v0.29.2 split into CLI + View assemblies):
  - **CLI Assembly** (UnityMCP.Editor.Chat.CLI, protocol + backends, compiles independently when main broken):
    - **Backends:** CliBackendBase, ClaudeBackend, CodexAppServerBackend, CodexAppServerParser, CodexArgBuilder, BackendProviderRegistry (auto-discovery), IBackendProvider (extensible), BackendConfig, BackendConfigStore, BackendSettingsForm
    - **Control Protocol:** CliBackendBase.SendInitializeHandshake (sends `initialize` request w/ PreToolUse hooks after spawn), ChatStreamParser (routes `control_request` w/ `hook_callback` subtype → PermissionPrompt event; backward compat `sdk_control_request`/`permission`), ControlResponseBuilder (serializes `{"continue":true/false,"reason":"..."}`), ApprovalDecision enum (Allow/Deny/Session/Always)
    - **Infrastructure:** ChatEvent, ChatTranscript, IChatBackend, ChatBinaryResolver, ChatProcess, ChatMcpConfigWriter, PendingTurnState, ReloadGuard, SentTextCache, StderrRingBuffer
    - **Tools & Input:** ToolVerbMap, ToolCallAccumulator, ToolCallRecord, ToolChipGrouper, ToolDetailBuilder, ToolGroupState, ToolGroupSummary, UserTurnBuilder, UserToolResultParser
    - **UX/Formatting:** TokenFormat, ChatActivityState, ChatLabel, ChatRefResolver, CopyableText, CopyTextBuilder, InputHeightCalc, JsonArrayScan, ArgTokenizer, ArgQuoting
    - **Chip Infrastructure (shared):** ChipContextResolver, ChipKindDetector, InlineChipData, InlineChipModel, ChipPillFactory, BareNameNormalizer
  - **View Assembly** (UnityMCP.Editor.Chat.View, UI rendering, depends on CLI):
    - **Windows & Cards:** MCPChatWindow (10 partials: Drain, FlowBar, Chips, InlineChips, Selector, Approve, Slash, Session, Resize, Send), RestoreButton, TurnUndoTracker, SelectionSummary, CompileAutoFix, EditorStateSnapshot, ToolPing, **ToolApprovalCard** (RiskClassifier, SessionAllowlist), **AskUserCard** (radio/checkbox/freetext)
    - **Response Rendering:** ResponseTagInliner, MixedParagraphRenderer (paragraph pills), RefParser (ref parsing for response pills)
    - **UX/Formatting (View-specific):** EnterKeySend, EnterKeyLogic, ChatRefAction, CopyTextBuilder, InputHeightCalc, TokenFormat
    - **Rendering:** Markdown/ (MdBlock, MarkdownParser, MarkdownParser.Blocks, MarkdownInline, IChatBlockRenderer, ChatBlockRendererRegistry, ChatBlockRendererFactory, MarkdownBlockRenderer, MarkdownBlockRenderer.Table, MarkdownBlockRenderer.List, ImageBlockRenderer, ChatLinkify), Mermaid/ (MermaidGraph, MermaidParser, MermaidLayout, MermaidLayout.Layers, MermaidBlockRenderer, MermaidView, MermaidEdgePainter)
    - **Styling:** MCPChatWindow.uss, ApproveButtonFactory, ApproveHelper
  - **Test Suites** (50+ NUnit files, split by assembly): CLI tests (ControlResponseBuilderTests, ChatStreamParserTests, CliBackendBaseTests, CodexArgBuilderTests, CodexAppServerParserTests, ClaudeArgBuilderTests, ToolVerbMapTests, PendingTurnStateTests, SentTextCacheTests, ArgTokenizerTests, ArgQuotingTests, BackendConfigStoreTests, BackendRegistryTests, ChatActivityStateTests, ChatMcpConfigWriterTests, ChatProcessTests, ChatBinaryResolverTests, ChipContextResolverTests, ChipKindDetectorTests, BareNameNormalizerTests); View tests (ToolApprovalCardTests, AskUserCardTests, EnterKeySendTests, RestoreButtonTests, TurnUndoTrackerTests, SlashRegistryTests, SlashPopupTests, InlineChipModelTests, InlineChipFieldTests, ChipPillFactoryTests, ChipDisplayOverrideTests, ApproveFlowTests, ResponseTagInlinerTests, ResponseTagPillTests, MixedParagraphRendererTests, NewSessionTests, TokenResetTests, SelectionSummaryTests, NormalizationPipelineTests, Markdown/Mermaid render tests, ChatLinkifyTests)

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
- [ ] **Multi-scene API** (skill: `.claude/skills/multi-scene.md`):
  - [ ] No raw `SceneManager.sceneCount > 1` — use `SceneContext.Current.IsMulti`
  - [ ] No hand-built `"sceneName:/" + path` — use `ComponentSerializer.GetPath(go)`
  - [ ] Scene iteration uses `SceneContext.Current.Scenes`, not raw `GetSceneAt(i)`
  - [ ] New tool returning paths: tested in both single and multi-scene mode

## Related

- Skills: `.claude/skills/`
- Changelog: `AI/changelog.md`
