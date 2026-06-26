using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Draws watch values as Scene View labels near each watched GameObject.
    // Labels are culled at CullDistance meters from camera.
    [InitializeOnLoad]
    internal static class DebugOverlayDrawer
    {
        private const float CullDistance = 50f;

        private static readonly GUIStyle StyleNormal = new GUIStyle
            { normal = { textColor = new Color(0.23f, 0.82f, 0.62f) } };
        private static readonly GUIStyle StyleTriggered = new GUIStyle
            { normal = { textColor = new Color(0.91f, 0.28f, 0.23f) } };

        private static readonly Dictionary<string, WeakReference<GameObject>> _goCache = new();

        static DebugOverlayDrawer()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            AssemblyReloadEvents.beforeAssemblyReload += () => SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.hierarchyChanged += () => _goCache.Clear();
        }

        private static void OnSceneGUI(SceneView sv)
        {
            if (WatchRegistry.All.Count == 0) return;
            var cam = sv.camera;
            if (cam == null) return;

            var camPos = cam.transform.position;

            foreach (var (_, entry) in WatchRegistry.All)
            {
                if (!_goCache.TryGetValue(entry.Path, out var wr) || !wr.TryGetTarget(out var go))
                {
                    go = ComponentSerializer.FindObject(entry.Path);
                    if (go != null)
                        _goCache[entry.Path] = new WeakReference<GameObject>(go);
                }
                if (go == null) continue;

                var pos = go.transform.position;
                if (Vector3.Distance(camPos, pos) > CullDistance) continue;

                var text = $"{entry.Field}={entry.LastValue ?? "–"}";
                var style = entry.Triggered ? StyleTriggered : StyleNormal;
                Handles.Label(pos + Vector3.up * 0.35f, text, style);
            }
        }
    }
}
