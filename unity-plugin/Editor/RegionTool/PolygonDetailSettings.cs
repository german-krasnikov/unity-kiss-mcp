using UnityEditor;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Persists global polygon detail level in EditorPrefs and resolves per-region overrides.
    /// Per-region override is stored as int in RegionSnapshot.DetailLevel; -1 = use global.
    /// </summary>
    internal static class PolygonDetailSettings
    {
        const string PrefKey = "MCP_PolygonDetailLevel";

        /// <summary>Global default persisted in EditorPrefs.</summary>
        public static PolygonDetailLevel Default
        {
            get => (PolygonDetailLevel)EditorPrefs.GetInt(PrefKey, (int)PolygonDetailLevel.Normal);
            set => EditorPrefs.SetInt(PrefKey, (int)value);
        }

        /// <summary>
        /// Resolve effective detail level for a region.
        /// snap.DetailLevel >= 0 overrides global default; -1 uses Default.
        /// </summary>
        public static PolygonDetailLevel ForRegion(RegionSnapshot snap)
        {
            if (snap != null && snap.DetailLevel >= 0)
                return (PolygonDetailLevel)snap.DetailLevel;
            return Default;
        }
    }
}
