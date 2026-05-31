# Project Structure (Current)

```
unity-kiss-mcp/
├── server/                     # Python MCP Server (~1095 unit tests)
│   ├── src/unity_mcp/
│   │   ├── server.py           # FastMCP instance, lifespan, 89 registered MCP tools
│   │   ├── bridge.py           # UnityBridge (TCP, heartbeat, SO_KEEPALIVE)
│   │   ├── connection_slot.py  # ConnectionSlot: single connection
│   │   ├── lockfile.py         # Exclusive fcntl.flock per port + stale server cleanup
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
│   │   ├── sampling.py         # Visual verification (Haiku)
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
│   │   │   └── _annotations.py          # Tool annotations
│   │   └── plugins/            # Plugin system — 3-source auto-discovery (auto-disabled via UNITY_MCP_SKIP_PLUGINS env)
│   │       └── __init__.py     # load_plugins(mcp, send_fn, args_fn), 3-source discovery, UNITY_MCP_SKIP_PLUGINS filtering
│   └── tests/                  # ~1095 unit tests + conftest.py
│       ├── test_server*.py             # Core + edge cases + tools
│       ├── test_bridge*.py             # TCP bridge + reconnect + resilience
│       ├── test_middleware*.py          # Middleware layers
│       ├── test_batch*.py              # Batch + conflict + timeout
│       ├── test_*_intent.py            # Intent tools
│       ├── test_sampling*.py           # Visual verification
│       ├── test_visual_*.py            # Visual diff + regression
│       ├── test_budget_*.py            # Budget/cost tracking
│       ├── test_scene_brief*.py        # Scene brief
│       ├── test_screenshot_*.py        # Screenshot features
│       └── ... + domain tests
├── unity-plugin/               # Unity Editor Plugin (72 C# files, ~13400 LOC)
│   └── Editor/
│       ├── MCPServer.cs                    # TCP listener, SO_KEEPALIVE, domain reload, state file
│       ├── CommandRouter.cs                # RegisterAll(), guards, core dispatch (partial class)
│       ├── CommandRouter.ObjectHandlers.cs # Object mutation handlers (partial class)
│       ├── CommandRouter.MediaHandlers.cs  # Media/asset handlers (partial class)
│       ├── IMCPPlugin.cs                   # Plugin interface (Name, CommandPrefix, RegisterCommands, OnDomainReload)
│       ├── PluginRegistry.cs               # Static plugin registry (Register, RegisterAllPlugins, OnDomainReload)
│       ├── CommandRegistry.cs              # Command registration + runtime flag
│       ├── CommandSchema.cs                # Parameter validation + fuzzy matching
│       ├── ObjectManager.cs                # CRUD + Undo + SetActive + WireEvent + SetParent
│       ├── ValueParser.cs                  # Parse vectors/quaternions/colors/arrays
│       ├── InputNormalizer.cs              # Auto-fix component/property hallucinations
│       ├── HierarchySerializer.cs          # Scene → text tree + MAX_NODES + summary + incremental
│       ├── ComponentSerializer.cs          # Component → key-value + ObjectReference + UnityEvent
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
│       ├── CodeExecutor.cs                 # Roslyn C# execution, 3-layer security
│       ├── SpatialHelper.cs                # Raycast, overlap, nearest, bounds, grid_cast
│       ├── AnimationHelper.cs + AnimationSerializer.cs
│       ├── AnimatorControllerHelper.cs + AnimatorControllerSerializer.cs
│       ├── TimelineHelper.cs + TimelineSerializer.cs
│       ├── ParticleHelper.cs + ParticleSerializer.cs  # 10 presets
│       ├── ShaderHelper.cs + ShaderSerializer.cs + ShaderGraphHelper.cs
│       ├── UIHelper.cs + LayoutValidator.cs
│       ├── AssetDatabaseHelper.cs + AssetHelper.cs
│       ├── ReferenceHelper.cs + ValidateReferencesHelper.cs
│       ├── SearchHelper.cs
│       ├── ProjectSettingsHelper.cs + MaterialHelper.cs
│       ├── PrefabHelper.cs + ScriptableObjectHelper.cs
│       ├── GameStateHelper.cs + TestRunner.cs
│       ├── ConsoleCapture.cs + CompileErrorCapture.cs + CompileNotifier.cs
│       ├── FingerprintHelper.cs + ScanHelper.cs + SceneDiffHelper.cs
│       ├── ChangeWatcher.cs + ColliderChecker.cs + SchemaHelper.cs
│       ├── MCPSettings.cs + MCPStatusWindow.cs
│       ├── MenuHelper.cs + SceneHelper.cs + EditorStateHelper.cs
│       ├── JsonHelper.cs + StringDistance.cs + UndoGroupHelper.cs
│       ├── FileOutputHelper.cs + VersionTracker.cs
│       └── Roslyn/                         # Roslyn compiler for execute_code
├── unity-test-project/          # Unity 6000.3 test project (~746 C# tests)
│   ├── Assets/Tests/Editor/     # NUnit test files
│   ├── Assets/Animations/       # Animation clips + controllers
│   ├── Assets/Scenes/
│   ├── Assets/Shaders/          # TestGraph.shadergraph
│   ├── Assets/Scripts/          # Test helpers (GridPlayer, etc.)
│   └── Packages/manifest.json   # References unity-plugin via file:
├── AI/                         # Feature knowledge docs + changelog
├── .claude/
│   ├── skills/                 # Technical references
│   └── agents/                 # Agent specifications
└── CLAUDE.md
```
