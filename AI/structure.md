# Project Structure (Current)

```
unity-kiss-mcp/
├── server/                     # Python MCP Server (2038 unit tests, 70 live = 2108 total Python)
│   ├── src/unity_mcp/
│   │   ├── server.py           # FastMCP instance, lifespan, 89 registered MCP tools
│   │   ├── bridge.py           # UnityBridge (TCP, heartbeat, SO_KEEPALIVE)
│   │   ├── connection_slot.py  # ConnectionSlot: dual connections (CLI + Chat agent)
│   │   ├── server_filtering.py # Port discovery + TCP probe (v0.23.0), catalog push, tool filtering
│   │   ├── lockfile.py         # Cross-platform exclusive locking + zombie detection (v0.23.0)
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
│   │   ├── tools/              # Tool modules (23 files + __init__)
│   │   │   ├── __init__.py     # Tool module registry
│   │   │   ├── objects.py      # create/delete/find/inspect/set_parent/set_material
│   │   │   ├── scene.py        # scene, editor, screenshot, search, spatial, scan, schema
│   │   │   ├── runtime.py      # invoke_method, wait_until, move_to, run_playtest, fuzz_playtest
│   │   │   ├── batch.py        # batch, references, validate_references + _dsl_tools set
│   │   │   ├── codegen.py      # execute_code, get_schema, auto_fix, smart_build
│   │   │   ├── skills.py       # save/use/list_skill, apply/save/list_template + _skills_dir
│   │   │   ├── spatial.py      # validate_layout, get_spatial_context, scan_scene, check_colliders, spatial_query
│   │   │   ├── ui.py           # create_ui, set_rect, menu, shader
│   │   │   ├── animation.py    # animation, timeline, animator, particle
│   │   │   ├── asset.py        # asset, material, prefab, scriptable_object, project_settings
│   │   │   ├── connection.py   # list_connections, reconnect_unity
│   │   │   ├── autobatch.py    # setup_objects, set_properties, configure_objects
│   │   │   ├── gating.py       # TIER1 + category-based capability filtering
│   │   │   ├── do_tool.py      # NL intent → Haiku plan → batch execute
│   │   │   ├── ask_tool.py     # NL read-only → route → Haiku summarize
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
│   └── tests/                  # ~2048 unit tests + 70 live tests + conftest.py (v0.26.0 quality audit)
│       ├── helpers.py                  # DRY: make_mock_bridge() + shared test utilities (v0.26.0)
│       ├── test_server*.py             # Core + edge cases + tools
│       ├── test_bridge*.py             # TCP bridge + reconnect + resilience
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
├── unity-plugin/               # Unity Editor Plugin (75 C# files, ~13600 LOC)
│   └── Editor/
│       ├── MCPServer.cs                    # Dual TCP listeners (main + chat), port auto-assign, ClientSlot pattern
│       ├── PortResolver.cs                 # Pure testable port helpers (ResolvePort, FindFreePort, SavePorts, etc.) + 25 tests
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
│       ├── LlmConfig.cs                   # [Serializable] universal LLM config (Claude + Codex profiles) (v0.23.0 Block 3)
│       ├── LlmConfigStore.cs              # Load/Save LLM configs to Library/ (v0.23.0 Block 3)
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
│       ├── Chat/                          # Optional in-Unity Agent Chat (isolated, UNITY_MCP_CHAT define)
│       │   ├── ChatEvent.cs               # Normalized event struct
│       │   ├── ChatStreamParser.cs        # Parse stream-json from claude CLI stdout
│       │   ├── ClaudeArgBuilder.cs        # Build --mcp-config file + CLI args (+ --disallowedTools)
│       │   ├── UserTurnBuilder.cs         # Encode user messages → stdin JSON
│       │   ├── ToolVerbMap.cs             # Tool name → humanized action text
│       │   ├── IChatBackend.cs            # Backend interface (future plugin seams)
│       │   ├── ChatBinaryResolver.cs      # Binary PATH resolution (macOS /bin/zsh -lc)
│       │   ├── ChatProcess.cs             # Process lifecycle manager
│       │   ├── CliBackendBase.cs          # Abstract host: shared lifecycle, 4 variation axes
│       │   ├── ClaudeBackend.cs           # Claude: CliBackendBase subclass (persistent stdin)
│       │   ├── CodexAppServerBackend.cs   # Codex (app-server): persistent JSON-RPC 2.0 sessions, token streaming (F28)
│       │   ├── CodexAppServerParser.cs    # Codex (app-server): JSON-RPC → ChatEvent (item/agentMessage/delta, mcpToolCall) (F28)
│       │   ├── BackendRegistry.cs         # Backend factory + BackendKind enum (Claude, Codex) (F28: 2 backends, removed spawn-per-turn)
│       │   ├── BackendConfig.cs           # [Serializable] Claude/Codex configs + persistence (F9)
│       │   ├── BackendConfigStore.cs      # JsonUtility Load/Save (F9, project-local Library/)
│       │   ├── BackendSettingsForm.cs     # UIToolkit per-backend settings forms (F9)
│       │   ├── ChatTranscript.cs          # In-memory message history + streaming→finalize strategy
│       │   ├── TranscriptSerializer.cs    # Serialize/deserialize chat history to plain-text (F21 reload survival)
│       │   ├── MCPChatWindow.cs           # EditorWindow UI + interaction (partial class)
│       │   ├── MCPChatWindow.Drain.cs     # Event draining + state updates + domain refresh trigger (F27) (partial class)
│       │   ├── MCPChatWindow.Send.cs      # Send path: OnSend, rawText/llmText split, chip snapshot (partial class)
│       │   ├── MCPChatWindow.FlowBar.cs   # Activity animation track+chip (partial class)
│       │   ├── MCPChatWindow.Chips.cs     # Drag-drop chip UX + removable ✕ buttons (F29: external files/folders, v0.23.0 Block 5: ProcessDraggedObject)
│       │   ├── MCPChatWindow.InlineChips.cs # Inline chip methods (extracted partial, F5)
│       │   ├── MCPChatWindow.Selector.cs  # Backend/mode selector + token reset (F1)
│       │   ├── MCPChatWindow.Resize.cs    # Window resize logic
│       │   ├── TokenFormat.cs             # Pure Abbr(n) helper — "1.2k" / "840" token display
│       │   ├── EnterKeySend.cs            # Enter-to-send + Alt+Enter newline logic (pure testable)
│       │   ├── ChatSettingsSection.cs     # Delegate class for ChatConnectionSection (F23 refactored)
│       │   ├── ChatConnectionSection.cs   # [InitializeOnLoad] subscriber to ChatSettingsHook.OnBuildConnection (F23)
│       │   ├── ChatActivityState.cs       # Activity state tracking for grouping
│       │   ├── ChatLabel.cs               # Label customization + UI behavior
│       │   ├── ChatRefAction.cs           # Click-navigate + context-menu for interactive refs
│       │   ├── ChatRefResolver.cs         # Scan hierarchy, resolve scene/script refs (F4 #ID)
│       │   ├── CopyableText.cs            # Selectable text wrapper
│       │   ├── CopyTextBuilder.cs         # Multi-line copy block assembly
│       │   ├── InputHeightCalc.cs         # Input field auto-height calculation (F30: 4-line default, tiny-window clamp fix)
│       │   ├── JsonArrayScan.cs           # Scan JSON arrays for streaming results
│       │   ├── ArgTokenizer.cs            # Shell-style quote-aware split (F9, review-hardening)
│       │   ├── ArgQuoting.cs              # Quote escaping helpers
│       │   ├── InlineChipData.cs          # ChipData + InlineChipTracker (F5)
│       │   ├── InlineChipOverlay.cs       # Pill row UI (F5)
│       │   ├── InlineChipKeyHandler.cs    # TextField event routing (F5)
│       │   ├── ChipKindDetector.cs        # Pure Detect() → ChipKind (F10)
│       │   ├── ResponseTagInliner.cs      # [kind:ref] parser + renderer (F10)
│       │   ├── RestoreButton.cs           # Undo per-turn + cascade restore (F2)
│       │   ├── TurnUndoTracker.cs         # Group lifecycle + RestoreFromIndex (F2)
│       │   ├── SelectionSummary.cs        # Auto-Selection context (F4 hierarchy #ID)
│       │   ├── CompileAutoFix.cs          # Auto-retry on compile
│       │   ├── EditorStateSnapshot.cs     # Context block injection
│       │   ├── ToolPing.cs                # Flash object on tool-call
│       │   ├── HierarchyContextMenu.cs    # Right-click Hierarchy GameObject → Add to Chat Context (F16a)
│       │   ├── ComponentContextMenu.cs    # Right-click Component → Add to Chat Context (F16b, v0.23.0 Block 5: dual-chip @GO|@Script)
│       │   ├── ChipContextResolver.cs     # Resolve chips + emit typed (F10)
│       │   ├── MCPChatWindow.Approve.cs   # Event handler (F3 gate)
│       │   ├── ApproveHelper.cs           # Session management
│       │   ├── ApproveButtonFactory.cs    # Button builder
│       │   ├── ChatBinaryResolver.cs      # Cross-platform binary PATH resolution (where.exe, bash -lic, zsh -lc per OS)
│       │   ├── ChatMcpConfigWriter.cs     # Python command resolution + warning on serverDir change (v0.23.0)
│       │   ├── SlashTemplate.cs           # Template model
│       │   ├── SlashRegistry.cs           # Template registry
│       │   ├── SlashPopup.cs              # UIToolkit popup
│       │   ├── MCPChatWindow.Slash.cs     # Slash setup
│       │   ├── ReloadGuard.cs             # Domain-reload lock
│       │   ├── PendingTurnState.cs        # Persist in-flight state (v3: BackendKind) (F28: backward-compat mapping for old int=2)
│       │   ├── SentTextCache.cs           # Domain-reload dedup
│       │   ├── StderrRingBuffer.cs        # Stderr capture
│       │   ├── ToolCallAccumulator.cs     # Accumulate tool calls
│       │   ├── ToolCallRecord.cs          # Tool call record struct
│       │   ├── ToolChipGrouper.cs         # Group tool calls by ID
│       │   ├── ToolDetailBuilder.cs       # Tool card humanization
│       │   ├── ToolGroupState.cs          # Tool grouping state
│       │   ├── ToolGroupSummary.cs        # Summary of grouped tool calls
│       │   ├── UserTurnBuilder.cs         # Encode user messages
│       │   ├── UserToolResultParser.cs    # Parse tool results
│       │   ├── MCPChatWindow.uss          # UIToolkit styling (header removal + bottom footer)
│       │   ├── Markdown/                  # Content rendering: registry seam + renderers
│       │   │   ├── MdBlock.cs             # Block model (enum + metadata)
│       │   │   ├── MarkdownParser.cs      # string → List<MdBlock> (single-pass)
│       │   │   ├── MarkdownParser.Blocks.cs # Block parsing helpers
│       │   │   ├── MarkdownInline.cs      # Inline spans → Unity rich-text (noparse <>, protect code)
│       │   │   ├── IChatBlockRenderer.cs  # Extension interface (can-render + render)
│       │   │   ├── ChatBlockRendererRegistry.cs # Ordered first-match-wins
│       │   │   ├── ChatBlockRendererFactory.cs # Default wiring (Mermaid first, Markdown catch-all); injects ChatRefResolver + AddRefToContext
│       │   │   ├── MarkdownBlockRenderer.cs # 8-kind dispatcher
│       │   │   ├── MarkdownBlockRenderer.Table.cs # Table grid layout (partial)
│       │   │   ├── MarkdownBlockRenderer.List.cs # Bullet/ordered list (partial)
│       │   │   ├── ImageBlockRenderer.cs  # PNG/JPG → Texture2D + click-to-open (v0.23.0: IsImageFile guard)
│       │   │   ├── Viewers/                # Media viewer windows (v0.23.0 Block 4)
│       │   │   │   ├── ImageViewerWindow.cs # Modal image viewer: zoom/pan/fit controls
│       │   │   │   ├── MermaidViewerWindow.cs # Modal mermaid viewer: zoom/pan + exportable SVG
│       │   │   │   └── ZoomPanManipulator.cs # DRY shared zoom/pan/fit logic (reusable for future viewers)
│       │   │   ├── Mermaid/               # Native Mermaid flowchart (no lib, pure parse+layout)
│       │   │   │   ├── MermaidGraph.cs    # POCO: nodes, edges, direction
│       │   │   │   ├── MermaidParser.cs   # lines → graph or null
│       │   │   │   ├── MermaidLayout.cs   # Kahn topo + longest-path + dynamic node sizing
│       │   │   │   ├── MermaidLayout.Layers.cs # Layer building + cycle guard
│       │   │   │   ├── MermaidBlockRenderer.cs # CanRender Mermaid, fallback to code-box (v0.23.0: opens MermaidViewerWindow)
│       │   │   │   ├── MermaidView.cs     # Absolute nodes + edge overlay + geom-change callback
│       │   │   │   └── MermaidEdgePainter.cs  # Painter2D lines + arrowheads
│       │   ├── UnityMCP.Editor.Chat.asmdef # Assembly: one-way ref to core, define-gated
│       │   ├── AssemblyInfo.cs            # AssemblyVersion + InternalsVisibleTo decorators
│       │   └── Tests/                     # 25+ NUnit suites = ~565+ test cases (render + backend + chips + pure)
│       │       │   # Render (66): MdBlockTests(5), MarkdownParserTests(16), MarkdownInlineTests(13), MermaidParserTests(17), MermaidLayoutTests(15)
│       │       │   # Backend/parse (150): ChatStreamParserTests(24), CliBackendBaseTests(29), CodexAppServerParserTests(15), CodexArgBuilderTests(35), ClaudeArgBuilderTests(8), ToolVerbMapTests(5), BackendRegistryTests(8), DomainRefreshTests(4), DragDropExternalTests(8)
│       │       │   # Interactivity/input (43): EnterKeySendTests(7), InputHeightCalcTests(14), ChatActivityStateTests(13), CopyTextBuilderTests(9)
│       │       │   # Chips (320+): ChipSequenceTests, ChipSequenceExtraTests, ChipSendSequenceTests, ChipSendSequenceExtraTests, ChipTestHelpers (shared)
│       │       │   # Pure/state (6+489): TokenFormatTests(6), PendingTurnStateTests(187), PendingTurnStateV4Tests(197), PendingTurnStateStalenessTests(105)
│       │       │   # Total: ~1550+ EditMode (5 pre-existing baseline reds, 0 new regressions)
│       ├── ChatSettingsHook.cs            # Event hook: fires on MCPSettings rebuild
│       ├── AssemblyInfo.cs                # InternalsVisibleTo("UnityMCP.Editor.Chat")
│       ├── MenuHelper.cs + SceneHelper.cs + EditorStateHelper.cs
│       ├── JsonHelper.cs + StringDistance.cs + UndoGroupHelper.cs
│       ├── FileOutputHelper.cs             # ScreenshotsDir = <ProjectRoot>/ScreenShots/ (v0.23.0)
│       ├── VersionTracker.cs
│       └── Roslyn/                         # Roslyn compiler for execute_code
│   └── Runtime/                           # Runtime assembly (v0.25.0: test helpers)
│       ├── UnityMCP.Runtime.TestHelpers.asmdef # Separate assembly for test utilities
│       └── TestHelpers/
│           └── TestDummyMB.cs             # Dummy MonoBehaviour for AddComponent<> in editor tests (moved from Editor/Chat/Tests v0.25.0)
├── unity-test-project/          # Unity 6000.3 test project (2623 NUnit tests incl. Editor + Chat)
│   ├── Assets/Tests/Editor/     # NUnit test files
│   ├── Assets/Animations/       # Animation clips + controllers
│   ├── Assets/Scenes/
│   ├── Assets/Shaders/          # TestGraph.shadergraph
│   ├── Assets/Scripts/          # Test helpers (GridPlayer, etc.)
│   └── Packages/manifest.json   # References unity-plugin via file:
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
