# Feature: Architecture Overview

## Overview

MCP-сервер для управления Unity Editor из Claude Code с минимизацией токенов (10-15x сжатие vs JSON).

## Installation & Distribution (v0.38.0+, v0.42.0: Setup Wizard 3-screen flow + 9 backends; v0.47.1: Windows UX + GitHub-direct install)

**Simplified install flow (vs v0.37.0):**

1. **Python Server**: Installs directly from GitHub via `uvx --from git+https://github.com/german-krasnikov/unity-kiss-mcp.git#subdirectory=server unity-mcp` (v0.47.1: GitHub-direct git+URL install, GIT_INSTALL_URL constant in resolver.py)
2. **Unity Plugin**: UPM git URL `https://github.com/german-krasnikov/unity-kiss-mcp.git?path=unity-plugin`
3. **Bootstrap Scripts**: One-liner `curl | bash` (macOS/Linux) or `iex (iwr).Content` (Windows) handles cloning repo + venv + config. **v0.47.1 Windows**: uses `Invoke-RestMethod` instead of `iwr` to reduce AV triggers, refreshes PATH after fallback uv install via astral.sh
4. **Setup Wizard** (v0.42.0, 3-screen flow; v0.47.1: fallback JSON export): Auto-opens in Unity on first run
   - **Screen 1 (Welcome)**: Introduction + System checks (Python found, TCP available)
   - **Screen 2 (PickBackend)**: 9 backend cards with auto-detection (BinaryName PATH check + ConfigDir existence)
   - **Screen 3 (Configure)**: Scope toggle (Global/Project via `--project-dir` flag) + per-backend setup. **v0.47.1**: Shows AiConfigScreen with fallback copyable JSON config when UPM install source detected (file: vs git: via InstallSourceDetector)
5. **9 Supported Backends** (v0.42.0): Claude Code, Claude Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity. **IsDetected logic**: BinaryName check via which/where (PATH) + ConfigDir existence check (~ expanded at runtime). **v0.47.1**: per-client root_key detection (e.g., `mcpServers` for Claude Code/Desktop, `mcp_servers` for Codex TOML, platform-aware ConfigDir for Windows)
6. **Config Auto-Gen**: `python install.py configure --tool [claude-code|claude-desktop|cursor|windsurf]` merges MCP server entry into client config. Global/Project scope via `--project-dir` flag (v0.42.0). **v0.47.1**: AiToolCardFactory abstracts platform paths, Claude Code writes ~/.claude.json instead of clipboard
7. **Doctor Tool**: `python install.py doctor` diagnostic checks (Python, imports, TCP connectivity, config validity). **v0.47.1**: validates git+URL presence in configs, warns on stale PyPI entries, checks uvx + git in PATH
8. **Version Sync**: `scripts/sync_versions.py X.Y.Z` bumps all 3 version files (server/pyproject.toml, plugin/package.json, server/__version__.py)
9. **GitHub-Direct Install** (v0.47.1): DRY consolidation — `GIT_INSTALL_URL` constant shared between Python resolver.py and C# WizardConfigWriter.cs, consumed by all backends for consistent versioning. Update banner includes `--reinstall` flag for recovery

**Architecture changes:**
- **install.py** (`install/` module): Multi-command CLI (setup, update, doctor, configure, uninstall) with lazy config module imports. `--project-dir` flag for scope toggle (v0.42.0). **doctor warns about stale Codex entries (v0.44.0)**. **v0.45.0**: Added `connect` (link projects via file: in manifest.json), `disconnect` (restore registry source), `pull` (git pull --tags for file: installs). **v0.47.1**: doctor validates git+URL presence in configs, warns on stale PyPI entries, checks uvx/git in PATH
- **Config system** (`server/src/unity_mcp/config/`): CLIENT_REGISTRY (Claude Code/Desktop/Cursor/Windsurf), config path detection, MCP JSON merger, backup/restore. **Codex TOML merger (v0.42.0)**: `merge_toml_mcp()` support. **Stale entry cleanup (v0.44.0)**: strips `[mcp_servers.unity]` on first write, creates .bak backup (first-write-wins). **v0.47.1**: `resolver.GIT_INSTALL_URL` as single source of truth, validator.py skips json.loads for TOML clients (Codex), respects per-client root_key (mcpServers vs mcp_servers)
- **Update checker & LevelUp UX** (v0.42.0+v0.44.0): GitHub API polling (v0.47.1: switched from PyPI to GitHub releases API via api.github.com/repos/.../releases/latest) + UpdatesPage changelog viewer. **LevelUp arcade-style animation (v0.44.0)**: 4-state panel (Idle→Animating→Done→Diff), XP bar + sparkles via LevelUpAnimator, release notes diff via ReleaseDiff. **v0.45.0**: InstallSourceDetector (file: vs git: detection via PackageInfo.source), LocalPluginUpdater (git pull --tags async), UpmPluginUpdater (Client.Add chain), UpdateDispatcher (DRY routing), ChatMcpConfigWriter uvx fallback. **v0.47.1**: `_update_check.py` uses GitHub releases API with importlib.metadata for version read, 24h cache TTL, banner includes --reinstall flag. **v0.50.0+**: UpdateChecker validates git+URL in configs, ClearCache on Level Up callback chain (v0.50.1). **v0.50.2**: WizardConfigWriter GitInstallUrl made public for cross-assembly access.
- **Plugin side** (C#, v0.42.0): SetupWizard 3-screen flow, BackendDescriptor with 9 backends + IsDetected, PickBackendScreen + ConfigureScreen, scope toggle, Wizard asmdef split. **Config recovery (v0.44.0)**: WizardConfigWriter.HasBackup + RestoreConfig, AiConfigScreen Restore button. **v0.45.0**: Async local plugin updates (LocalPluginUpdater.UpdateAsync), UPM registry updates (UpmPluginUpdater.UpdateAsync). **v0.47.1**: `WizardConfigWriter.GitInstallUrl` constant (shared with Python resolver.py), AiConfigScreen with fallback copyable JSON on UPM installs, `AiToolCardFactory` platform-aware path methods for Windows (ConfigDir detection, .as_posix() for TOML paths, BackendDescriptor platform-specific root_key)

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
     │                        ├─ Config module (client detection) ├─ PortResolver (dual-port)
     │                        ├─ Update checker (GitHub API, v0.47.1) ├─ SetupWizard (3-screen, 9 backends, AiConfigScreen fallback)
     │                        ├─ Config TOML merger (v0.42.0)    ├─ UpdatesPage (changelog viewer)
     │                        ├─ GIT_INSTALL_URL constant        ├─ AiToolCardFactory (platform paths)
     │                        └─ Heartbeat (15s, reconnect)      ├─ Guards (compile/play/runtime/tool)
     │                                                           └─ Python resolver (venv/uv/system)
```

### Почему такая архитектура

- **Python MCP**: Claude Code запускает через stdio, зрелый SDK
- **TCP socket**: Переживает domain reload Unity (vs WebSocket)
- **Binary framing**: 4 байта длины BE + JSON, минимальный overhead
- **No cache**: All calls go directly to Unity via bridge.send (scene changes too frequently)

### Components

1. **MCP Server** (Python: 80+ modules total, including `server.py`, 23 tools modules + support, v0.42.0: +25 config/TOML tests, v0.47.1: +73+78 config validation tests)
   - **99 core MCP tools registered** (v0.50.3+). Gating: TIER1=41 core (hardcoded). External plugins can add more tools dynamically. `_UnstructuredMCP(FastMCP)` subclass forces `structured_output=False` on all tools.
   - **Config Module (v0.42.0+v0.44.0)**: `server/src/unity_mcp/config/` extended with TOML merger for Codex backend. `merge_toml_mcp(path, section)` merges MCP config into TOML with diff-based updates (preserves user settings). Python 3.9 compat: `Optional[X]` instead of `X | None`. ValueError raised on corrupt JSON. **Stale entry cleanup (v0.44.0)**: strips `[mcp_servers.unity]` duplicates on first write, creates .bak backup. **v0.47.1**: `validator.py` skips json.loads for TOML clients, checks string presence in configs. Adds 25 new tests (v0.42.0) + 9 new tests (v0.44.0) + 151 new tests (v0.47.1: 73 Python + 78 C# in test_config_gaps.py)
   - **CodeExecutor.SecurityScan (v0.31.0)**: Hardened pipeline — (1) strip C# comments via regex (2) whitespace densification (3) OrdinalIgnoreCase matching (4) 11 new blocked patterns (EditorApplication.Exit, Application.Quit, Environment.FailFast, ExportPackage, ImportPackage, OpenProject, ProjectWindowUtil, using-aliases for System.IO/Diagnostics/Net/Reflection)
   - **In-Unity Chat Backends** (v0.29.2+): Five CLI providers with auto-discovery via TypeCache:
     * **ClaudeBackend** — Claude CLI with --permission-prompt-tool, MCP elicitation, stream-json protocol
     * **CodexBackend** — Codex CLI (no permission prompts), experimentalApi: true, tool/requestUserInput support
     * **AntigravityBackend** (v0.41.0) — Antigravity LLM service with plain-text output, EofSentinel injection on process finish
     * **KimiBackend** (v0.34.0) — Kimi K2 CLI with role-based NDJSON protocol (system→user→assistant), model autoconfig via ~/.kimi-code/models.json, binary resolver sources kimi PATH via zsh -lic
     * **OpenCodeBackend** (v0.34.0) — OpenCode CLI with multi-provider model selection, stream-json protocol
   - Transport: stdio (default) or streamable-http (`UNITY_MCP_TRANSPORT=http`)
   - `_UnstructuredMCP(FastMCP)` subclass: overrides `add_tool()` to force `structured_output=False` on all 99 registered tools, eliminating duplicate `content` + `structuredContent` in responses + `outputSchema` from ListTools (v0.50.3). Reduces MCP response size & Claude parsing overhead.
   - Lifespan: auto-discover Unity port from `~/.unity-mcp/ports/*.port`, acquire exclusive PID lockfile, create ConnectionSlot, connect bridge, fetch disabled tools cache (`get_disabled_tools`), push Python-authoritative catalog (`_push_catalog`), start heartbeat, register reconnect callbacks, load_plugins()
   - **MCP SDK Version (v0.31.0, v0.50.3)**: Pinned `mcp>=1.28.0,<2` — v2.0 ships 2026-07-28 with breaking changes (e.g., `response.content` structure). Upper bound prevents silent breakage. v0.50.3: bumped to 1.28.0+ for structured_output support.
   - Plugin system (3-source discovery: pkgutil built-in, entry_points, UNITY_MCP_PLUGIN_DIRS env): each plugin has `register(mcp, send_fn, args_fn)`. UNITY_MCP_SKIP_PLUGINS env (comma-separated prefixes) skips matching plugins.
   - _send() helper: sends to bridge via slot, raises ToolError on !ok
   - File-based output: checks `file` field in response → returns path string
   - Tool annotations: readOnlyHint, destructiveHint for MCP compliance
   - Dynamic tool filtering: patches `mcp._mcp_server.request_handlers[ListToolsRequest]` with gating + disabled-set subtraction (hide-disabled-set model, not allowlist)

2. **TCP Bridge** (Python: `bridge.py` + `bridge_heartbeat.py` + `bridge_reload_state.py` + `connection_slot.py` + `lockfile.py` + `compile_state.py` + `server_filtering.py`)
   - **ConnectionSlot**: dual per-project connections (CLI main + Chat agent-only) with project-based discovery
   - **Port Discovery** (`server_filtering.py:read_unity_port`, v0.23.0, v0.36.0 chat-port fallback): CWD-based project matching → ~/.unity-mcp/ports/*.port files (or *.chat-port when UNITY_MCP_CHAT=1) → env UNITY_MCP_PORT → default 9500. **v0.23.0: TCP probe** filters stale discovery files (port written but not listening). **v0.36.0: Windows chat-port fallback** — when chat subprocess sets UNITY_MCP_CHAT=1 env var, reads *.chat-port files (written by C# MCPServer) instead of *.port. Candidates ranked by project path match (CWD), then mtime. PermissionError (cross-user processes) skipped gracefully, live .port files preserved.
   - **Port Persistence (v0.35.0, v0.36.0 chat-port)** — PortResolver discovery chain: env UNITY_MCP_PORT → ProjectSettings/MCPSettings.json (user intent, survives Library purge) → Library/MCP_Port.json (cache) → FindFreePort. MCPServer.cs calls SaveProjectSettings() to persist both main + chat port assignments at startup. **v0.36.0: MCPServer.WritePortFile()** now writes both {pid}.port (main) and {pid}.chat-port (chat) when dual ports active. DeletePortFile() cleans both. Backward compatible: nil ProjectSettings falls through to Library cache.
   - **Fail-Fast Lockfile** (`lockfile.py`): RuntimeError raised on live process (instead of SIGTERM) to let Python server handle reconnection logic cleanly. **v0.23.0: Zombie detection** — `_is_zombie(pid)` check prevents treating defunct processes as "live", allowing fast server startup without waiting for cleanup.
   - **UnityBridge (v0.36.0)**: AsyncIO TCP client, 4-byte BE length prefix JSON
     * **BridgeState enum**: DISCONNECTED | CONNECTED | DOMAIN_RELOADING | FAILED (startup grace expired)
     * **DomainReloadTracker** (`bridge_reload_state.py`, v0.36.0): Tracks domain reload state independently from compile probe (30s expiry). Shared between bridge.send() and heartbeat via `_reload` instance. Three methods: `mark()` (on DomainReloadError), `clear()` (on success), `is_active()` (checks expiry). Decouples reload window from compile heuristics.
     * **should_retry()** (`bridge.py`, v0.36.0): Pure decision function invoked by _send_with_retry on error. Returns (should_retry: bool, delay_s: float, reason: str). Logic: (1) check attempt count/deadline, (2) on DomainReloadError mark reload + state→DOMAIN_RELOADING, (3) on any error check reload.is_active() or probe_busy(), backoff 2^attempt ≤ 8s. Extracted from inline retry logic for testability + clarity.
     * **Atomic reader/writer swap** (v0.36.0): In _reconnect(), both reader and writer closed atomically within lock to prevent zombie reads after close. Fixed CancelledError cleanup.
   - Socket: TCP_NODELAY, SO_KEEPALIVE (idle=60s, interval=10s, count=3 on macOS/Linux)
   - **Heartbeat**: 15s interval, raw ping, 3 consecutive failures → close, 2s polling when disconnected (5s when busy). Sole reconnect mechanism.
   - **Port Re-Discovery on Reconnect (v0.24.1)** — `UnityBridge` accepts optional `port_discoverer` callable (typically `read_unity_port`), invoked during `_reconnect()` before TCP connect to detect if Unity moved to a new port. If discoverer returns different port, bridge updates `_port` and recreates CompileStateProbe. Gracefully handles discoverer exceptions (falls back to current port). ConnectionSlot threads discoverer through and adds `_sync_port()` callback to sync port back to slot + trigger server-side lockfile swap (`_on_port_change`). Backward-compatible: no discoverer → normal reconnect.
   - **CompileStateProbe**: heuristic compile/domain-reload detector (state file, PID check)
   - **DomainReloadError**: on Unity `going_away` event → immediate close + busy flag. Heartbeat now calls `_reload.mark()` on DomainReloadError (v0.36.0) to extend retry window in send()
   - **PID Lockfile**: `~/.unity-mcp/server-{port}.lock`, **cross-platform locking**:
     * **macOS/Linux**: `fcntl.flock` (advisory, whole-file lock)
     * **Windows**: `msvcrt.locking` on sentinel byte at offset 1024 (non-blocking, avoids mandatory lock of PID data at bytes 0-31)
     * Kills stale servers: SIGTERM→SIGKILL (Unix), TerminateProcess (Windows)
     * **v0.23.0: Zombie detection** via `_is_zombie()` prevents stale defunct processes from blocking reconnection
   - **SIGPIPE handling**: guarded with `hasattr(signal, "SIGPIPE")` since Windows lacks SIGPIPE. Suppressed on Unix to prevent server crash on client disconnect.
   - **Reconnect (v0.30.3)**: cooldown MIN_RECONNECT_INTERVAL=5s (was 2s), heartbeat debounce=30s (was 5s). send() reconnect no longer fires callbacks (only heartbeat does) — breaks reconnect feedback loop. push_catalog skips if already locked.
   - Max message: 10MB, timeouts: 30s default, 60s compile_preflight/batch, 120s run_tests/run_playtest/fuzz_playtest

3. **Unity Plugin** (C#: 160+ files, ~16500 LOC, v0.42.0: Wizard asmdef split, Updates folder, MarkdownInlineFormatter extraction, v0.44.0: LevelUp UX, v0.45.0: InstallSourceDetector + async updaters)
   - **MCPServer.cs**: Dual TCP listeners (main port 9500-9599 + chat port auto-assigned, separate), 4-byte BE framing, 10MB max, SO_KEEPALIVE, **v0.23.0: SO_REUSEPORT** (macOS/Linux) for rapid reconnect recovery, auto-assigns free ports via `PortResolver.FindFreePort()`, persists to Library/MCP_Port.json, state file (`ready`/`compiling`/`reloading`), `going_away` event before domain reload, ClientSlot pattern isolates CLI and Chat connections. **v0.37.0: IsReallyCompiling** — managed flag replaces EditorApplication.isCompiling latching, 120s wedge guard prevents false-positive "backgrounded" state. **v0.36.0: WritePortFile** writes both {pid}.port (main) + {pid}.chat-port (Windows env fallback)
   - **PortResolver.cs**: Pure testable helpers (ResolvePort, ResolveChatPort, FindFreePort, SavePorts, IsValidPort, ParsePortFromJson) with 25 NUnit tests. Validates 1024–65535 range, skips reserved ports, fallback to OS-assigned via port 0
   - **CommandRouter.cs**: RegisterAll() → calls core commands + PluginRegistry.RegisterAllPlugins() for external plugins, data-driven IsMutatingCommand/IsRuntimeCommand. **v0.37.0: DefaultIsCompiling** — two-layer check (IsReallyCompiling + 120s wedge guard) prevents false-positive compile blocks.
   - **PluginRegistry.cs**: Static registry for IMCPPlugin implementations. Plugins register via `[InitializeOnLoad]`. One-way asmdef dependency: external → public.
   - **IMCPPlugin.cs**: Interface — Name, CommandPrefix, RegisterCommands(), OnDomainReload(), AdditionalCommands
   - **CommandRegistry.cs**: Func<string,string> handlers, mutating + runtime flags
   - **CommandSchema.cs**: parameter validation with fuzzy did-you-mean suggestions (79 schemas)
   - **ValueParser.cs**: vectors, quaternions, colors, arrays, 100+ types (Rect/Bounds/RectInt/BoundsInt/LayerMask + Int64/Double precision), type-aware SetPropertyValue
   - **InputNormalizer.cs**: component/property/value normalization
   - **BatchHelper.cs**: multi-command text parser + executor (on_error=continue/stop). **v0.37.0:** testable IsCompiling seam via CommandRouter (supports reload-latch testing)
   - **7 Serializers**: HierarchySerializer (tree, MAX_NODES=3000, incremental, summary), ComponentSerializer (key-value, UnityEvent expansion, PrefabStage-aware, **v0.23.0: #instanceID in all path tools**), AnimationSerializer, TimelineSerializer, AnimatorControllerSerializer, ParticleSerializer, ShaderSerializer
   - **ScenePathParser (v0.31.0)**: Shared struct for multi-scene path parsing (`"SceneName:/"` prefix extraction). Used by SceneObjectFinder + ComponentSerializer.Finder. Replaces inline string parsing, prevents multi-scene reference bugs.
   - **ObjectManager (v0.23.0 fixes)**: Properties.cs auto-redirects `set_property("active")` to SetActive. Lookup.cs adds FindType + short-name fallback for custom components.
   - **FileOutputHelper (v0.23.0)**: ScreenshotsDir now `<ProjectRoot>/ScreenShots/` (project-local, not shared cache)
   - **RefManager**: short refs $a-$zz (702 slots), invalidated on scene change
   - **ErrorHelper**: contextual errors with did-you-mean hints
   - **RegionTool (v0.46.0, new)**: Interactive Scene View polygon region selection for level design
     * **Polygon2D**: Immutable 2D polygon (XZ plane), winding-number PIP test, AABB bounds, CSV import/export, RDP simplification
     * **SceneRegionTool**: EditorTool with multi-mode FSM (Lasso/Rectangle/Circle/PointByPoint), keyboard shortcuts (Shift+R activate, Q/W/E/R mode switch, G grid snap, Enter commit, Esc cancel)
     * **SceneRegionQuery**: 3-stage spatial pipeline (AABB pre-filter → component filter → PIP test → cap+format), GameObject[] array result
     * **SceneRegionState**: LRU registry (8 slots) + EditorPrefs persistence, CSV export for later use
     * **Drawing Modes** (IDrawingMode interface): LassoMode (free-form), RectangleMode (orthogonal), CircleMode (radius), PointByPointMode (manual vertices). Each mode tracks active state, completion, grid snap tolerance. DrawingUtils shared snapping logic.
     * **Rendering**: RegionRenderer (GL wireframe + fill), RenderStyle (color/alpha), RenderState (active/preview/committed). UIToolkit SceneRegionOverlay for UI elements.
     * **PolygonDetail**: Detail level presets (High/Medium/Low), per-preset RDP threshold, EditorPrefs toggle
     * **Chat Integration**: RegionChipProvider for region selection in chat (selectable from dropdown, persists across turns)
     * **Tests**: 104 C# NUnit tests (Drawing modes, Rendering, state management)

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

7. **In-Unity Chat Session Control (v0.19.0, F20–F30, v0.36.0 timeout messaging)**
   - **Stop button (F20)**: CancelTurn() method in MCPChatWindow + backend handlers. Sends `{ "stop_reason": "end_turn" }` to Claude stdin or terminates Codex process. Esc hotkey also triggers cancel. Button UI swaps from Send→Stop during streaming.
   - **Timeout Context Hints (v0.36.0)**: When turn exceeds InactivityTimeoutSec (300s for Codex, 90s for Claude), failure message now includes last tool name: `[Timed out: no response for 300s (last tool: set_property)]`. Helps debug what operation was in-flight. Tracked via `_lastToolName` in EventHandlers.cs.
   - **Dead-Process Guard Message (v0.36.0)**: When backend process unexpectedly exits mid-turn, appends `[Process exited]` to transcript before finalizing. Surfaces connection loss (vs. timeout) as distinct error state. Clears turn flags to unlock reload.
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
   - **Asset Export/Import Enhancements (Server v0.8.2, Plugin v0.35.0)**: `export_package` gains `include_deps` parameter (default true) — skip dependencies if false for token optimization on large packages. `import_package` now returns manifest: list of imported asset paths. **AssetDatabaseHelper.cs extended** with dependency filtering + import result tracking. **Tests**: 6 new test_server_asset.py scenarios.
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

## Reload Stability (v0.42.0, commit 39672a0)

Root cause: v0.42.0 asmdef split (7→9 assemblies) amplified 3 latent bugs into crashes and stale DLLs. Addressed via 13 surgical fixes + 39 regression tests.

### Crash Prevention

1. **Socket Poll Freeze (MCPStatusWindow)**: OnDisable now stops Socket.Poll() cleanly during domain reload (was blocking main thread indefinitely)
2. **Reload Port Leak (ReloadMiniServer)**: Client tracking + graceful close on Stop prevents fd exhaustion + reload freeze
3. **Window Layout Crash**: [MovedFrom] attributes on MCPStatusWindow/MCPSettings/etc moved across assemblies (Editor assemblies moved to Wizard, was losing window layout)
4. **Use-After-Free (TeardownCore)**: Drains `_mainThreadQueue` before domain unload (was referencing deallocated memory post-domain)
5. **Tundra Digest Cache**: Removed unconditional deletion of digestcache (SIGABRT in RegisterAssemblyDefinition during reload)

### Stale DLL Detection Pipeline

**ComputeStamp (compile_state.py)**: Now iterates all UnityMCP.* assemblies (was checking only one assembly, blind to breakage in other .dlls)
- Detects stale .mvfrm files via MVID comparison (per assembly)
- Aggregates into single STALE verdict if ANY assembly diverges

**ReloadGuard (C# CLI assembly)**: 
- Constructor barrier calls `AssetDatabase.Refresh()` on init
- `ForceUnlock()` triggers additional `AssetDatabase.Refresh()` + `RequestScriptCompilation()`
- Exception-safe: asymmetric lock rollback in `OnTurnStarted` to prevent deadlock

**PID Liveness Check (lockfile.py)**: 
- Port file discovery now verifies process is alive via `_is_zombie(pid)` check
- Blocks stale PID lockfiles from ghosting commands (fast server restart without wait)

**TCP Probe in is_startup_in_progress (server.py)**:
- False "Unity busy" detection fixed by probing TCP port during startup detection
- Distinguishes real startup grace from transient disconnect

### Reload Timing

**DOMAIN_RELOAD_EXPIRY_S**: Increased 30s → 90s for 9-assembly reload window
- v0.42.0 increased assembly count 7→9 (main, Chat CLI, Chat View, Wizard, Reload, reload tests, Chat tests, Wizard tests)
- Longer window accommodates full serialization + reload + recompile cycle
- Bridge heartbeat retry logic uses this window to avoid false "domain stuck" timeouts

**_DISCONNECT_WINDOW_S**: Also 90s (synchronizes with reload window)

### Asmdef Isolation

**Wizard.asmdef** (v0.42.0): `autoReferenced: false` enables independent compilation when core/Chat broken
- Prevents Wizard compile errors from blocking MCP startup (diagnostic UI still available even during crashes)

### Test Infrastructure (39 regression tests, commit 39672a0)

**Python** (`test_reload_stability.py`, 300 LOC):
- ComputeStamp multi-assembly detection (test_compute_stamp_detects_stale_in_any_assembly)
- PID liveness fallback (test_port_discovery_skips_zombie_pids)
- TCP probe avoids false startup detection (test_is_startup_probes_tcp)
- DOMAIN_RELOAD_EXPIRY edge cases (test_domain_reload_expiry_90s_holds)

**C#** (145 LOC across 3 test files):
- ReloadMiniServerTests: client tracking, graceful shutdown (85 tests)
- ReloadGuardTests: exception safety, ForceUnlock flow (98 tests)
- MCPStatusWindowSchedulerTests: OnDisable stop behavior (37 tests)
- MovedFromAttributeTests: layout crash isolation (25 tests)
- ReloadStabilityTests (Wizard, Editor): full pipeline integration (91 tests)

**Verification**: 39 new regression tests all green on macOS/Windows (domain reload stress: 100+ recompile cycles)

## Level Design Toolkit (v0.46.0+, F1-F5)

**Chat-Integrated Visual Tools:**

1. **F1: Token Counter + Context Progress Bar** (replaces USD cost display)
   - **ModelContextWindows.cs** — Context window size per LLM (hardcoded: Claude 200k, Opus 4.8 vs 4.6/4.7, Haiku 100k, Sonnet 400k, Codex/Gemini preset fallback)
   - **TokenFormat.cs** — Extended `FormatReadout()` displays `↑input ↓output | ▓▓▓▓░░░░░░ 40%` (input+output count + progress fill as Unicode bar)
   - **ContextProgressBar.cs** — UIToolkit visual bar (50px height, animated fill on token change, responsive layout)
   - **TokenResetTests** — Verify counter resets on backend/model/inactivity-timeout switch

2. **F2: Component Field Chips** — Right-click Component header in Inspector → "Attach Field" dropdown
   - **FieldChipProvider.cs** — Chip provider for individual component fields (priority 200, between Script and Scene)
   - **FieldContextMenu.cs** — Inspector context menu listener, routes field selection
   - **ChipKindKeys.cs** — New ChipKind: `Field` + `AnnotatedScreenshot` (supports v0.46 annotation flow)
   - **FieldChipProviderTests, FieldContextMenuTests** — Full menu + selection flow coverage

3. **F3: Native Screenshot Button + Chip**
   - **ScreenshotService.cs** — Wrapper around existing ScreenshotCapture, captures camera view to file
   - **ScreenshotToolbarButton.cs** — Toolbar button (📷 icon), OnClick calls ScreenshotService, emits chip + injects into chat
   - **ScreenshotServiceTests, ScreenshotToolbarButtonTests** — Service + button lifecycle

4. **F4: Full Annotation Editor** (Annotation/ folder, 11 files)
   - **AnnotationCanvas.cs** — Drawing surface (Texture2D-backed, pixel-level rasterization)
   - **AnnotationCommand.cs** — Command pattern: pen/line/arrow/rect/ellipse/text/erase (base class + 7 subclasses)
   - **AnnotationHistory.cs** — Undo/redo stack (command list, index tracking)
   - **AnnotationToolState.cs** — Active tool + brush color/size state (mutable, live-updated)
   - **AnnotationToolbar.cs** — Tool palette + color picker + undo/redo buttons (UIToolkit buttons)
   - **AnnotationEditorWindow.cs** — EditorWindow host (canvas + toolbar side-by-side)
   - **AnnotationRasterizer.cs** — Rasterize commands to Texture2D (line bresenham, circle/ellipse scanline fills)
   - **AnnotationDrawer.cs** — Preview command strokes (GL lines, circles, text)
   - **AnnotationCompositor.cs** — Flatten command stream to final PNG (rasterize all + encode)
   - **AnnotationIcons.cs** — Procedural vector icons for toolbar buttons + RegionTool overlay (230 LOC: Pen/Line/Arrow/Rect/Ellipse/Text/Erase/Save icons via Painter2D)
   - **AnnotateToolbarButton.cs** — Chat toolbar button to launch AnnotationEditorWindow
   - **AnnotatedScreenshotChipProvider.cs** — Chip kind for annotated images (markdown `![](path.png)` with annotation metadata JSON)
   - **Tests**: 10+ NUnit test files covering all components (canvas rasterization, undo/redo, metadata serialization)

5. **F5: Raycast World Coordinates** in Annotation Metadata
   - **AnnotationRaycaster.cs** — Scene raycast from mouse position + camera (returns world XYZ + GameObject + hit distance)
   - **AnnotationMetaWriter.cs** — Embeds raycast hits into annotation metadata JSON (for chat reference: "annotated pixel at world 15.2, -3.5, 42.1 on Player")
   - **Tests**: AnnotationRaycasterTests (228 cases), AnnotationMetaWriterTests (64 cases) covering raycast edge cases + metadata serialization

6. **Region Icons** (RegionIcons.cs, moved to RegionTool/Rendering/)
   - Procedural Painter2D vector rendering for Lasso/Rect/Circle/PbP tools + overlay UI
   - Replaces hardcoded icon assets, resolves v0.46 black-flash issue

7. **Region hasFocus Guard** (RegionRenderer.cs)
   - Prevents black GL rendering flash when Scene View loses focus
   - Checks EditorGUIUtility.editingTextField to hide region overlay during text input

8. **Chip Thumbnails** (ChipPillFactory.cs)
   - Inline thumbnail previews (32x32px) for image chips in both input and response
   - Lazy-load from ScreenShots/ directory, fallback graceful if file missing

9. **Configurable Inactivity Timeout**
   - Moved from hardcoded 90s (Claude) / 300s (Codex) to **BackendConfigStore** (default 180s)
   - **ChatSettingsSection** → General → Inactivity timeout slider (30–600s)
   - Persists to Library/MCP_ChatBackendConfig.json, per-backend override available

## Tool Categories

**Update v0.30.4**: validate_move added to asset category (6 tools total). Test marker `live_haiku` → `live_cli` (v0.8.2+).

**Update v0.46.0**: ChipKind registry now includes Field + AnnotatedScreenshot. ModelContextWindows presets added per LLM.

### TIER1 (always visible, 41 core)

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
- **ClaudeBackend** (ported onto base): Zero behavior change (−65 lines net). Persistent stdin loop (IsPersistentProcess=true), Claude NDJSON parser, `--resume <sessionId>` argv builder. Uses `ChatMcpConfigWriter.GetOrCreateConfigPath()` to generate temporary JSON config + `--mcp-config <path>` flag. No `--strict-mcp-config` (v0.38.0) — Claude CLI automatically merges our MCP config with user's `~/.claude/` servers (Blender MCP, luna-kiss-mcp, etc.).
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
- **Wiring:** `ClaudeArgBuilder` + `CodexArgBuilder` + `GeminiArgBuilder` + `KimiArgBuilder` read from store, inject into argv construction
  - **ClaudeArgBuilder.cs** (v0.38.0) — builds `claude` subprocess argv with stdin pipe + MCP config path. No `--strict-mcp-config` — allows Claude to merge with user's `~/.claude/` MCP servers
  - **GeminiArgBuilder.cs** (v0.38.0) — builds `gcloud` argv; `RewriteWithFreshMcp()` merges config by replacing only the "unity-mcp" entry, preserving other user servers
  - **KimiArgBuilder.cs** (v0.38.0) — builds `kimi` argv; `WriteMcpConfig()` merges instead of full-overwrite, preserving user's other MCP servers
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

### @Mention Autocomplete (v0.41.4)
- **MentionTokenParser.cs** — Pure static backward scan from cursor to find `@` prefix + alphanumeric query. Allocation-free, handles multi-word paths.
- **MentionFuzzyScorer.cs** — Allocation-free fuzzy scoring with 26-bit bitmask pre-filter (early exit for impossible matches). Scores by word-boundary match > positional match > character count.
- **SceneMentionIndex.cs** — Hierarchy index with VersionTracker dirty-flag tracking. 3000-entry cap (same as HierarchySerializer.MAX_NODES). Auto-rebuild on scene changes.
- **AssetMentionIndex.cs** — Asset database index via OnAssetsChanged. Caps asset count. Implements IDisposable for cleanup on domain reload.
- **RecentMentionSource.cs** — Selection.activeGameObject + 2000-point score boost. Always suggests last-selected object.
- **MentionCoordinator.cs** — Merges sources, dedup by path (set uniqueness), sort by score desc, cap at maxResults (typically 8).
- **MentionPopup.cs** — UIToolkit ScrollView popup (focusable=false for input field focus). Max 8 rows visible, keyboard-navigable (arrow keys, Enter select, Esc dismiss).
- **MCPChatWindow.Mention.cs (partial)** — Debounce 100ms on text change. On @query match: show popup. Keyboard intercept (Up/Down/Enter/Esc). Blur → dismiss.
- **InlineChipField.ReplaceMentionRangeWithChip** — Delete @mention text, insert chip at cursor with proper spacing. Offset tracking post-replacement.
- **6-layer modular design:**
  ```
  User types "@Ca" → ChangeEvent → MentionTokenParser → MentionCoordinator
  → [SceneMentionIndex, AssetMentionIndex, RecentMentionSource] → MentionFuzzyScorer
  → merge/dedup/sort → MentionPopup.Show() → user selects → ReplaceMentionRangeWithChip → ChipData
  ```
- **Tests:** 72 NUnit tests (MentionTokenParserTests, MentionFuzzyScorerTests, SceneMentionIndexTests, AssetMentionIndexTests, MentionCoordinatorTests, MentionPopupTests, MentionIntegrationTests, MentionPerfTests, MentionEdgeCaseTests)
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

### Python Tests: 2450 unit tests + 78 live integration tests + 4 live CLI tests
- Default: `PYTHONWARNDEFAULTENCODING=1 pytest -m "not live" -q` — unit tests, $0 cost (2450 tests, includes test_llm_config.py + test_ask.py + intent tests)
- With Unity: `PYTHONWARNDEFAULTENCODING=1 UNITY_MCP_PORT=<port> pytest -m "live and not live_cli" -q` — 78 live integration tests, $0 cost (requires Unity running, sampling disabled)
- Real CLI: `PYTHONWARNDEFAULTENCODING=1 UNITY_MCP_PORT=<port> UNITY_MCP_VISUAL_VERIFY=1 pytest -m "live_cli" -v` — 4 real CLI tests, ~$0.001/call (requires Unity + claude CLI, visual verification enabled)
- Test order: unit → C# EditMode → C# PlayMode → live integration → live_cli (live/live_cli always last, occupy TCP)

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
- `server/src/unity_mcp/bridge.py` — UnityBridge TCP client, should_retry() decision logic, BridgeState enum (DISCONNECTED|CONNECTED|DOMAIN_RELOADING|FAILED)
- `server/src/unity_mcp/bridge_heartbeat.py` — HeartbeatMixin loop (15s ping, 2–5s reconnect polling, startup grace deadline)
- `server/src/unity_mcp/bridge_reload_state.py` — DomainReloadTracker dataclass (30s expiry, marks domain reload state shared with send())
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

## v0.42.0 Features: Setup Wizard (3-Screen Flow, 9 Backends), Updates Hub, Asmdef Split

**Setup Wizard Redesign (v0.42.0)**
- **3-Screen Flow** replacing 4-screen model:
  1. **Welcome Screen** — System checks (Python found ✓, TCP available ✓), intro text
  2. **PickBackend Screen** — 9 backend cards: Claude Code, Claude Desktop, Cursor, Windsurf, VS Code, Codex, Kimi, OpenCode, Antigravity. Auto-detection via `IsDetected` (BinaryName PATH lookup via which/where + ConfigDir ~ expansion + exists check)
  3. **Configure Screen** — Scope toggle (Global/Project via install.py `--project-dir` flag), per-backend setup instructions
- **BackendDescriptor.cs** (v0.42.0) — Sealed class with static `All[]` array. Fields: Key, DisplayName (UI name), Icon (Unicode symbol), Description, InstallMechanism enum (PythonConfig/CliCommand/ChatAuto), BinaryName (for PATH detection), ConfigDir (for existence check). IsDetected logic: checks Process.Start() which/where (Windows/Unix) for BinaryName, expands ~ and checks Directory.Exists(ConfigDir)
- **ConfigureScreen.cs** (228 LOC) — Scope toggle buttons (Global/Project), passes `--project-dir` flag to install.py. Backend-specific setup cards with instructions per mechanism
- **PickBackendScreen.cs** (156 LOC) — Grid of 9 backend cards using AiToolCardFactory.Build(), auto-detection badge, click→navigation to ConfigureScreen
- **WizardScreenHost refactored** (v0.42.0) — Updated to support 3-screen sequence (was 4), smooth transitions
- **SetupDiagnostics.cs** (16 LOC) — Moved to Wizard/ folder, extracted diagnostic checks (Python detection, TCP test)
- **Tests** — 5 new test files in `Wizard/Tests/`:
  - BackendDescriptorTests (89 tests): IsDetected logic, all 9 backends, mock PATH/ConfigDir
  - ConfigureScreenTests (127 tests): Scope toggle, per-backend instructions, navigation
  - PickBackendScreenTests (97 tests): Card rendering, auto-detection badge, click behavior
  - SetupDiagnosticsTests (existing, moved)
  - (5 test files total: AiToolCardFactoryTests also moved)

**Updates Hub + Changelog Viewer (v0.42.0)**
- **UpdatesPage.cs** (80 LOC) — New Hub page (registered via SettingsPageFactory):
  - "Check for Updates" button (disabled during check, 3s cooldown)
  - Changelog area with foldout entries per version (IsNewer versions expanded by default, colored background)
  - Uses ChangelogReader.Parse() to extract entries + MarkdownInlineFormatter.ToRichText() for markdown rendering
- **ChangelogReader.cs** — Parses CHANGELOG.md:
  - Returns `List<ChangelogEntry>` (Version, Date, Content, IsNewer)
  - Locates CHANGELOG.md via ChangelogReader.LocatePath() (Assets/ relative path)
  - Content (markdown) rendered via MarkdownInlineFormatter
- **UpdateBanner.cs** — Existing banner UI (already in place, no changes)
- **UpdateChecker.cs** — Existing PyPI poller (already in place, no changes)
- **MarkdownInlineFormatter.cs** (59 LOC) — NEW, extracted to Editor/ base assembly (v0.42.0):
  - Pure static method `ToRichText(span)` → Unity rich-text
  - Patterns: `**bold**`, `*italic*`, `_underline_`, `` `code` ``, `[link](url)`
  - Uses Unicode non-characters (﷐/﷑) for collision-proof code-span placeholders
  - Reused by UpdatesPage (changelog rendering) and MarkdownInline (Chat assembly, delegates here)
- **MarkdownInlineFormatterTests.cs** (66 tests) — Unit tests for all markdown patterns
- **UpdatesPageTests.cs** (60 tests) — Changelog rendering, update check button behavior

**Wizard Assembly Split (v0.42.0)**
- **UnityMCP.Editor.Wizard.asmdef** — New separate compile unit, references: Editor (core). Enables Wizard to compile independently if core/Chat broken
- **UnityMCP.Editor.Wizard.Tests.asmdef** — Parallel test assembly, references: Wizard + Chat (for integration tests), Test assembly
- **Moved to Wizard/**:
  - SetupWizard, WizardScreen, WizardScreenHost, WizardAnimUtils, WizardAssemblyInfo
  - SetupDiagnostics (was at root)
  - MCPDiagnosePanel, MCPDiagnoseWindow, MCPStatusWindow (diagnostic windows)
  - AiToolCardFactory (reusable card builder, also used by PickBackendScreen)
  - Tests: SetupWizardTests, SetupDiagnosticsTests, WizardAnimUtilsTests, AiToolCardFactoryTests
  - (new) BackendDescriptorTests, ConfigureScreenTests, PickBackendScreenTests, Screens/ folder

**Python Config TOML Merger (v0.42.0)**
- **merger.py extended** — `merge_toml_mcp(path, section_name)` for Codex backend (TOML config support)
- **Merge logic**: Preserves user settings, upserts only MCP entry (diff-based approach)
- **Python 3.9 compat** — `Optional[X]` instead of `X | None` for Union types
- **Error handling** — ValueError raised on corrupt JSON (instead of silent fail)
- **Tests** — 25 new tests in test_config_module.py covering TOML merger edge cases

**Tool Input Click Router (v0.41.9 → v0.42.0)**
- **ChipClickRouter** — DRY pattern for chip click handling (input field + response)
- **Scope**: Hyperlink navigation, inline chip interactions
- **Tests** — InputChipClickTests.cs (199 tests) for input chip interactions

**NUnit Test Count (v0.42.0)**
- **Total: 3908 EditMode + PlayMode** (was ~2900), +1000+ new tests across Wizard, Chat, Updates, Markdown modules
- **Green: 3908** (4 pre-existing reds in unrelated areas)

## v0.44.0 Features: Arcade Level Up UX, Codex Config Hardening

**Arcade LevelUp Celebration Panel (v0.44.0)**
- **LevelUpPanel.cs** — 4-state machine: Idle (waiting) → Animating (bar fill + sparkles) → Done (completion badge) → Diff (release notes)
- **LevelUpAnimator.cs** — Progressive XP bar with AnimationCurve, particle sparkles via Instantiate
- **ReleaseDiff.cs** — Parse CHANGELOG.md, extract version entries, compute release notes diff (version A→B)
- **LevelUpAnim.uss** — Complete animation stylesheet (bar fill, particle, badge emerge, slide-out diff panel)
- **UpdatesPage integration** — Swapped UpdateBanner → LevelUpPanel (conditional on version change)
- **Tests** — LevelUpTests.cs (12 tests): state transitions, animation timing, release diff parsing

**Codex Config Cleanup & Backup/Restore (v0.44.0)**
- **Python side**:
  - **merger.py** — Strips stale `[mcp_servers.unity]` duplicates on first write (idempotent safety)
  - Creates `.bak` backup before modifications (first-write-wins, manual restore via WizardConfigWriter)
  - **install.py doctor** — Warns about stale Codex entries (diagnostic hint)
- **C# side**:
  - **WizardConfigWriter.cs** — `HasBackup()` detection + `RestoreConfig()` recovery method
  - **AiConfigScreen.cs** — Restore button in UI (manual rollback on config corruption)
  - **Tests** — WizardConfigWriterTests.cs (9 tests): backup creation, restore logic, merge safety

**Stability & Bug Fixes (v0.44.0)**
- **ReloadMiniServer.cs** — Fixed CS1503 (explicit TcpClient variable, C# type inference)
- **HelperTests.cs** — Removed MCPServer.Stop() from test teardown (was killing TCP prematurely)

**NUnit Test Count (v0.44.0)**
- **Total: 3945 EditMode + PlayMode** (was 3912), +33 new tests (12 LevelUp + 9 Config + 12 misc)
- **Green: 3945** (5 pre-existing reds, same as v0.42.0)
- **Python pytest: 2606** (was 2597, +9 config merger tests)

## Related

- Skills: `.claude/skills/`
- Changelog: `CHANGELOG.md` (root, single source of version history)
