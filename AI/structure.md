# Project Structure (Current)

```
unity-kiss-mcp/
├── server/                     # Python MCP Server (2362 unit tests, 70 live = 2432 total Python, v0.30.4)
│   ├── src/unity_mcp/
│   │   ├── server.py           # FastMCP instance, lifespan, 89 registered MCP tools
│   │   ├── bridge.py           # UnityBridge (TCP, heartbeat, SO_KEEPALIVE)
│   │   ├── connection_slot.py  # ConnectionSlot: dual connections (CLI + Chat agent)
│   │   ├── server_filtering.py # Port discovery + TCP probe (v0.23.0), catalog push, tool filtering
│   │   ├── lockfile.py         # Cross-platform exclusive locking + zombie detection (v0.23.0)
│   │   ├── diagnose.py         # Shared diagnose parser + verdict logic (_parse_diagnose, _verdict, _DiagnoseFields)
│   │   ├── compile_state.py    # CompileStateProbe (heuristic Unity compile detection)
│   │   ├── middleware.py       # 23-layer middleware pipeline (env-gated UNITY_MCP_MIDDLEWARE=1)
│   │   ├── middleware_paths.py # PathResolverMixin extracted from middleware.py
│   │   ├── plugin_api.py      # Stable public API for external plugins (RO, RW, SamplingService, strip_fences)
│   │   ├── unity_state.py      # Unity state file reader
│   │   ├── crash_log.py        # Crash log tracking
│   │   ├── degrade.py          # Graceful degradation
│   │   ├── distiller.py        # Response distillation (Haiku)
│   │   ├── compressor.py       # Response compression
│   │   ├── clarifier.py        # Ambiguity resolution
│   │   ├── errors.py           # Error types (DomainReloadError, etc.)
│   │   ├── fuzzer.py           # Fuzz playtest generation
│   │   ├── hinter.py           # Tool suggestions
│   │   ├── inference.py        # Argument inference from context
│   │   ├── input_normalizer.py # Component/property name normalization
│   │   ├── lessons.py          # Usage pattern learning
│   │   ├── metrics.py          # Performance metrics
│   │   ├── prefetch_cache.py   # Speculative prefetch
│   │   ├── resources.py        # MCP resources
│   │   ├── llm_config.py        # LlmProfile dataclass: universal config for Claude/Codex (v0.23.0 Block 3)
│   │   ├── sampling.py         # Visual verification (DRY: uses get_profile for model selection) (v0.23.0)
│   │   ├── sampling_postproc.py # Sampling post-processing
│   │   ├── scene_brief.py      # Scene context injection
│   │   ├── schema_cache.py     # Schema caching
│   │   ├── schema_guard.py     # Pre-flight argument validation
│   │   ├── speculation.py      # Speculative prefetch layer
│   │   ├── visual_diff.py      # Visual regression diff
│   │   ├── watchdog.py         # Proactive validation watchdog
│   │   ├── ask/                # ask() tool decomposition
│   │   │   ├── router.py       # Keyword regex router — deterministic 80% of ask() questions
│   │   │   ├── plans.py        # ToolPlan dataclass + canonical plan templates
│   │   │   ├── executor.py     # Runs ToolPlan steps via _send
│   │   │   └── summarizer.py   # Bypass for short results, Haiku for complex
│   │   ├── budget/             # Cost budgeting + adaptive routing for Haiku calls
│   │   │   ├── cost_tracker.py # Track Haiku spend per session + per day, persist to disk
│   │   │   ├── registry.py     # Static feature metadata: priority, difficulty, token estimates
│   │   │   ├── router.py       # Adaptive routing: skip/run based on budget + priority
│   │   │   └── _filelock.py    # Cross-process file lock via fcntl for budget.json
│   │   ├── do_intent/          # do() tool decomposition — NL intent to batch
│   │   │   ├── catalog.py      # Whitelist of allowed commands and signatures
│   │   │   ├── planner.py      # Haiku planner — converts intent to batch DSL
│   │   │   ├── executor.py     # Runs batch + 1 retry on partial failure
│   │   │   ├── prompt.py       # System prompt builder for planner
│   │   │   └── validator.py    # Static plan validation (max lines, forbidden commands)
│   │   ├── reflect/            # Asymmetric reflection: mutation args vs response snapshot
│   │   │   ├── rules_batch.py  # Reflection rules for batch commands
│   │   │   ├── rules_objects.py # Reflection rules for object-mutation commands
│   │   │   └── rules_runtime.py # Reflection rules for runtime/UI mutations
│   │   ├── screenshot_describe/ # Screenshot description via Haiku sampling
│   │   │   ├── describer.py    # Screenshot → text description via SamplingService
│   │   │   ├── cache.py        # Fingerprint-based description cache
│   │   │   └── prompts.py      # Prompt templates per description mode
│   │   ├── som/                # Set-of-Mark visual annotation
│   │   │   ├── overlay.py      # Pillow-based SoM overlay renderer (numbered circles, boxes)
│   │   │   ├── extract.py      # Parse and filter rects from Unity screenshot payload
│   │   │   └── diff_annotate.py # Annotate before/after images with SoM, call sampling
│   │   ├── tools/              # Tool modules (27 files + __init__, asset tool extended v0.30.4)
│   │   │   ├── __init__.py     # Tool module registry
│   │   │   ├── reload_ladder.py # Reload recovery T0-T5 ladder (MVID-delta healing proof)
│   │   │   ├── objects.py      # create/delete/find/inspect/set_parent/set_material
│   │   │   ├── scene.py        # scene, editor, screenshot, search, spatial, scan, schema
│   │   │   ├── runtime.py      # invoke_method, wait_until, move_to, run_playtest, fuzz_playtest
│   │   │   ├── batch.py        # batch, references, validate_references + _dsl_tools set
│   │   │   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   │   │   ├── skills.py       # save/use/list_skill, apply/save/list_template + _skills_dir
│   │   │   ├── spatial.py      # validate_layout, get_spatial_context, scan_scene, check_colliders, spatial_query
│   │   │   ├── ui.py           # create_ui, set_rect, menu, shader
│   │   │   ├── animation.py    # animation, timeline, animator, particle
│   │   │   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings, validate_move (v0.30.4)
│   │   │   ├── connection.py   # list_connections, reconnect_unity
│   │   │   ├── autobatch.py    # setup_objects, set_properties, configure_objects
│   │   │   ├── gating.py       # TIER1 + category-based capability filtering (permission_prompt in CORE_TOOLS, v0.29.37)
│   │   │   ├── do_tool.py      # NL intent → Haiku plan → batch execute
│   │   │   ├── ask_tool.py     # NL read-only → route → Haiku summarize
│   │   │   ├── ask_user_tool.py # ask_user MCP tool (ask_user AskUserCard routing, v0.29.11)
│   │   │   ├── permission_prompt_tool.py # permission_prompt MCP tool (Claude --permission-prompt-tool routing, v0.29.37)
│   │   │   ├── animator_intent_tool.py  # Domain NL: animator
│   │   │   ├── vfx_intent_tool.py       # Domain NL: VFX/particles
│   │   │   ├── ui_intent_tool.py        # Domain NL: UI
│   │   │   ├── intent_common.py         # Shared intent infrastructure
│   │   │   ├── budget_tool.py           # Haiku spend tracking
│   │   │   ├── metrics_tool.py          # Performance metrics tool
│   │   │   ├── code_intel.py            # find_references, compile_preflight, semantic_at
│   │   │   ├── scene_session.py         # save_session, load_session, screenshot_baseline/compare (plain-text format v0.18.0+)
│   │   │   └── _annotations.py          # Tool annotations
│   │   └── plugins/            # Plugin system — 3-source auto-discovery (auto-disabled via UNITY_MCP_SKIP_PLUGINS env)
│   │       └── __init__.py     # load_plugins(mcp, send_fn, args_fn), 3-source discovery, UNITY_MCP_SKIP_PLUGINS filtering
│   └── tests/                  # ~2362 unit tests + 70 live tests + conftest.py (v0.26.0 quality audit, v0.30.4: +2 asset validate_move baseline)
│       ├── helpers.py                  # DRY: make_mock_bridge() + shared test utilities (v0.26.0)
│       ├── test_server*.py             # Core + edge cases + tools
│       ├── test_bridge*.py             # TCP bridge + reconnect + resilience
│       ├── test_reload_ladder.py       # Reload recovery T0-T5 stages + verdict scenarios (20+ tests, v0.27.4)
│       ├── test_middleware*.py          # Middleware layers (god-file split in v0.26.0)
│       ├── test_batch*.py              # Batch + conflict + timeout
│       ├── test_multiscene.py          # Multi-scene CRUD, transfer, diff, bugs (305 tests, v0.24.3)
│       ├── test_transfer_object.py     # transfer_object cross-scene operations (91 tests, v0.24.3)
│       ├── test_schema_cache.py        # Schema caching + validation (17 tests, v0.26.0)
│       ├── test_*_intent.py            # Intent tools
│       ├── test_sampling*.py           # Visual verification
│       ├── test_visual_*.py            # Visual diff + regression
│       ├── test_budget_*.py            # Budget/cost tracking
│       ├── test_scene_brief*.py        # Scene brief
│       ├── test_screenshot_*.py        # Screenshot features
│       ├── live/conftest.py            # Live test fixtures + _ok/_iid helpers (v0.26.0 DRY)
│       ├── live/test_multiscene_live.py        # Multi-scene live integration (158 tests, v0.24.3)
│       ├── live/test_multiscene_stress_live.py # Stress tests: large scenes, rapid operations (243 tests, v0.24.3)
│       └── ... + domain tests (182 files total, 1018 @pytest.mark.asyncio removed v0.26.0)
├── unity-plugin-reload/        # Reload Recovery Package (independent compile-unit, v0.27.4)
│   ├── Editor/
│   │   ├── ReloadBinder.cs                   # SO_REUSEADDR bind-retry for port 9600+
│   │   ├── ReloadCommands.cs                 # Public API for recovery tools
│   │   ├── ReloadCompileNotifier.cs          # Domain load completion detector
│   │   ├── ReloadDiagnoseCommand.cs          # TCP diagnose endpoint (portable _parse_diagnose)
│   │   ├── ReloadDomainStamp.cs              # Session-scoped domain timestamp
│   │   ├── ReloadMiniServer.cs               # Mini TCP server (async accept + handler)
│   │   ├── ReloadPlugin.cs                   # Entry point (AssetImportWorker gate)
│   │   ├── ReloadPortResolver.cs             # Atomic Delete+Move port persistence
│   │   └── Tests/                            # 7 NUnit test files (asmdef: UnityMCP.Reload.Tests)
│   │       ├── ReloadCommandsTests.cs
│   │       ├── ReloadCompileNotifierTests.cs
│   │       ├── ReloadDiagnoseTests.cs
│   │       ├── ReloadDomainStampTests.cs
│   │       ├── ReloadMiniServerTests.cs
│   │       ├── ReloadPluginTests.cs
│   │       └── ReloadPortResolverTests.cs
│   ├── UnityMCP.Reload.asmdef                # Core assembly (no references)
│   ├── package.json                          # v0.1.4, "com.unity-mcp.reload"
│   └── package.json.meta
├── unity-plugin/               # Unity Editor Plugin (130+ C# files, ~14000 LOC, v0.29.2: Chat split into CLI+View, v0.30.4: +482 new tests)
│   └── Editor/
│       ├── MCPServer.cs                    # Dual TCP listeners (main + chat), port auto-assign, ClientSlot pattern
│       ├── PortResolver.cs                 # Pure testable port helpers (ResolvePort, FindFreePort, SavePorts, SaveProjectSettings) + 35 tests (v0.35.0: 4-arg chain env→ProjectSettings→Library→FindFreePort)
│       ├── CommandRouter.cs                # RegisterAll(), guards, core dispatch (partial class)
│       ├── CommandRouter.ObjectHandlers.cs # Object mutation handlers (partial class)
│       ├── CommandRouter.MediaHandlers.cs  # Media/asset handlers (partial class)
│       ├── IMCPPlugin.cs                   # Plugin interface (Name, CommandPrefix, RegisterCommands, OnDomainReload)
│       ├── PluginRegistry.cs               # Static plugin registry (Register, RegisterAllPlugins, OnDomainReload)
│       ├── CommandRegistry.cs              # Command registration + runtime flag
│       ├── CommandSchema.cs                # Parameter validation + fuzzy matching
│       ├── ObjectManager.cs                # CRUD + Undo + SetActive + WireEvent + SetParent
│       ├── ObjectManager.Properties.cs     # Property setter + auto-redirect (v0.23.0: set_property("active") → SetActive)
│       ├── ObjectManager.Transfer.cs       # Move/copy objects between scenes (v0.24.3: transfer_object)
│       ├── ObjectManager.Lookup.cs         # FindType + short-name fallback for custom components (v0.23.0)
│       ├── SceneContext.cs                 # Multi-scene state centralization: IsMulti, QualifyPath, FilterByScene (v0.24.3)
│       ├── ObjectDiffHelper.cs             # Unified-diff format for object comparison (~10x token savings) (v0.24.3, v0.25.0: Transform properties)
│       ├── ValueParser.cs                  # Parse vectors/quaternions/colors/arrays
│       ├── InputNormalizer.cs              # Auto-fix component/property hallucinations
│       ├── HierarchySerializer.cs          # Scene → text tree + MAX_NODES + summary + incremental
│       ├── ComponentSerializer.cs          # Component → key-value + ObjectReference + UnityEvent
│       ├── ComponentSerializer.Finder.cs   # #instanceID in all path tools (v0.23.0)
│       ├── BatchHelper.cs                  # Batch text parser + per-command guards + timeout
│       ├── RefManager.cs                   # Ephemeral $a-$zz scene refs (702 slots)
│       ├── ErrorHelper.cs                  # Contextual errors + "did you mean?"
│       ├── RuntimeHelper.cs                # Reflection invoke + state read
│       ├── PlaytestRunner.cs               # DSL playtest executor (partial class, core)
│       ├── PlaytestRunner.Steps.cs         # ExecuteStep dispatch (partial class, 21 cases)
│       ├── PlaytestParser.cs               # DSL parser
│       ├── PlaytestState.cs + PlaytestConfig.cs
│       ├── IPlaytestSimulator.cs + IPlaytestMonitor.cs
│       ├── PlaytestMonitorRegistry.cs + SimulatorRegistry.cs  # Playtest type registries
│       ├── MultiViewCapture.cs + MultiViewOverlay.cs + OverlayDrawer.cs  # 4-panel screenshots
│       ├── ScreenshotCapture.cs            # Camera modes: default, overview, multi_view
│       ├── CodeExecutor.cs                 # Roslyn C# execution, 3-layer security (IsAllowedAssembly: private→internal v0.26.0)
│       ├── SpatialHelper.cs                # Raycast, overlap, nearest, bounds, grid_cast
│       ├── AnimationHelper.cs + AnimationSerializer.cs
│       ├── AnimatorControllerHelper.cs + AnimatorControllerSerializer.cs
│       ├── TimelineHelper.cs + TimelineSerializer.cs
│       ├── ParticleHelper.cs + ParticleSerializer.cs  # 10 presets
│       ├── ShaderHelper.cs + ShaderSerializer.cs + ShaderGraphHelper.cs
│       ├── UIHelper.cs + LayoutValidator.cs
│       ├── AssetDatabaseHelper.cs + AssetHelper.cs
│       ├── ReferenceHelper.cs + ValidateReferencesHelper.cs
│       ├── SearchHelper.cs                 # Scene queries + multi-scene scanning (v0.24.3: all-scene support)
│       ├── SceneHelper.cs                  # Scene management: open additive, close, set active, list (v0.24.3)
│       ├── ProjectSettingsHelper.cs + MaterialHelper.cs
│       ├── PrefabHelper.cs + ScriptableObjectHelper.cs
│       ├── GameStateHelper.cs + TestRunner.cs # TestRunner v0.25.0: filter param (pipe-separated class names), SessionState-based pending tracking
│       ├── ConsoleCapture.cs + CompileErrorCapture.cs + CompileNotifier.cs
│       ├── FingerprintHelper.cs + ScanHelper.cs + SceneDiffHelper.cs
│       ├── ChangeWatcher.cs + ColliderChecker.cs + SchemaHelper.cs
│       ├── MCPSettings.cs                 # Pure static data class (catalog, EnabledTools, no EditorWindow)
│       ├── CatalogParser.cs               # Plain-text catalog parser (v0.18.0+): "CORE:tool1,tool2\n..." format
│       ├── SettingsNavController.cs       # iOS-style navigational stack + slide animations (v0.23.0 Block 1)
│       ├── SettingsPageFactory.cs         # DRY builder for 4 settings pages (Tools/Permissions/Chat/Sampling) (v0.23.0 Block 1)
│       ├── LlmConfig.cs                   # [Serializable] universal LLM config (Claude + Codex + Gemini) (v0.23.0 Block 3, backend field v0.30.1)
│       ├── LlmConfigStore.cs              # Load/Save LLM configs to Library/ (v0.23.0 Block 3)
│       ├── SamplingPresets.cs             # Backend + Model preset templates: Claude Fast / Gemini Flash / Codex (v0.30.1)
│       ├── MCPSettingsHub.cs              # Central hub window coordinating all settings UI (F26, v0.23.0)
│       ├── MCPHubUI.cs                    # Hub-level layout + sub-window orchestration (F26, v0.23.0)
│       ├── HubHeaderAnim.cs               # Circuit-node network animation: 5 nodes + lines + packet (F26)
│       ├── HubCardButton.cs               # Launcher card buttons for each settings window (F26)
│       ├── MCPHubDivider.cs               # Visual divider component for hub sections (F26)
│       ├── MCPHub.uss                     # Stylesheet for hub + animation classes `han-*` (F26)
│       ├── MCPToolSettingsWindow.cs       # MCP/Tool Settings window (toggles + presets + plugins)
│       ├── ToolsHeaderAnim.cs             # 5 toggle-sweep animation (400ms) — connection-aware colors (F25)
│       ├── MCPPermissionsWindow.cs        # MCP/Permissions window (deny-set config)
│       ├── PermissionsHeaderAnim.cs       # Shield + lock pulse (150ms) — connection-aware colors (F25)
│       ├── ChatSettingsHook.cs            # Event hook: OnBuildConnection fired when Connection window builds
│       ├── MCPStatusWindow.cs             # Connection status monitor (heartbeat animation)
│       ├── MCPActions.cs                  # Shared actions (Restart, Kill, Reimport)
│       ├── MCPStatusModel.cs              # Pure state logic (no deps) — maps connection state → display
│       ├── MCPStatusBarWidget.cs          # Injects MCP pill into AppStatusBar via reflection
│       ├── Tests/                         # Editor tests asmdef (references core, v0.26.0: +[TestFixture] to 6 classes)
│       │   ├── UnityMCP.Editor.Tests.asmdef
│       │   ├── Helpers/                  # Test infrastructure (v0.26.0)
│       │   │   ├── ChipTestBase.cs       # Base class: H() helpers centralized (12 shims extracted, v0.26.0)
│       │   │   └── TestStringHelpers.cs  # CountOccurrences utility (DRY across 4+ files, v0.26.0)
│       │   ├── MultiSceneTestBase.cs      # Base class for multi-scene tests (DRY consolidation v0.24.3+v0.25.0: saves additive scenes, captures main scene name before NewScene)
│       │   ├── MultiSceneFinderTests.cs   # Object finding across scenes + reference scanning (v0.24.3)
│       │   ├── PortResolverTests.cs       # 25 NUnit tests (port validation, fallback, dual-port edge cases)
│       │   ├── MCPStatusModelTests.cs     # 14 NUnit tests (state transitions, labels, pills) [+TestFixture v0.26.0]
│       │   ├── CatalogParserTests.cs      # [+TestFixture v0.26.0]
│       │   ├── JsonHelperTests.cs         # [+TestFixture v0.26.0]
│       │   ├── MCPStatusBarPaletteTests.cs # [+TestFixture v0.26.0]
│       │   ├── ValueParserQuaternionTests.cs # [+TestFixture v0.26.0]
│       │   ├── PluginRegistryTests.cs     # [+TestFixture v0.26.0]
│       │   ├── HubHeaderAnimTests.cs      # 11 NUnit tests (circuit-node animation, packet motion, state logic) (F26)
│       │   ├── HubCardButtonTests.cs      # NUnit tests (card rendering, click behavior) (F26)
│       │   ├── MCPHubDividerTests.cs      # NUnit tests (divider styling, layout) (F26)
│       │   ├── ToolsHeaderAnimTests.cs    # 7 NUnit tests (toggle sweep, color cycling, state logic)
│       │   ├── PermissionsHeaderAnimTests.cs # 7 NUnit tests (shield pulse, state logic)
│       │   ├── ChatHeaderAnimTests.cs     # 7 NUnit tests (wifi arc, state logic)
│       │   ├── ChatSettingsHookEventTests.cs # NUnit tests (event firing, hook execution) (F26)
│       │   ├── CodeExecutorSecurityBypassTests.cs # Security hardening: comment-strip, whitespace densify, blocked patterns (v0.31.0, 15 tests)
│       │   ├── CodeExecutorSecurityTests.cs # Core security + whitespace bypass tests
│       │   ├── CodeExecutorSecurityWhitespaceBypassTests.cs # Whitespace evasion scenarios
│       │   ├── ConsoleCaptureTests.cs     # Multi-level console filter + comma-separated levels (v0.31.0)
│       │   ├── MultiSceneHierarchyTests.cs # Multi-scene hierarchy tests
│       │   ├── MultiSceneOperationsTests.cs # Multi-scene CRUD operations
│       │   ├── MultiSceneFinderTests.cs   # Object finding across scenes (updated v0.31.0)
│       │   ├── SceneContextMultiSceneTests.cs # Scene context multi-scene behavior
│       │   ├── ScenePathParserTests.cs    # Multi-scene path parsing: "SceneName:/" extraction (v0.31.0)
│       ├── Chat/                          # Optional in-Unity Agent Chat (v0.29.2: split into CLI + View, UNITY_MCP_CHAT define)
│       │   ├── CLI/                        # Chat.CLI assembly (protocol, parsing, backends, independent compile)
│       │   │   ├── ChatEvent.cs               # Normalized event struct
│       │   │   ├── ChatStreamParser.cs    # Parse stream-json from claude CLI stdout
│       │   │   ├── ClaudeArgBuilder.cs    # Build --mcp-config file + CLI args (--permission-prompt-tool wired, v0.29.37)
│       │   │   ├── UserTurnBuilder.cs     # Encode user messages → stdin JSON
│       │   │   ├── ToolVerbMap.cs             # Tool name → humanized action text
│       │   │   ├── IChatBackend.cs            # Backend interface (future plugin seams)
│       │   │   ├── ChatBinaryResolver.cs      # Binary PATH resolution (macOS /bin/zsh -lc)
│       │   │   ├── ChatProcess.cs             # Process lifecycle manager
│       │   │   ├── CliBackendBase.cs          # Abstract host: shared lifecycle, 4 variation axes
│       │   │   ├── ClaudeBackend.cs           # Claude: CliBackendBase subclass (persistent stdin)
│       │   │   ├── CodexAppServerBackend.cs   # Codex (app-server): persistent JSON-RPC 2.0 sessions (experimentalApi, v0.29.38)
│       │   │   ├── CodexAppServerParser.cs    # Codex (app-server): JSON-RPC + tool/requestUserInput handler (v0.29.38)
│       │   │   ├── GeminiBackend.cs           # Gemini: CliBackendBase subclass (gcloud CLI, v0.30.1)
│       │   │   ├── GeminiArgBuilder.cs        # Build gcloud args + .gemini/settings.json (smart port merge, v0.30.1)
│       │   │   ├── GeminiParser.cs            # Parse stream-json: skip role:user + non-mcp_ tools, suppress ask_user (v0.30.1)
│       │   │   ├── GeminiProvider.cs          # IBackendProvider Gemini implementation (auto-discovered via TypeCache, v0.30.1)
│       │   │   ├── KimiBackend.cs             # Kimi: CliBackendBase subclass (Kimi K2 CLI, v0.34.0)
│       │   │   ├── KimiArgBuilder.cs          # Build Kimi args + role-based NDJSON protocol (v0.34.0, 120 LOC)
│       │   │   ├── KimiParser.cs              # Parse Kimi NDJSON response stream (v0.34.0, 74 LOC)
│       │   │   ├── KimiProvider.cs            # IBackendProvider Kimi implementation (v0.34.0)
│       │   │   ├── OpenCodeBackend.cs         # OpenCode: CliBackendBase subclass (multi-provider model selection, v0.34.0)
│       │   │   ├── OpenCodeArgBuilder.cs      # Build OpenCode args + model name mapping (v0.34.0, 132 LOC)
│       │   │   ├── OpenCodeParser.cs          # Parse OpenCode stream-json (v0.34.0, 92 LOC)
│       │   │   ├── OpenCodeProvider.cs        # IBackendProvider OpenCode implementation (v0.34.0)
│       │   │   ├── BackendRegistry.cs         # Backend factory + BackendKind enum (Claude, Codex, Gemini, Kimi, OpenCode)
│       │   │   ├── BackendConfig.cs           # [Serializable] configs per backend + KimiBackendConfig + OpenCodeBackendConfig (v0.34.0)
│       │   │   ├── BackendConfigStore.cs      # JsonUtility Load/Save (project-local Library/)
│       │   │   ├── BackendSettingsForm.cs     # UIToolkit per-backend settings forms (v0.30.1: redesigned with presets)
│       │   │   ├── ControlResponseBuilder.cs  # Serialize approval + user input responses (v0.29.2+, CodexUserInputResponse v0.29.38)
│       │   │   ├── ClipboardImageReader.cs    # Platform-specific clipboard image read (macOS/Windows/Linux, v0.34.0, 142 LOC)
│       │   │   ├── ImageAttachmentStore.cs    # Temp file storage for pasted/dropped images (v0.34.0, 96 LOC)
│       │   │   ├── ProviderRegistry.cs        # Base class for extensible provider registries (Settings/Toolbar/Panel, v0.34.0)
│       │   │   ├── SettingsProviderRegistry.cs # Registry for ISettingsProvider implementations (v0.34.0)
│       │   │   ├── ToolbarButtonRegistry.cs   # Registry for IToolbarButtonProvider implementations (v0.34.0)
│       │   │   ├── PanelProviderRegistry.cs   # Registry for IPanelProvider implementations (v0.34.0)
│       │   │   ├── ISettingsProvider.cs       # Plugin interface for custom settings UI (v0.34.0)
│       │   │   ├── IToolbarButtonProvider.cs  # Plugin interface for toolbar buttons (v0.34.0)
│       │   │   ├── IPanelProvider.cs          # Plugin interface for side panels (v0.34.0)
│       │   │   ├── ChatTranscript.cs          # In-memory message history + streaming→finalize strategy
│       │   │   ├── TranscriptSerializer.cs    # Serialize/deserialize chat history to plain-text (F21 reload survival)
│       │   │   ├── AssemblyInfo.cs            # AssemblyVersion + InternalsVisibleTo decorators (Chat.CLI)
│       │   │   └── Tests/                     # CLI assembly tests (protocol, parsing, backends)
│       │   │       ├── ChatStreamParserTests.cs # Parse stream-json events + control_request routing
│       │   │       ├── ClaudeArgBuilderTests.cs # CLI arg building + permission-prompt-tool (v0.29.37)
│       │   │       ├── CodexAppServerParserTests.cs # Codex JSON-RPC + requestUserInput (v0.29.38)
│       │   │       ├── CodexArgBuilderTests.cs # Codex CLI args + model wiring (v0.30.4, 33 tests)
│       │   │       ├── ControlResponseBuilderTests.cs # Response serialization including CodexUserInputResponse (v0.29.38)
│       │   │       ├── GeminiArgBuilderTests.cs # Gemini gcloud args + settings.json port update + field mapping (v0.30.1, 217 tests)
│       │   │       ├── GeminiParserTests.cs   # Gemini stream-json parsing: prompt echo filter, tool prefix, ask_user suppression (v0.30.1, 190 tests)
│       │   │       ├── GeminiTestFixtures.cs  # Shared Gemini test payloads (v0.30.1, 33 LOC)
│       │   │       ├── MultiSceneChipTests.cs # Scene-qualified object path parsing + display (v0.30.4, 74 tests)
│       │   │       ├── TokenFormatTests.cs    # Token cost display + null-safe guards (v0.30.4, 12 tests)
│       │   │       └── ... # 40+ total CLI tests
│       │   ├── View/                       # Chat.View assembly (UI windows, rendering, cards)
│       │   │   ├── MCPChatWindow.cs           # EditorWindow UI + interaction (partial class)
│       │   │   ├── MCPChatWindow.Drain.cs     # Event draining + state updates + domain refresh trigger (F27) (partial class)
│       │   │   ├── MCPChatWindow.Send.cs      # Send path: OnSend, rawText/llmText split, chip snapshot (partial class)
│       │   │   ├── MCPChatWindow.FlowBar.cs   # Activity animation track+chip (_askPending flag v0.29.37)
│       │   │   ├── MCPChatWindow.Chips.cs     # Drag-drop chip UX + removable ✕ buttons (F29: external files/folders, v0.23.0 Block 5: ProcessDraggedObject)
│       │   │   ├── MCPChatWindow.InlineChips.cs # Inline chip methods (extracted partial, F5)
│       │   │   ├── MCPChatWindow.Selector.cs  # Backend/mode selector + token reset (F1)
│       │   │   ├── MCPChatWindow.Resize.cs    # Window resize logic
│       │   │   ├── MCPChatWindow.Approve.cs   # Event handler for interactive permissions (v0.29.2+)
│       │   │   ├── TokenFormat.cs             # Pure Abbr(n) helper — "1.2k" / "840" token display
│       │   │   ├── EnterKeySend.cs            # Enter-to-send + Alt+Enter newline logic (pure testable)
│       │   │   ├── ChatSettingsSection.cs     # Delegate class for ChatConnectionSection (F23 refactored)
│       │   │   ├── ChatConnectionSection.cs   # [InitializeOnLoad] subscriber to ChatSettingsHook.OnBuildConnection (F23)
│       │   │   ├── ChatActivityState.cs       # Activity state tracking for grouping
│       │   │   ├── ChatLabel.cs               # Label customization + UI behavior
│       │   │   ├── ChatRefAction.cs           # Click-navigate + context-menu for interactive refs
│       │   │   ├── ChatRefResolver.cs         # Scan hierarchy, resolve scene/script refs (F4 #ID)
│       │   │   ├── CopyableText.cs            # Selectable text wrapper
│       │   │   ├── CopyTextBuilder.cs         # Multi-line copy block assembly
│       │   │   ├── InputHeightCalc.cs         # Input field auto-height calculation (F30: 4-line default, tiny-window clamp fix)
│       │   │   ├── JsonArrayScan.cs           # Scan JSON arrays for streaming results
│       │   │   ├── ArgTokenizer.cs            # Shell-style quote-aware split (F9, review-hardening)
│       │   │   ├── ArgQuoting.cs              # Quote escaping helpers
│       │   │   ├── InlineChipData.cs          # ChipData + InlineChipTracker (F5)
│       │   │   ├── InlineChipOverlay.cs       # Pill row UI (F5)
│       │   │   ├── InlineChipKeyHandler.cs    # TextField event routing (F5)
│       │   │   ├── ChipKindDetector.cs        # Pure Detect() → ChipKind (F10)
│       │   │   ├── ResponseTagInliner.cs      # [kind:ref] parser + renderer (F10)
│       │   │   ├── RestoreButton.cs           # Undo per-turn + cascade restore (F2)
│       │   │   ├── TurnUndoTracker.cs         # Group lifecycle + RestoreFromIndex (F2)
│       │   │   ├── SelectionSummary.cs        # Auto-Selection context (F4 hierarchy #ID)
│       │   │   ├── CompileAutoFix.cs          # Auto-retry on compile
│       │   │   ├── EditorStateSnapshot.cs     # Context block injection
│       │   │   ├── ToolPing.cs                # Flash object on tool-call
│       │   │   ├── HierarchyContextMenu.cs    # Right-click Hierarchy GameObject → Add to Chat Context (F16a)
│       │   │   ├── ComponentContextMenu.cs    # Right-click Component → Add to Chat Context (F16b, v0.23.0 Block 5: dual-chip @GO|@Script)
│       │   │   ├── ChipContextResolver.cs     # Resolve chips + emit typed (F10)
│       │   │   ├── AskUserCard.cs             # Interactive user input dialog (radio/checkbox/freetext, v0.29.11+, v0.29.38: codex: support)
│       │   │   ├── AskUserQuestionRow.cs      # Extracted pill-button row UI (217 LOC, v0.29.37)
│       │   │   ├── ToolApprovalCard.cs        # Risk-classified tool approval UI (Allow/Deny/Session/Always, v0.29.2)
│       │   │   ├── RiskClassifier.cs          # Tool risk categorization (v0.29.2)
│       │   │   ├── SessionAllowlist.cs        # Session-scoped tool allowlist manager (v0.29.2)
│       │   │   ├── ApproveHelper.cs           # Session management for approvals
│       │   │   ├── ApproveButtonFactory.cs    # Button builder (Allow/Deny/Session/Always)
│       │   │   ├── ChatMcpConfigWriter.cs     # Python command resolution + warning on serverDir change (v0.23.0)
│       │   │   ├── SlashTemplate.cs           # Template model
│       │   │   ├── SlashRegistry.cs           # Template registry
│       │   │   ├── SlashPopup.cs              # UIToolkit popup
│       │   │   ├── MCPChatWindow.Slash.cs     # Slash setup
│       │   │   ├── ReloadGuard.cs             # Domain-reload lock
│       │   │   ├── PendingTurnState.cs        # Persist in-flight state (v3: BackendKind) (F28: backward-compat mapping for old int=2)
│       │   │   ├── SentTextCache.cs           # Domain-reload dedup
│       │   │   ├── StderrRingBuffer.cs        # Stderr capture
│       │   │   ├── ToolCallAccumulator.cs     # Accumulate tool calls
│       │   │   ├── ToolCallRecord.cs          # Tool call record struct
│       │   │   ├── ToolChipGrouper.cs         # Group tool calls by ID
│       │   │   ├── ToolDetailBuilder.cs       # Tool card humanization
│       │   │   ├── ToolGroupState.cs          # Tool grouping state
│       │   │   ├── ToolGroupSummary.cs        # Summary of grouped tool calls
│       │   │   ├── UserToolResultParser.cs    # Parse tool results
│       │   │   ├── MCPChatWindow.uss          # UIToolkit styling (header removal + bottom footer)
│       │   │   ├── Markdown/                  # Content rendering: registry seam + renderers
│       │   │   │   ├── MdBlock.cs             # Block model (enum + metadata)
│       │   │   │   ├── MarkdownParser.cs      # string → List<MdBlock> (single-pass)
│       │   │   │   ├── MarkdownParser.Blocks.cs # Block parsing helpers
│       │   │   │   ├── MarkdownInline.cs      # Inline spans → Unity rich-text (noparse <>, protect code)
│       │   │   │   ├── InlineImageThumbnail.cs # Image thumbnail rendering in paragraphs (v0.34.0, 70 LOC)
│       │   │   │   ├── ChipInlinePreviewPanel.cs # Lazy-load toggle panel for media previews (v0.35.0, 57 LOC)
│       │   │   │   ├── InlinePreviewBuilder.cs # Extensible preview factory (texture/image/model/prefab/audio, v0.35.0, 116 LOC)
│       │   │   │   ├── IChatBlockRenderer.cs  # Extension interface (can-render + render)
│       │   │   │   ├── ChatBlockRendererRegistry.cs # Ordered first-match-wins
│       │   │   │   ├── ChatBlockRendererFactory.cs # Default wiring (Mermaid first, Markdown catch-all); injects ChatRefResolver + AddRefToContext
│       │   │   │   ├── MarkdownBlockRenderer.cs # 8-kind dispatcher
│       │   │   │   ├── MarkdownBlockRenderer.Table.cs # Table grid layout (partial)
│       │   │   │   ├── MarkdownBlockRenderer.List.cs # Bullet/ordered list (partial)
│       │   │   │   ├── ImageBlockRenderer.cs  # PNG/JPG → Texture2D + click-to-open (v0.23.0: IsImageFile guard)
│       │   │   │   ├── Viewers/                # Media viewer windows (v0.23.0 Block 4, v0.34.0 expanded)
│       │   │   │   │   ├── ImageViewerWindow.cs # Modal image viewer: zoom/pan/fit controls
│       │   │   │   │   ├── MermaidViewerWindow.cs # Modal mermaid viewer: zoom/pan + exportable SVG
│       │   │   │   │   ├── ZoomPanManipulator.cs # DRY shared zoom/pan/fit logic (reusable for future viewers)
│       │   │   │   │   ├── IAssetViewer.cs      # Plugin interface for custom asset viewers (v0.34.0)
│       │   │   │   │   ├── AssetViewerFactory.cs # Registry + factory for extensible viewers (v0.34.0, 83 LOC)
│       │   │   │   │   ├── PrefabViewerWindow.cs # Prefab 3D preview window (v0.34.0, 151 LOC)
│       │   │   │   │   ├── PrefabPreviewLoader.cs # Temporary scene prefab instantiation (v0.34.0, 82 LOC)
│       │   │   │   │   ├── ModelViewerWindow.cs # 3D model viewer (.fbx/.obj/.blend/.dae, v0.34.0, 151 LOC)
│       │   │   │   │   ├── SpriteViewerWindow.cs # Sprite texture viewer with grid (v0.34.0, 78 LOC)
│       │   │   │   │   ├── AudioViewerWindow.cs # Audio clip player (v0.34.0, 142 LOC)
│       │   │   │   │   └── AudioUtilProxy.cs    # Reflection wrapper for Editor AudioUtil (v0.34.0, 66 LOC)
│       │   │   │   ├── Mermaid/               # Native Mermaid flowchart (no lib, pure parse+layout)
│       │   │   │   │   ├── MermaidGraph.cs    # POCO: nodes, edges, direction
│       │   │   │   │   ├── MermaidParser.cs   # lines → graph or null
│       │   │   │   │   ├── MermaidLayout.cs   # Kahn topo + longest-path + dynamic node sizing
│       │   │   │   │   ├── MermaidLayout.Layers.cs # Layer building + cycle guard
│       │   │   │   │   ├── MermaidBlockRenderer.cs # CanRender Mermaid, fallback to code-box (v0.23.0: opens MermaidViewerWindow)
│       │   │   │   │   ├── MermaidView.cs     # Absolute nodes + edge overlay + geom-change callback
│       │   │   │   │   └── MermaidEdgePainter.cs  # Painter2D lines + arrowheads
│       │   │   ├── Tests/                     # CLI + View assembly tests (parsing, backends, cards, interactivity)
│       │   │   │   ├── CLI/                   # CLI assembly tests
│       │   │   │   │   ├── ChatStreamParserTests.cs # Parse stream-json events + control_request routing
│       │   │   │   │   ├── ClaudeArgBuilderTests.cs # CLI arg building + permission-prompt-tool (v0.29.37)
│       │   │   │   │   ├── CodexAppServerParserTests.cs # Codex JSON-RPC + requestUserInput (v0.29.38)
│       │   │   │   │   ├── CodexArgBuilderTests.cs # Codex CLI args + model wiring (v0.30.4, 33 tests)
│       │   │   │   │   ├── ControlResponseBuilderTests.cs # Response serialization including CodexUserInputResponse (v0.29.38)
│       │   │   │   │   ├── GeminiArgBuilderTests.cs # Gemini gcloud args + settings.json port update (v0.30.1, 217 tests)
│       │   │   │   │   ├── GeminiParserTests.cs   # Gemini stream-json parsing (v0.30.1, 190 tests)
│       │   │   │   │   ├── KimiArgBuilderTests.cs # Kimi K2 args + role NDJSON protocol (v0.34.0, 214 tests)
│       │   │   │   │   ├── KimiParserTests.cs     # Kimi NDJSON response parsing (v0.34.0, 243 tests)
│       │   │   │   │   ├── OpenCodeArgBuilderTests.cs # OpenCode args + model mapping (v0.34.0, 222 tests)
│       │   │   │   │   ├── OpenCodeParserTests.cs # OpenCode stream-json parsing (v0.34.0, 273 tests)
│       │   │   │   │   ├── ImageAttachmentStoreTests.cs # Image attachment storage + temp files (v0.34.0, 188 tests)
│       │   │   │   │   ├── BuiltInChipProvidersTests.cs # Image/Model/Audio chip providers (v0.34.0, 214 tests)
│       │   │   │   │   ├── ProviderRegistryTests.cs # Provider registry base class (v0.34.0, 57 tests)
│       │   │   │   │   ├── MultiSceneChipTests.cs # Scene-qualified object path parsing + display (v0.30.4, 74 tests)
│       │   │   │   │   ├── TokenFormatTests.cs    # Token cost display + null-safe guards (v0.30.4, 12 tests)
│       │   │   │   │   ├── UserTurnBuilderImageTests.cs # User turn JSON with image serialization (v0.34.0, 76 tests)
│       │   │   │   │   └── ... # 40+ total CLI tests
│       │   │   │   ├── View/                  # View assembly tests (UI, cards, interactivity)
│       │   │   │   │   ├── AskUserCardTests.cs     # User input dialog + Codex protocol (v0.29.38 addition)
│       │   │   │   │   ├── ApproveFlowTests.cs     # Interactive approvals flow
│       │   │   │   │   ├── ChipSequenceTests.cs
│       │   │   │   │   ├── ChipSendSequenceTests.cs
│       │   │   │   │   ├── ModelSelectorTests.cs   # Per-backend model dropdown + preset selection (v0.30.4, 231 tests)
│       │   │   │   │   ├── SetModeTests.cs         # Ask↔Agent mode switch + session persistence (v0.30.4, 120 tests)
│       │   │   │   │   ├── TokenResetTests.cs      # Token counter reset + cost display (v0.30.4 upd v0.31.0, 14 tests + cost fix)
│       │   │   │   │   ├── TokenFormatTests.cs     # Token cost display formatting + null-safe guards (v0.31.0, 12 tests)
│       │   │   │   │   ├── ClipboardPasteTests.cs  # Clipboard image paste + mime detection (v0.34.0, 37 tests)
│       │   │   │   │   ├── ImageDragDropTests.cs   # Image drag-drop from Finder (v0.34.0, 154 tests)
│       │   │   │   │   ├── InlineImageThumbnailTests.cs # Image thumbnails in chat paragraphs (v0.34.0, 116 tests + 13 extended v0.35.0)
│       │   │   │   │   ├── ChipInlinePreviewPanelTests.cs # Inline preview toggle panel (v0.35.0, 8 tests)
│       │   │   │   │   ├── InlinePreviewBuilderTests.cs # Preview factory extensibility (v0.35.0, 9 tests)
│       │   │   │   │   ├── MultiImageBubbleTests.cs # Multi-image bubble rendering (v0.35.0, 3 tests)
│       │   │   │   │   ├── ImageViewerWindowTests.cs # Image viewer window (v0.35.0, 8 tests)
│       │   │   │   │   ├── PrefabViewerWindowTests.cs # Prefab preview window (v0.34.0, 198 tests)
│       │   │   │   │   ├── AssetViewerFactoryTests.cs # Media viewer factory + registry (v0.34.0, 224 tests + 11 extended v0.35.0)
│       │   │   │   │   ├── PluginSettingsInjectionTests.cs # ISettingsProvider plugin interface (v0.34.0, 72 tests)
│       │   │   │   │   ├── PluginToolbarButtonTests.cs # IToolbarButtonProvider plugin interface (v0.34.0, 105 tests)
│       │   │   │   │   └── ... # 48+ total View tests
│       │   │   │   └── Markdown/                # Render tests
│       │   │   │       ├── MarkdownParserTests.cs
│       │   │   │       ├── MermaidParserTests.cs
│       │   │   │       └── ... # 25+ render tests
│       │   ├── UnityMCP.Editor.Chat.CLI.asmdef # CLI assembly: protocol, parsing, backends (independent compile, v0.29.2)
│       │   ├── UnityMCP.Editor.Chat.View.asmdef # View assembly: UI windows, rendering, cards (depends on CLI)
│       ├── ChatSettingsHook.cs            # Event hook: fires on MCPSettings rebuild
│       ├── AssemblyInfo.cs                # InternalsVisibleTo("UnityMCP.Editor.Chat.*")
│       ├── MenuHelper.cs + SceneHelper.cs + EditorStateHelper.cs
│       ├── JsonHelper.cs + StringDistance.cs + UndoGroupHelper.cs
│       ├── FileOutputHelper.cs             # ScreenshotsDir = <ProjectRoot>/ScreenShots/ (v0.23.0)
│       ├── VersionTracker.cs
│       └── Roslyn/                         # Roslyn compiler for execute_code
│   └── Runtime/                           # Runtime assembly (v0.25.0: test helpers)
│       ├── UnityMCP.Runtime.TestHelpers.asmdef # Separate assembly for test utilities
│       └── TestHelpers/
│           └── TestDummyMB.cs             # Dummy MonoBehaviour for AddComponent<> in editor tests (moved from Editor/Chat/Tests v0.25.0)
├── unity-test-project/          # Unity 6000.3 test project (2690+ NUnit tests incl. Editor + Chat, v0.30.4: +69 new)
│   ├── Assets/Tests/Editor/     # NUnit test files
│   ├── Assets/Animations/       # Animation clips + controllers
│   ├── Assets/Scenes/
│   ├── Assets/Shaders/          # TestGraph.shadergraph
│   ├── Assets/Scripts/          # Test helpers (GridPlayer, etc.)
│   └── Packages/manifest.json   # References unity-plugin via file:
├── docs/                       # User documentation
│   ├── assets/                 # SVG diagrams and badges
│   ├── install/                # Backend setup guides (v0.34.6+)
│   │   ├── kimi.md             # Kimi K2 CLI backend: Homebrew, PATH, model config
│   │   └── gemini.md           # Gemini backend: gcloud auth, model selection
│   └── README.md               # Root documentation mirror
├── install.py                  # Setup/update/doctor/configure CLI (v0.23.0, 179 lines)
├── .mcp.json                   # uv-based config template (v0.23.0, no absolute paths)
├── scripts/                    # Tooling: changelog SVG, force_reset.sh (recovery), test updates
│   ├── gen_changelog_svg.py    # Changelog → SVG badge
│   ├── force_reset.sh          # Kill stale servers + clean lockfiles (v0.23.0 recovery)
│   └── ...                     # Test suite utilities
├── AI/                         # Feature knowledge docs + changelog
├── .claude/
│   ├── skills/                 # Technical references
│   └── agents/                 # Agent specifications
└── CLAUDE.md
```
