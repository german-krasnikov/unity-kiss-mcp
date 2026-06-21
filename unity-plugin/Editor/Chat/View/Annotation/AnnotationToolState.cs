using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal sealed class AnnotationToolState
    {
        internal AnnotationTool ActiveTool { get; set; } = AnnotationTool.Arrow;
        internal Color32 ActiveColor { get; set; } = new Color32(255, 50, 50, 255); // red default
        internal float StrokeWidth { get; set; } = 3f; // medium
        internal AnnotationFill FillMode { get; set; } = AnnotationFill.None;

        // Preset palette
        internal static readonly Color32[] Palette = new Color32[]
        {
            new Color32(255, 50, 50, 255),   // Red (default)
            new Color32(50, 150, 255, 255),   // Blue
            new Color32(50, 200, 50, 255),    // Green
            new Color32(255, 200, 0, 255),    // Yellow
            new Color32(255, 130, 0, 255),    // Orange
            new Color32(200, 50, 255, 255),   // Purple
            new Color32(255, 255, 255, 255),  // White
            new Color32(0, 0, 0, 255),        // Black
        };

        // Stroke width presets
        internal static readonly float[] WidthPresets = { 2f, 3f, 5f };

        internal void Reset()
        {
            ActiveTool = AnnotationTool.Arrow;
            ActiveColor = Palette[0];
            StrokeWidth = WidthPresets[1];
            FillMode = AnnotationFill.None;
        }
    }
}
