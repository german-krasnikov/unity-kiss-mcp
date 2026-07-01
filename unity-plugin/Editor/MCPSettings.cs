using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public static class MCPSettings
    {
        // ── EditorPrefs API (P0 — must stay intact) ──────────────────────────
        internal const string KeyPrefix      = "UnityMCP_Tool_";
        private const string KeyCatalog     = "UnityMCP_Catalog";

        public static bool IsToolEnabled(string toolName) =>
            EditorPrefs.GetBool(KeyPrefix + toolName, true);

        // ── Catalog persistence (P1) ─────────────────────────────────────────
        public static void SetCatalog(string json) =>
            EditorPrefs.SetString(KeyCatalog, json);

        public static string GetCatalog() =>
            EditorPrefs.GetString(KeyCatalog, null);

        // Minimal built-in default — used when no Python catalog received yet.
        private static readonly Dictionary<string, string[]> _defaultCatalog =
            new Dictionary<string, string[]>
            {
                { "CORE",       new[] { "get_hierarchy","get_component","inspect","batch","set_property","create_object","delete_object","manage_component","scene","search_scene","set_parent","get_console","get_compile_errors","get_enabled_tools","discover_tools","editor","do","ask","reconnect_unity","list_connections" } },
                { "SCENE_EDIT", new[] { "find_objects","get_object_detail","get_components_list","set_active","set_material","set_property_delta" } },
                { "COMPONENTS", new[] { "wire_event","unwire_event" } },
                { "ANIMATION",  new[] { "animation","timeline","animator","particle" } },
                { "SHADERS_MATERIAL", new[] { "shader","material","references" } },
                { "VFX",        new[] { "vfx_intent" } },
                { "UI",         new[] { "create_ui","set_rect","validate_layout","get_spatial_context","ui_intent" } },
                { "SCREENSHOTS",new[] { "screenshot","screenshot_baseline","screenshot_compare" } },
                { "UNIT_TESTS", new[] { "run_tests","get_test_results","run_playtest","test_step" } },
                { "RUNTIME",    new[] { "invoke_method","set_runtime_property","wait_until","move_to","query_state" } },
                { "ASSETS",     new[] { "asset","prefab","scriptable_object","project_settings" } },
                { "ADVANCED_CODE", new[] { "execute_code","recompile","find_references","semantic_at","compile_preflight","get_schema","auto_fix","smart_build","checkpoint","validate_references","menu" } },
                { "SESSION_SKILLS", new[] { "save_skill","use_skill","list_skills","apply_template","save_template","list_templates","fingerprint","scene_diff","get_changes","save_session","load_session" } },
                { "META",       new[] { "animator_intent","get_metrics","setup_objects","set_properties","configure_objects","scan_scene","check_colliders","spatial_query" } },
            };

        // Returns catalog categories (from EditorPrefs JSON or built-in default).
        public static Dictionary<string, string[]> GetCatalogCategories()
        {
            var raw = GetCatalog();
            if (!string.IsNullOrEmpty(raw))
            {
                try
                {
                    var parsed = CatalogParser.Parse(raw);
                    if (parsed.Count > 0) return parsed;
                }
                catch { /* fall through */ }
            }
            return _defaultCatalog;
        }

        // ── Tool name list (P0 backward-compat) ──────────────────────────────
        public static string[] GetToolNames()
        {
            var all = new List<string>();
            foreach (var kv in GetCatalogCategories())
                all.AddRange(kv.Value);
            var pluginTools = PluginRegistry.GetAllPluginToolNames();
            all.AddRange(pluginTools);
            return all.ToArray();
        }

        // ── Init / lifecycle ─────────────────────────────────────────────────
        static MCPSettings()
        {
            EditorApplication.wantsToQuit += OnWantsToQuit;
        }

        private static bool OnWantsToQuit() => true;
    }
}
