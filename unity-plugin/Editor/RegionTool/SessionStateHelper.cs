using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Shadow-persists RegionSnapshot instances to UnityEditor.SessionState so snaps
    /// survive domain reload even when the Library/ JSON write fails silently.
    /// Main-thread only. All SessionState calls are in-process and never throw.
    /// </summary>
    internal static class SessionStateHelper
    {
        const string IdsKey   = "MCP_SnapIds";
        const string SnapPfx  = "MCP_Snap_";

        internal static void Cache(RegionSnapshot snap)
        {
            SessionState.SetString(SnapPfx + snap.Id, JsonUtility.ToJson(snap));
            var ids = GetIds();
            if (!ids.Contains(snap.Id)) ids.Add(snap.Id);
            while (ids.Count > SceneRegionState.MaxRegions) ids.RemoveAt(0);
            SessionState.SetString(IdsKey, string.Join(",", ids));
        }

        internal static void RecoverInto(Dictionary<string, RegionSnapshot> cache, long cutoff)
        {
            foreach (var id in GetIds())
            {
                if (cache.ContainsKey(id)) continue;
                var json = SessionState.GetString(SnapPfx + id, "");
                if (string.IsNullOrEmpty(json)) continue;
                try
                {
                    var r = JsonUtility.FromJson<RegionSnapshot>(json);
                    if (r?.Id != null && r.CreatedTicks > cutoff)
                        cache[r.Id] = r;
                }
                catch { /* corrupt entry — skip */ }
            }
        }

        internal static void Remove(string id)
        {
            SessionState.EraseString(SnapPfx + id);
            var ids = GetIds();
            ids.Remove(id);
            SessionState.SetString(IdsKey, string.Join(",", ids));
        }

        internal static void ClearAll()
        {
            foreach (var id in GetIds())
                SessionState.EraseString(SnapPfx + id);
            SessionState.EraseString(IdsKey);
        }

        static List<string> GetIds()
        {
            var s = SessionState.GetString(IdsKey, "");
            return string.IsNullOrEmpty(s)
                ? new List<string>()
                : new List<string>(s.Split(','));
        }
    }
}
