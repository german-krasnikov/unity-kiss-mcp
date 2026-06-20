using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal static class DrawingUtils
    {
        public static Vector2 Snap(Vector2 v, float grid = 0.5f) =>
            new(Mathf.Round(v.x / grid) * grid, Mathf.Round(v.y / grid) * grid);

        public static float SnapRadius(float r, float grid = 0.5f) =>
            Mathf.Round(r / grid) * grid;
    }
}
