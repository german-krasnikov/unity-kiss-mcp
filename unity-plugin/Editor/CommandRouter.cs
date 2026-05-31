using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

[assembly: InternalsVisibleTo("Tests")]

namespace UnityMCP.Editor
{
    public static partial class CommandRouter
    {
        // Testable compilation state (defaults to real EditorApplication state)
        internal static Func<bool> IsCompiling = () => UnityEditor.EditorApplication.isCompiling;
        internal static Func<bool> IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;

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
            if (!IsAlwaysAllowed(cmd) && !MCPSettings.IsToolEnabled(cmd))
                return JsonHelper.FormatResponse(id, false, null, $"Tool '{cmd}' is disabled in settings");
            return null;
        }

        // editor excluded: play/stop/select don't corrupt scene data
        private static bool IsMutatingCommand(string cmd) => CommandRegistry.IsMutating(cmd);

        public static string Process(string json)
        {
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
            try
            {
                var cmd = JsonHelper.ExtractString(json, "cmd");
                var id = JsonHelper.ExtractString(json, "id");

                if (cmd == "run_tests")
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    UndoGroupHelper.SetCommandFallback(cmd);
                    var mode = JsonHelper.ExtractString(argsJson, "mode");
                    TestRunner.Execute(mode, result =>
                    {
                        UndoGroupHelper.EndGroup();
                        if (result.StartsWith("Error:"))
                            tcs.TrySetResult(JsonHelper.FormatResponse(id, false, null, result.Substring(7)));
                        else
                            tcs.TrySetResult(BuildResponse(id, result));
                    });
                    return;
                }

                if (cmd == "wait_until")
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    var argsJson = JsonHelper.ExtractObject(json, "args");
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
                    var waitTcs = new TaskCompletionSource<string>();
                    RuntimeHelper.WaitUntil(path, component, field, value, timeout, negate, waitTcs);
                    waitTcs.Task.ContinueWith(t => tcs.TrySetResult(BuildResponse(id, t.Result)));
                    return;
                }

                if (cmd == "move_to")
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    var path = JsonHelper.ExtractString(argsJson, "path");
                    var position = JsonHelper.ExtractString(argsJson, "position");
                    var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout");
                    float timeout = 15f;
                    if (timeoutStr != null)
                        float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out timeout);
                    var moveTcs = new TaskCompletionSource<string>();
                    RuntimeHelper.MoveTo(path, position, timeout, moveTcs);
                    moveTcs.Task.ContinueWith(t => tcs.TrySetResult(BuildResponse(id, t.Result)));
                    return;
                }

                if (cmd == "test_step")
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    var argsJson = JsonHelper.ExtractObject(json, "args");
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
                    var stepTcs = new TaskCompletionSource<string>();
                    RuntimeHelper.TestStep(path, position, checksBefore, checksAfter, waitAfter, timeout, stepTcs);
                    stepTcs.Task.ContinueWith(t => tcs.TrySetResult(BuildResponse(id, t.Result)));
                    return;
                }

                if (cmd == "run_playtest")
                {
                    var guard = CheckGuards(id, cmd);
                    if (guard != null) { tcs.TrySetResult(guard); return; }
                    var argsJson = JsonHelper.ExtractObject(json, "args");
                    var script = JsonHelper.ExtractString(argsJson, "script");
                    var timeoutStr = JsonHelper.ExtractString(argsJson, "timeout") ?? "120";
                    float.TryParse(timeoutStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float timeout);
                    if (timeout <= 0) timeout = 120f;
                    var runTcs = new TaskCompletionSource<string>();
                    PlaytestRunner.Run(script, timeout, runTcs);
                    runTcs.Task.ContinueWith(t => tcs.TrySetResult(BuildResponse(id, t.Result)));
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

        internal static string ExecuteCommand(string cmd, string args)
        {
            return CommandRegistry.Execute(cmd, args);
        }

        internal static void RegisterAll()
        {
            CommandRegistry.Clear();

            // Meta (non-mutating)
            CommandRegistry.Register("ping", _ => "pong");
            CommandRegistry.Register("get_version", _ => VersionTracker.Version.ToString());
            CommandRegistry.Register("get_enabled_tools", _ => ExecGetEnabledTools());

            // Read (non-mutating)
            CommandRegistry.Register("get_hierarchy", ExecGetHierarchy);
            CommandRegistry.Register("get_component", ExecGetComponent);
            CommandRegistry.Register("get_components_list", ExecGetComponentsList);
            CommandRegistry.Register("get_object_detail", ExecGetObjectDetail);
            CommandRegistry.Register("find_objects", ExecFindObjects);
            CommandRegistry.Register("get_console", ExecGetConsole);
            CommandRegistry.Register("get_compile_errors", _ => CompileErrorCapture.GetErrors());
            CommandRegistry.Register("compile_status", _ => CompileNotifier.GetStatus());
            // screenshot is intercepted in Process/ProcessAsync for file response formatting;
            // registered here only for IsRegistered/IsMutating queries
            CommandRegistry.Register("screenshot", _ => throw new InvalidOperationException("screenshot intercepted before ExecuteCommand"));
            CommandRegistry.Register("recompile", _ => { UnityEditor.AssetDatabase.Refresh(); return "ok"; });
            CommandRegistry.Register("search_scene", args => SearchHelper.Search(JsonHelper.ExtractString(args, "query")));
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
            CommandRegistry.Register("scan_scene", args => ScanHelper.Scan(
                JsonHelper.ExtractString(args, "bands")));
            CommandRegistry.Register("check_colliders", args => ColliderChecker.Check(
                JsonHelper.ExtractString(args, "path")));
            CommandRegistry.Register("get_schema", args => SchemaHelper.GetSchema(
                JsonHelper.ExtractString(args, "type")));
            CommandRegistry.Register("get_changes", args => ChangeWatcher.GetChanges(
                JsonHelper.ExtractString(args, "clear") != "false"));
            // run_tests is intercepted by ProcessAsync before ExecuteCommand; this entry exists for IsMutating/IsRegistered only
            CommandRegistry.Register("run_tests", _ => throw new InvalidOperationException("run_tests requires async dispatch via ProcessAsync"));
            CommandRegistry.Register("get_test_results", _ => TestRunner.GetResults());

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
            // wait_until, move_to, test_step, run_playtest intercepted by ProcessAsync; entries exist for IsRuntime guard
            CommandRegistry.Register("wait_until", _ => throw new InvalidOperationException("wait_until requires async dispatch via ProcessAsync"), runtime: true);
            CommandRegistry.Register("move_to", _ => throw new InvalidOperationException("move_to requires async dispatch via ProcessAsync"), runtime: true);
            CommandRegistry.Register("test_step", _ => throw new InvalidOperationException("test_step requires async dispatch via ProcessAsync"), runtime: true);
            CommandRegistry.Register("run_playtest", _ => throw new InvalidOperationException("run_playtest requires async dispatch via ProcessAsync"), runtime: true);
            CommandRegistry.Register("query_state", args => GameStateHelper.Snapshot(
                JsonHelper.ExtractString(args, "queries")), runtime: true);

            // Write (mutating)
            CommandRegistry.Register("create_object", ExecCreateObject, mutating: true);
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
                return BatchHelper.Execute(
                    JsonHelper.ExtractString(args, "commands"),
                    JsonHelper.ExtractString(args, "on_error") ?? "continue",
                    timeoutMs);
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

            // Code execution via Roslyn (mutating)
            CommandRegistry.Register("execute_code", args => CodeExecutor.Execute(
                JsonHelper.ExtractString(args, "code"),
                JsonHelper.ExtractString(args, "undo_label") ?? "execute_code"), mutating: true);

            PluginRegistry.RegisterAllPlugins();
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
            cmd == "ping" || cmd == "get_version" || cmd == "get_enabled_tools";

        internal static bool IsAllowedDuringCompile(string cmd) =>
            cmd == "ping" || cmd == "get_version" || cmd == "get_console" ||
            cmd == "screenshot" || cmd == "get_enabled_tools" || cmd == "compile_status";

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
            _enabledToolsCache = null;
        }

    }
}
