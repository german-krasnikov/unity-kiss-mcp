using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.RegionTool
{
    internal static class SceneAnnotationUtils
    {
        public static float ComputeLength(IReadOnlyList<Vector2> verts)
        {
            float len = 0f;
            for (int i = 1; i < verts.Count; i++)
                len += Vector2.Distance(verts[i - 1], verts[i]);
            return len;
        }

        public static bool MouseToXZ(Vector2 guiPos, out Vector2 xz)
        {
            var ray   = HandleUtility.GUIPointToWorldRay(guiPos);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                var hit = ray.GetPoint(enter);
                xz = new Vector2(hit.x, hit.z);
                return true;
            }
            xz = default;
            return false;
        }

        public static string GenerateId() => Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>Returns near-object paths for Polyline annotations; empty for other types.</summary>
        public static string[] PolyNearPaths(AnnotationModeId modeId, Vector2[] pts)
        {
            if (modeId != AnnotationModeId.Polyline) return Array.Empty<string>();
            var gos = SceneRegionQuery.FindNearPolyline(pts, 2f, 50);
            var paths = new string[gos.Length];
            for (int i = 0; i < gos.Length; i++) paths[i] = ComponentSerializer.GetPath(gos[i]);
            return paths;
        }
    }
}
