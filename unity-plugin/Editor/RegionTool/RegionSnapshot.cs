using System;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Immutable snapshot of a drawn scene region. Persisted to Library/MCP_Regions.json.
    /// VerticesFlat = [x0,z0, x1,z1, ...] — flat pairs for JsonUtility compatibility.
    /// ObjectPaths capped at 50; TotalCount holds true count.
    /// </summary>
    [Serializable]
    internal sealed class RegionSnapshot
    {
        // Identity
        public string Id;
        public int    SchemaVersion = 1;

        // Geometry (XZ plane — Y always 0 in MVP)
        public float[] VerticesFlat;  // [x0,z0, x1,z1, ...]
        public float   Area;          // m², Shoelace
        public float   CenterX;
        public float   CenterZ;
        public float   MinX, MinZ, MaxX, MaxZ; // AABB

        // Scene context
        public string  SceneName;
        public float   PlaneY = 0f;

        // Objects (capped at 50)
        public string[] ObjectPaths;  // ComponentSerializer.GetPath() results
        public int[]    ObjectIds;    // GetInstanceID() — parallel with ObjectPaths
        public int      TotalCount;   // true count before cap
        public bool     Truncated;    // true if TotalCount > 50

        // Polygon detail level: -1 = use global default; 0–3 = PolygonDetailLevel override
        public int DetailLevel = -1;

        // Staleness
        public int  SnapshotVersion;  // set to SceneRegionState.CurrentVersion on creation
        public long CreatedTicks;     // DateTimeOffset.UtcNow.ToUnixTimeSeconds()

        public string ShortLabel =>
            $"{(Truncated ? TotalCount + "+" : (ObjectPaths?.Length.ToString() ?? "0"))}obj {Area:F0}m²";

        public static RegionSnapshot Create(
            string id,
            Polygon2D polygon,
            GameObject[] objects,
            string sceneName)
        {
            var verts = polygon.Vertices;
            var flat = new float[verts.Length * 2];
            for (int i = 0; i < verts.Length; i++)
            {
                flat[i * 2]     = verts[i].x;
                flat[i * 2 + 1] = verts[i].y;  // y = world Z in XZ plane
            }

            int cap   = Math.Min(objects.Length, 50);
            var paths = new string[cap];
            var ids   = new int[cap];
            for (int i = 0; i < cap; i++)
            {
                paths[i] = ComponentSerializer.GetPath(objects[i]);
                ids[i]   = objects[i].GetInstanceID();
            }

            var centroid = polygon.Centroid();
            var bounds   = polygon.ComputeBounds();

            return new RegionSnapshot
            {
                Id              = id,
                SchemaVersion   = 1,
                VerticesFlat    = flat,
                Area            = polygon.Area(),
                CenterX         = centroid.x,
                CenterZ         = centroid.y,
                MinX            = bounds.xMin, MinZ = bounds.yMin,
                MaxX            = bounds.xMax, MaxZ = bounds.yMax,
                SceneName       = sceneName ?? "",
                PlaneY          = 0f,
                ObjectPaths     = paths,
                ObjectIds       = ids,
                TotalCount      = objects.Length,
                Truncated       = objects.Length > 50,
                CreatedTicks    = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        /// <summary>Reconstruct Polygon2D from flat array (for Navigate/FrameRegion).</summary>
        public Polygon2D ToPolygon2D()
        {
            if (VerticesFlat == null || VerticesFlat.Length < 6) return default;
            var verts = new Vector2[VerticesFlat.Length / 2];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new Vector2(VerticesFlat[i * 2], VerticesFlat[i * 2 + 1]);
            return new Polygon2D(verts);
        }
    }
}
