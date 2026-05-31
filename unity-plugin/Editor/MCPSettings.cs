using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    public class MCPSettings : EditorWindow
    {
        private static readonly string[] CoreToolNames =
        {
            "get_hierarchy", "get_component", "get_components_list",
            "get_object_detail", "find_objects", "set_property",
            "create_object", "delete_object", "manage_component",
            "get_console", "screenshot", "run_tests", "recompile",
            "search_scene", "set_material", "batch",
            "create_ui", "set_rect",
            "scene", "animation", "timeline", "references", "editor", "animator",
            "particle",
            "shader",
            "inspect",
            "menu",
            "checkpoint",
            "validate_references",
            "set_active",
            "wire_event",
            "asset",
            "project_settings",
            "material",
            "prefab",
            "scriptable_object",
            "execute_code"
        };

        public static string[] GetToolNames()
        {
            var pluginTools = PluginRegistry.GetAllPluginToolNames();
            if (pluginTools.Length == 0) return CoreToolNames;
            var all = new string[CoreToolNames.Length + pluginTools.Length];
            CoreToolNames.CopyTo(all, 0);
            pluginTools.CopyTo(all, CoreToolNames.Length);
            return all;
        }

        private const string KeyPrefix = "UnityMCP_Tool_";
        private const string KeyAutoDiscard = "UnityMCP_AutoDiscardScene";
        private Vector2 _scroll;

        public static bool IsToolEnabled(string toolName) =>
            EditorPrefs.GetBool(KeyPrefix + toolName, true);

        public static bool AutoDiscardScene =>
            EditorPrefs.GetBool(KeyAutoDiscard, true);

        static MCPSettings()
        {
            EditorApplication.wantsToQuit += OnWantsToQuit;
        }

        private static bool OnWantsToQuit()
        {
            if (!AutoDiscardScene) return true;

            var scene = SceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                Debug.LogWarning("[MCP] Auto-discarding dirty scene on quit");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            return true;
        }

        [MenuItem("Tools/Unity MCP/Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPSettings>("MCP Settings");
            window.minSize = new Vector2(250, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("MCP Tool Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Disabled tools will return an error when called.", MessageType.Info);
            EditorGUILayout.Space();

            // Auto-discard toggle
            var autoDiscard = EditorGUILayout.Toggle("Auto-discard scene on quit", AutoDiscardScene);
            if (autoDiscard != AutoDiscardScene)
                EditorPrefs.SetBool(KeyAutoDiscard, autoDiscard);

            EditorGUILayout.Space();
            GUILayout.Label("Tool Toggles", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var tool in GetToolNames())
            {
                var key = KeyPrefix + tool;
                var enabled = EditorPrefs.GetBool(key, true);
                var newVal = EditorGUILayout.Toggle(tool, enabled);
                if (newVal != enabled)
                    EditorPrefs.SetBool(key, newVal);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All"))
                SetAll(true);
            if (GUILayout.Button("Disable All"))
                SetAll(false);
            EditorGUILayout.EndHorizontal();
        }

        private static void SetAll(bool enabled)
        {
            foreach (var tool in GetToolNames())
                EditorPrefs.SetBool(KeyPrefix + tool, enabled);
        }
    }
}
