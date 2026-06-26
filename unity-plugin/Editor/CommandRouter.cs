using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Compilation;
using UnityEngine;

[assembly: InternalsVisibleTo("UnityMCP.TestProject")]

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        // Fired when ask_user command arrives; MCPChatWindow subscribes to show AskUserCard.
        public static event System.Action<string, string> OnAskUser;  // (requestId, questionsJson)

        // Testable compilation state (defaults to real MCPServer state).
        // Two-layer check:
        //   1. MCPServer.IsReallyCompiling — authoritative flag set by compilationStarted/Finished events.
        //      Never stays latched after domain reload (unlike EditorApplication.isCompiling on Windows).
        //   2. CompileElapsedSeconds < 120s — wedge guard: treat >120s latched compiling as done.

        // Production lambda — saved separately so tests can restore it via DefaultIsCompiling
        // instead of reconstructing the lambda by hand.
        internal static readonly Func<bool> DefaultIsCompiling = () =>
        {
            if (!MCPServer.IsReallyCompiling) return false;
            return MCPServer.CompileElapsedSeconds < 120.0;
        };

        internal static Func<bool> IsCompiling = DefaultIsCompiling;
        internal static Func<bool> IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
        internal static Func<string, bool> IsToolEnabledFn = MCPSettings.IsToolEnabled;

        // Feature 3: recent command history for smart checkpoint naming
        internal static readonly Queue<string> _recentCmds = new Queue<string>();

        private static void TrackCommand(string cmd)
        {
            _recentCmds.Enqueue(cmd);
            if (_recentCmds.Count > 3) _recentCmds.Dequeue();
        }

        // Feature 2: suggest next tool for mutating commands
        internal static string SuggestNext(string cmd) => cmd switch
        {
            "set_property" => "get_console level=Error",
            "create_object" => "get_hierarchy depth=1",
            "wire_event" => "validate_references",
            "unwire_event" => "get_component",
            "manage_component" => "get_components_list",
            "delete_object" => "get_hierarchy depth=1",
            "set_parent" => "get_hierarchy depth=1",
            "batch" => "get_console level=Error",
            _ => null
        };

        // Returns error response string if a guard blocks the command, null otherwise.
        private static string CheckGuards(string id, string cmd)
        {
            if (IsCompiling() && !IsAllowedDuringCompile(cmd))
                return JsonHelper.FormatBusyResponse(id, "Unity is compiling. Retry in 5s.", 5000);
            if (IsPlayMode() && IsMutatingCommand(cmd))
                return JsonHelper.FormatResponse(id, false, null, "Play mode active — changes will be lost. Stop play mode first.");
            if (!IsPlayMode() && CommandRegistry.IsRuntime(cmd))
                return JsonHelper.FormatResponse(id, false, null, "Not in Play Mode. Use editor(action='play') first.");
            if (!IsAlwaysAllowed(cmd) && !IsToolEnabledFn(cmd))
                return JsonHelper.FormatResponse(id, false, null, $"Tool '{cmd}' is disabled in settings");
            return null;
        }

        // editor excluded: play/stop/select don't corrupt scene data
        private static bool IsMutatingCommand(string cmd) => CommandRegistry.IsMutating(cmd);

        public static string Process(string json)
        {
            SceneContext.InvalidateCache();
            try
            {
                var id = JsonHelper.ExtractString(json, "id");
                var cmd = JsonHelper.ExtractString(json, "cmd");

                var guard = CheckGuards(id, cmd);
                if (guard != null) return guard;

                var argsJson = JsonHelper.ExtractObject(json, "args");

                UndoGroupHelper.SetCommandFallback(cmd);

                if (cmd == "screenshot")
                {
                    var result = BuildScreenshotResponse(id, argsJson);
                    UndoGroupHelper.EndGroup();
                    return result;
                }

                if (cmd == "run_tests")
                {
                    UndoGroupHelper.EndGroup();
                    return JsonHelper.FormatResponse(id, false, null, "run_tests requires async dispatch — use ProcessAsync");
                }

                TrackCommand(cmd);
                var before = DateTime.Now;
                var data = ExecuteCommand(cmd, argsJson);
                UndoGroupHelper.EndGroup();
                if (IsMutatingCommand(cmd))
                {
                    var errors = ConsoleCapture.GetErrorsSince(before);
                    if (errors != null) data += "\n⚠ CONSOLE ERRORS:\n" + errors;
                    var suggestion = SuggestNext(cmd);
                    if (suggestion != null) data += $"\n[next: {suggestion}]";
                }
                return BuildResponse(id, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCP] Command failed: {e.Message}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                return JsonHelper.FormatResponse(id, false, null, e.Message);
            }
        }

        public static void ProcessAsync(string json, TaskCompletionSource<string> tcs)
        {
            SceneContext.InvalidateCache();
            try
            {
                var cmd = JsonHelper.ExtractString(json, "cmd");
                var id = JsonHelper.ExtractString(json, "id");

                if (CommandRegistry.HasAsyncHandler(cmd, out var asyncHandler))
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    UndoGroupHelper.SetCommandFallback(cmd);
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    asyncHandler(id, argsJson, tcs);
                    return;
                }

                tcs.TrySetResult(Process(json));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MCP] Command failed: {e.Message}");
                var id = JsonHelper.ExtractString(json, "id") ?? "unknown";
                tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, e.Message));
            }
        }

        private static void AsyncRunTests(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var mode = JsonHelper.ExtractString(argsJson, "mode");
            var group = JsonHelper.ExtractString(argsJson, "group");
            var filter = JsonHelper.ExtractString(argsJson, "filter");
            TestRunner.Execute(mode, result =>
            {
                UndoGroupHelper.EndGroup();
                if (result.StartsWith("Error:"))
                    tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, result.Substring(7)));
                else
                    tcs.TrySetResult(BuildResponse(id, result));
            }, group, filter);
        }

        private static void AsyncWaitUntil(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var component = JsonHelper.ExtractString(argsJson, "component");
            var field = JsonHelper.ExtractString(argsJson, "field");
            var value = JsonHelper.ExtractString(argsJson, "value");
            var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout");
            var negate = JsonHelper.ExtractString(argsJson, "negate") == "true";
            float timeout = 5f;
            if (timeoutStr != null)
                float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out timeout);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.WaitUntil(path, component, field, value, timeout, negate, inner);
            inner.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted
                    ? BuildResponse(id, $"wait_until error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}")
                    : BuildResponse(id, t.Result)));
        }

        private static void AsyncMoveTo(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var position = JsonHelper.ExtractString(argsJson, "position");
            var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout");
            float timeout = 15f;
            if (timeoutStr != null)
                float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out timeout);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.MoveTo(path, position, timeout, inner);
            inner.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted
                    ? BuildResponse(id, $"move_to error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}")
                    : BuildResponse(id, t.Result)));
        }

        private static void AsyncTestStep(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var position = JsonHelper.ExtractString(argsJson, "position");
            var checksBefore = JsonHelper.ExtractString(argsJson, "checks_before") ?? "";
            var checksAfter = JsonHelper.ExtractString(argsJson, "checks_after") ?? "";
            var waitStr = JsonHelper.ExtractString(argsJson, "wait_after");
            var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout");
            float waitAfter = 0.5f;
            float timeout = 15f;
            if (waitStr != null)
                float.TryParse(waitStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out waitAfter);
            if (timeoutStr != null)
                float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out timeout);
            var inner = new TaskCompletionSource<string>();
            RuntimeHelper.TestStep(path, position, checksBefore, checksAfter, waitAfter, timeout, inner);
            inner.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted
                    ? BuildResponse(id, $"test_step error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}")
                    : BuildResponse(id, t.Result)));
        }

        private static void AsyncRunPlaytest(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var script = JsonHelper.ExtractString(argsJson, "script");
            var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout") ?? "120";
            float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float timeout);
            if (timeout <= 0) timeout = 120f;
            var inner = new TaskCompletionSource<string>();
            PlaytestRunner.Run(script, timeout, inner);
            inner.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted
                    ? BuildResponse(id, $"run_playtest error: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}")
                    : BuildResponse(id, t.Result)));
        }

        private static void AsyncAskUser(string id, string argsJson, TaskCompletionSource<string> tcs)
        {
            var questionsJson = JsonHelper.ExtractString(argsJson, "questions") ?? "[]";
            var requestId = System.Guid.NewGuid().ToString("N");
            PendingAskRegistry.Register(requestId);
            if (OnAskUser == null)
                Debug.LogWarning("[MCP] ask_user: no listener — is chat window open?");
            OnAskUser?.Invoke(requestId, questionsJson);
            var askTcs = PendingAskRegistry.GetTcs(requestId);
            askTcs.Task.ContinueWith(t =>
                tcs.TrySetResult(t.IsFaulted || t.IsCanceled
                    ? BuildResponse(id, "{\"cancelled\":true}")
                    : BuildResponse(id, t.Result)));
        }

        internal static string ExecuteCommand(string cmd, string args)
        {
            return CommandRegistry.Execute(cmd, args);
        }

        internal static void RegisterAll()
        {
            CommandRegistry.Clear();

            // Meta (non-mutating)
            CommandRegistry.Register("ping", _ => "pong");
            // C7: "get_version" fast-path is in MCPServer.cs:293 (emits MVID stamp).
            // The VersionTracker delegation here was dead code — MCPServer intercepts get_version
            // before it reaches CommandRouter. Removed so get_version unambiguously means MVID stamp.
            CommandRegistry.Register("get_enabled_tools", _ => ExecGetEnabledTools());
            CommandRegistry.Register("get_disabled_tools", _ => ExecGetDisabledTools());
            CommandRegistry.Register("set_tool_catalog", args =>
            {
                var json = JsonHelper.ExtractString(args, "catalog");
                if (!string.IsNullOrEmpty(json)) MCPSettings.SetCatalog(json);
                return "ok";
            });

            // Read (non-mutating)
            CommandRegistry.Register("get_hierarchy", ExecGetHierarchy);
            CommandRegistry.Register("get_component", ExecGetComponent);
            CommandRegistry.Register("get_components_list", ExecGetComponentsList);
            CommandRegistry.Register("get_object_detail", ExecGetObjectDetail);
            CommandRegistry.Register("find_objects", ExecFindObjects);
            CommandRegistry.Register("get_console", ExecGetConsole);
            CommandRegistry.Register("get_compile_errors", _ => CompileErrorCapture.GetErrors());
            CommandRegistry.Register("compile_status", _ => CompileNotifier.GetStatus());
            CommandRegistry.Register("diagnose", args => DiagnoseCommand.Execute(args));  // C8: read-only multi-signal snapshot
            // sync/sync_status: unified reload API (v0.21)
            CommandRegistry.Register("sync",        args => SyncHelper.TriggerSync(
                JsonHelper.ExtractString(args, "resolve") == "true"));
            CommandRegistry.Register("sync_status", _ => SyncHelper.GetSyncStatus());
            // screenshot is intercepted in Process/ProcessAsync for file response formatting;
            // registered here only for IsRegistered/IsMutating queries
            CommandRegistry.Register("screenshot", _ => throw new InvalidOperationException("screenshot intercepted before ExecuteCommand"));
            CommandRegistry.Register("recompile", _ => { UnityEditor.AssetDatabase.Refresh(); return "ok"; });
            // G11: force_refresh — CLASS-A recovery sequence for file: UPM packages.
            // 1. ImportPackageSources: targeted ImportAsset bypasses dead directory-monitor.
            // 2. Refresh: whole-DB rescan as fallback for multi-file/asmdef edits.
            // 3. RequestScriptCompilation(None): compile ingested source (CleanBuildCache has 6.x no-op bug).
            // 4. StartTickPump: nudge backgrounded editor to start compiling.
            CommandRegistry.Register("force_refresh", _ =>
            {
                SyncHelper.Ops.ImportPackageSources();
                SyncHelper.Ops.Refresh();
                SyncHelper.Ops.RequestScriptCompilation(RequestScriptCompilationOptions.None);
                SyncHelper.Ops.StartTickPump();
                return "force_refresh triggered";
            });
            CommandRegistry.Register("search_scene", args => SearchHelper.Search(
                JsonHelper.ExtractString(args, "query"),
                JsonHelper.ExtractString(args, "root"),
                int.TryParse(JsonHelper.ExtractString(args, "limit") ?? "50",
                    out var sl) ? sl : 50,
                JsonHelper.ExtractString(args, "scene")));
            CommandRegistry.Register("object_diff", args => ObjectDiffHelper.Diff(
                JsonHelper.ExtractString(args, "pathA"),
                JsonHelper.ExtractString(args, "pathB")));
            CommandRegistry.Register("editor", ExecEditor);
            CommandRegistry.Register("inspect", ExecInspect);
            CommandRegistry.Register("validate_references", args => ValidateReferencesHelper.Validate(
                JsonHelper.ExtractString(args, "path"),
                ExtractInt(args, "depth", 3),
                JsonHelper.ExtractString(args, "ignore_optional") == "true",
                JsonHelper.ExtractString(args, "verbose") == "true"));
            CommandRegistry.Register("checkpoint", args =>
            {
                var label = JsonHelper.ExtractString(args, "label");
                if (string.IsNullOrEmpty(label) || label == "checkpoint")
                    label = $"before_{_recentCmds.Count}_{string.Join("_", _recentCmds)}";
                UndoGroupHelper.BeginGroup($"AI: {label}");
                return $"Checkpoint: {label}";
            });
            CommandRegistry.Register("validate_layout", args => LayoutValidator.Validate(
                JsonHelper.ExtractString(args, "root") ?? "/",
                float.TryParse(JsonHelper.ExtractString(args, "min_distance") ?? "3",
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var md) ? md : 3f));
            CommandRegistry.Register("get_spatial_context", args => LayoutValidator.GetSpatialContext(
                JsonHelper.ExtractString(args, "path"),
                float.TryParse(JsonHelper.ExtractString(args, "radius") ?? "5",
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 5f));
            CommandRegistry.Register("fingerprint", args => FingerprintHelper.Fingerprint(
                JsonHelper.ExtractString(args, "path"),
                ExtractInt(args, "depth", 3)));
            CommandRegistry.Register("scan_scene", _ => ScanHelper.Scan());
            CommandRegistry.Register("render_analyze", args => RenderAnalyzer.Execute(args));
            CommandRegistry.Register("check_colliders", args => ColliderChecker.Check(
                JsonHelper.ExtractString(args, "path")));
            CommandRegistry.Register("material_audit", args => MaterialAuditHelper.Execute(args));
            CommandRegistry.Register("analyze_lod_culling", args => LodCullingAnalyzer.Analyze(
                JsonHelper.ExtractString(args, "focus")));
            CommandRegistry.Register("get_schema", args => SchemaHelper.GetSchema(
                JsonHelper.ExtractString(args, "type")));
            CommandRegistry.Register("get_changes", args => ChangeWatcher.GetChanges(
                JsonHelper.ExtractString(args, "clear") != "false"));
            CommandRegistry.RegisterAsync("run_tests", AsyncRunTests);
            CommandRegistry.RegisterAsync("ask_user", AsyncAskUser);
            CommandRegistry.Register("get_test_results", _ => TestRunner.GetResults());
            CommandRegistry.Register("get_test_count", _ => TestRunner.GetTestCount());

            // Runtime (Play Mode only)
            CommandRegistry.Register("invoke_method", args => RuntimeHelper.InvokeMethod(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "method"),
                JsonHelper.ExtractString(args, "args")), runtime: true);
            CommandRegistry.Register("set_runtime_property", args => RuntimeHelper.SetRuntimeProperty(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "field"),
                JsonHelper.ExtractString(args, "value")), runtime: true);
            CommandRegistry.RegisterAsync("wait_until", AsyncWaitUntil, runtime: true);
            CommandRegistry.RegisterAsync("move_to", AsyncMoveTo, runtime: true);
            CommandRegistry.RegisterAsync("test_step", AsyncTestStep, runtime: true);
            CommandRegistry.RegisterAsync("run_playtest", AsyncRunPlaytest, runtime: true);
            CommandRegistry.Register("query_state", args => GameStateHelper.Snapshot(
                JsonHelper.ExtractString(args, "queries")), runtime: true);
            CommandRegistry.Register("get_perf", _ => ProfilerHelper.GetSnapshot(), runtime: true);
            CommandRegistry.Register("get_frame_stats", _ => ProfilerHelper.GetFrameStats(), runtime: true);
            CommandRegistry.RegisterAction("profile", Profiling.ProfileRecorder.Dispatch, runtime: true);
            CommandRegistry.Register("debug_animator", args =>
            {
                var go = ComponentSerializer.FindObjectOrThrow(JsonHelper.ExtractString(args, "path"));
                var animator = go.GetComponent<Animator>();
                if (animator == null) throw new ArgumentException("No Animator on " + go.name);
                return AnimatorHelper.GetState(animator);
            }, runtime: true);
            CommandRegistry.Register("debug_physics", args =>
            {
                var go = ComponentSerializer.FindObjectOrThrow(JsonHelper.ExtractString(args, "path"));
                float radius = JsonHelper.ExtractFloat(args, "radius");
                return PhysicsHelper.GetState(go, radius > 0f ? radius : 5f);
            }, runtime: true);
            CommandRegistry.Register("get_memory", args =>
                MemoryHelper.GetSnapshot(JsonHelper.ExtractString(args, "include") ?? "all"));

            // Write (mutating)
            CommandRegistry.Register("create_object", ExecCreateObject, mutating: true);
            CommandRegistry.Register("transfer_object", ExecTransferObject, mutating: true);
            CommandRegistry.Register("delete_object", ExecDeleteObject, mutating: true);
            CommandRegistry.Register("set_property", ExecSetProperty, mutating: true);
            CommandRegistry.Register("set_property_delta", ExecSetPropertyDelta, mutating: true);
            CommandRegistry.Register("scene_diff", _ => SceneDiffHelper.Diff());
            CommandRegistry.Register("set_active", args => ObjectManager.SetActive(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "active") == "true"), mutating: true);
            CommandRegistry.Register("wire_event", args => ObjectManager.WireEvent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "event"),
                JsonHelper.ExtractString(args, "target"),
                JsonHelper.ExtractString(args, "method"),
                JsonHelper.ExtractString(args, "arg_type") ?? "void",
                JsonHelper.ExtractString(args, "arg_value")), mutating: true);
            CommandRegistry.Register("unwire_event", args => ObjectManager.UnwireEvent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "component"),
                JsonHelper.ExtractString(args, "event"),
                JsonHelper.ExtractString(args, "index")), mutating: true);
            CommandRegistry.Register("manage_component", ExecManageComponent, mutating: true);
            CommandRegistry.Register("set_parent", args => ObjectManager.SetParent(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "parent"),
                JsonHelper.ExtractString(args, "world_position_stays") != "false"), mutating: true);
            CommandRegistry.Register("set_material", args => ObjectManager.SetMaterial(
                JsonHelper.ExtractString(args, "path"),
                JsonHelper.ExtractString(args, "color"),
                JsonHelper.ExtractString(args, "shader")), mutating: true);
            CommandRegistry.Register("batch", args => {
                var timeoutStr = JsonHelper.ExtractString(args, "timeout_ms");
                int timeoutMs = 25000;
                if (timeoutStr != null) int.TryParse(timeoutStr, out timeoutMs);
                bool atomic = JsonHelper.ExtractString(args, "atomic") == "true";
                return BatchHelper.Execute(
                    JsonHelper.ExtractString(args, "commands"),
                    JsonHelper.ExtractString(args, "on_error") ?? "continue",
                    timeoutMs, atomic);
            }, mutating: false);
            CommandRegistry.Register("scene", ExecScene, mutating: true);
            CommandRegistry.Register("animation", ExecAnimationConsolidated, mutating: true);
            CommandRegistry.Register("timeline", ExecTimelineConsolidated, mutating: true);
            CommandRegistry.Register("references", ExecReferencesConsolidated, mutating: true);
            CommandRegistry.Register("create_ui", ExecCreateUI, mutating: true);
            CommandRegistry.Register("set_rect", ExecSetRect, mutating: true);
            CommandRegistry.Register("animator", ExecAnimatorConsolidated, mutating: true);
            CommandRegistry.Register("particle", ExecParticleConsolidated, mutating: true);
            CommandRegistry.Register("shader", ExecShaderConsolidated, mutating: true);
            CommandRegistry.Register("menu", ExecMenu, mutating: true);

            // Action-based (Phase 26, mutating)
            CommandRegistry.RegisterAction("asset", AssetDatabaseHelper.Execute, mutating: true);
            CommandRegistry.RegisterAction("project_settings", ProjectSettingsHelper.Execute, mutating: true);
            CommandRegistry.RegisterAction("material", MaterialHelper.Execute, mutating: true);
            CommandRegistry.RegisterAction("prefab", PrefabHelper.Execute, mutating: true);
            CommandRegistry.RegisterAction("scriptable_object", ScriptableObjectHelper.Execute, mutating: true);

            // Spatial queries (read-only)
            CommandRegistry.Register("spatial_query", args => SpatialHelper.Execute(args));
#if UNITY_MODULE_AI || UNITY_AI_NAVIGATION
            CommandRegistry.Register("navmesh", NavMeshHelper.Execute);
#endif

            // Spatial mutation: region_clear (delete objects inside polygon)
            CommandRegistry.Register("region_clear", args => SpatialHelper.RegionClear(args), mutating: true);

            // Code execution via Roslyn (non-mutating: allowed in Play Mode)
            CommandRegistry.Register("execute_code", args => CodeExecutor.Execute(
                JsonHelper.ExtractString(args, "code"),
                JsonHelper.ExtractString(args, "undo_label") ?? "execute_code"));

            // Watch system (Phase 3)
            WatchCommandHandler.RegisterAll();

            PluginRegistry.RegisterAllPlugins();
            AttributeScanner.ScanAndRegister();

            // Eager-populate after ALL tools are registered (including plugins).
            // This is the correct site: RegisterAll is the last step in CommandRegistry.InitDefaults
            // and is always called on the main thread — safe to read EditorPrefs here.
            _enabledToolsCache = ExecGetEnabledTools();
        }

        private static string BuildResponse(string id, string data)
        {
            if (data != null && data.Length > FileOutputHelper.TEXT_THRESHOLD)
            {
                var filePath = FileOutputHelper.WriteText(data);
                return JsonHelper.FormatFileResponse(id, filePath);
            }
            return JsonHelper.FormatResponse(id, true, data, null);
        }

        // Commands that bypass MCPSettings.IsToolEnabled check
        internal static bool IsAlwaysAllowed(string cmd) =>
            cmd == "ping" || cmd == "get_version" || cmd == "get_enabled_tools" ||
            cmd == "get_disabled_tools" || cmd == "set_tool_catalog" || cmd == "diagnose" ||  // C4: diagnose always reachable
            cmd == "ask_user";  // read-only UI card — not gated by MCPSettings

        internal static bool IsAllowedDuringCompile(string cmd) =>
            cmd == "ping" || cmd == "get_version" || cmd == "get_console" ||
            cmd == "screenshot" || cmd == "get_enabled_tools" || cmd == "compile_status" ||
            cmd == "get_disabled_tools" || cmd == "set_tool_catalog" || cmd == "sync_status" ||
            cmd == "get_compile_errors" || cmd == "diagnose" ||  // C4: escape-hatch + diagnose reachable while wedged
            cmd == "force_refresh" ||  // G11: real force-recompile must work when wedged
            cmd == "get_test_results" ||  // P1: reads SessionState only — safe during compile
            cmd == "get_test_count" ||  // discovery-only, no test run
            cmd == "execute_code" ||  // T2.5: ReloadGuard probe must work when wedged
            cmd == "ask_user";  // shows UI card only — no assembly access

        private static int ExtractInt(string json, string key, int defaultVal)
        {
            var val = JsonHelper.ExtractString(json, key);
            if (val == null) return defaultVal;
            return int.TryParse(val, out var result) ? result : defaultVal;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            PluginRegistry.OnDomainReload();
            // Do NOT populate here: [InitializeOnLoadMethod] fires before CommandRegistry.static ctor
            // (which calls RegisterAll). Populating now yields an empty/partial tool list.
            // Instead, RegisterAll() eagerly populates at its end (after all tools are registered).
        }

    }
}
