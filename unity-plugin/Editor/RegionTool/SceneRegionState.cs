using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// In-memory + file-persisted registry of RegionSnapshot instances.
    /// Thread-safety: main thread only (Editor API).
    /// Persistence: Library/MCP_Regions.json (gitignored, survives domain reload + restart).
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneRegionState
    {
        // ── Test seams ───────────────────────────────────────────────────────
        internal static string PersistPath = DefaultPath();
        internal static int    MaxRegions  = 20;

        static readonly Dictionary<string, RegionSnapshot> _cache = new();

        static SceneRegionState()
        {
            Load();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        // ── Version tracking (staleness) ─────────────────────────────────────
        static int _globalVersion;

        static void OnHierarchyChanged() => _globalVersion++;

        internal static int CurrentVersion => _globalVersion;

        // ── CRUD ─────────────────────────────────────────────────────────────

        /// <summary>Add or replace. Returns the ID used.</summary>
        internal static string SetRegion(RegionSnapshot snap)
        {
            if (string.IsNullOrEmpty(snap.Id))
                throw new System.ArgumentException("snap.Id must be set by caller before SetRegion.");
            snap.SnapshotVersion = _globalVersion;
            _cache[snap.Id] = snap;
            if (_cache.Count > MaxRegions) Evict();
            Save();
            return snap.Id;
        }

        internal static RegionSnapshot GetById(string id)
        {
            if (id == null) return null;
            _cache.TryGetValue(id, out var snap);
            return snap;
        }

        internal static bool Remove(string id)
        {
            var removed = _cache.Remove(id);
            if (removed) Save();
            return removed;
        }

        internal static IReadOnlyCollection<RegionSnapshot> All => _cache.Values;

        internal static bool IsStale(string id)
        {
            var snap = GetById(id);
            return snap != null && snap.SnapshotVersion != _globalVersion;
        }

        // ── Navigation (used by RegionChipProvider) ───────────────────────────

        internal static void FrameRegion(string id)
        {
            var snap = GetById(id);
            if (snap == null) { Debug.LogWarning("[MCP] Region not found: " + id); return; }
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            var center = new Vector3(snap.CenterX, 0f, snap.CenterZ);
            var size   = Mathf.Max(snap.MaxX - snap.MinX, snap.MaxZ - snap.MinZ, 1f);
            sv.Frame(new Bounds(center, Vector3.one * size * 1.5f), instant: false);
        }

        internal static void HighlightRegion(string id) => FrameRegion(id);

        // ── Persistence ───────────────────────────────────────────────────────

        internal static void Save()
        {
            var list  = new List<RegionSnapshot>(_cache.Values);
            var store = new RegionStore { Regions = list.ToArray() };
            try
            {
                File.WriteAllText(PersistPath, JsonUtility.ToJson(store, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MCP] RegionStore save failed: " + e.Message);
            }
        }

        internal static void Load()
        {
            _cache.Clear();
            if (!File.Exists(PersistPath)) return;
            try
            {
                var store = JsonUtility.FromJson<RegionStore>(File.ReadAllText(PersistPath));
                if (store?.Regions == null) return;
                long cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400; // 24h
                foreach (var r in store.Regions)
                    if (r?.Id != null && r.CreatedTicks > cutoff)
                        _cache[r.Id] = r;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MCP] RegionStore load failed: " + e.Message);
            }
        }

        internal static void Clear()
        {
            _cache.Clear();
            try { File.Delete(PersistPath); } catch { /* ignore */ }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static string DefaultPath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Regions.json"));

        static void Evict()
        {
            RegionSnapshot oldest = null;
            foreach (var r in _cache.Values)
                if (oldest == null || r.CreatedTicks < oldest.CreatedTicks)
                    oldest = r;
            if (oldest != null) _cache.Remove(oldest.Id);
        }

        [Serializable]
        private sealed class RegionStore
        {
            public RegionSnapshot[] Regions;
        }
    }
}
