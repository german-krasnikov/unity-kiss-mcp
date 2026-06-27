using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Isolated config storage for MCP plugins. Keys namespaced as MCPPlugin_{pluginId}_{key}.
    /// All methods must be called from the main thread (EditorPrefs constraint).
    /// </summary>
    public static class PluginConfig
    {
        internal static string BuildKey(string pluginId, string key)
            => $"MCPPlugin_{pluginId}_{key}";

        public static string GetString(string pluginId, string key, string defaultValue = "")
            => EditorPrefs.GetString(BuildKey(pluginId, key), defaultValue);

        public static void SetString(string pluginId, string key, string value)
            => EditorPrefs.SetString(BuildKey(pluginId, key), value);

        public static bool GetBool(string pluginId, string key, bool defaultValue = false)
            => EditorPrefs.GetBool(BuildKey(pluginId, key), defaultValue);

        public static void SetBool(string pluginId, string key, bool value)
            => EditorPrefs.SetBool(BuildKey(pluginId, key), value);

        public static int GetInt(string pluginId, string key, int defaultValue = 0)
            => EditorPrefs.GetInt(BuildKey(pluginId, key), defaultValue);

        public static void SetInt(string pluginId, string key, int value)
            => EditorPrefs.SetInt(BuildKey(pluginId, key), value);

        public static float GetFloat(string pluginId, string key, float defaultValue = 0f)
            => EditorPrefs.GetFloat(BuildKey(pluginId, key), defaultValue);

        public static void SetFloat(string pluginId, string key, float value)
            => EditorPrefs.SetFloat(BuildKey(pluginId, key), value);

        public static void Delete(string pluginId, string key)
            => EditorPrefs.DeleteKey(BuildKey(pluginId, key));
    }
}
