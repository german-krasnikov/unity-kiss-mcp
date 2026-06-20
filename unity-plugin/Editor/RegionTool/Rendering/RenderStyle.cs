using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal static class RenderStyle
    {
        // State colors
        public static readonly Color Drawing = new(0.2f, 0.5f, 1.0f, 0.90f);  // blue
        public static readonly Color Preview  = new(0.2f, 0.9f, 0.3f, 0.90f); // green

        // Fill = same hue, 12% alpha
        public static Color FillColor(Color c) => new(c.r, c.g, c.b, 0.12f);

        // Object highlight
        public static readonly Color ObjectHighlight = new(0f, 1f, 0.5f, 0.55f);

        // Vertex handles
        public static readonly Color VertexIdle  = new(1f, 1f, 1f, 0.85f);
        public static readonly Color VertexClose = new(0.2f, 1f, 0.3f, 1.00f);

        // Line widths
        public const float ContourWidth   = 2.5f;
        public const float GlowWidthOuter = 9f;
        public const float VertexSize     = 0.08f;  // handle size multiplier

        // Dotted line
        public const float DottedSpacing = 5f;
    }
}
