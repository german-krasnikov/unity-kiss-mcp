using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal enum AnnotationTool { Pen, Line, Arrow, Rect, Ellipse, Text, Erase }
    internal enum AnnotationFill { None, Solid, SemiTransparent }

    internal interface IAnnotationCommand
    {
        AnnotationTool Tool { get; }
        Color32 Color { get; }
        float StrokeWidth { get; }
        AnnotationFill Fill { get; }
        // All coords are normalized 0..1 (resolution-independent)
        IReadOnlyList<Vector2> Points { get; }
        // For Text command only
        string Text { get; }
    }

    internal sealed class PenCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Pen;
        public Color32 Color { get; }
        public float StrokeWidth { get; }
        public AnnotationFill Fill => AnnotationFill.None;
        public IReadOnlyList<Vector2> Points { get; }
        public string Text => null;

        public PenCommand(Color32 color, float strokeWidth, List<Vector2> points)
        {
            Color = color; StrokeWidth = strokeWidth;
            Points = points.ToArray(); // defensive copy
        }
    }

    internal sealed class LineCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Line;
        public Color32 Color { get; }
        public float StrokeWidth { get; }
        public AnnotationFill Fill => AnnotationFill.None;
        public IReadOnlyList<Vector2> Points { get; }
        public string Text => null;

        public LineCommand(Color32 color, float strokeWidth, Vector2 start, Vector2 end)
        {
            Color = color; StrokeWidth = strokeWidth;
            Points = new[] { start, end };
        }
    }

    internal sealed class ArrowCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Arrow;
        public Color32 Color { get; }
        public float StrokeWidth { get; }
        public AnnotationFill Fill => AnnotationFill.None;
        public IReadOnlyList<Vector2> Points { get; }
        public string Text => null;

        public ArrowCommand(Color32 color, float strokeWidth, Vector2 start, Vector2 end)
        {
            Color = color; StrokeWidth = strokeWidth;
            Points = new[] { start, end };
        }
    }

    internal sealed class RectCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Rect;
        public Color32 Color { get; }
        public float StrokeWidth { get; }
        public AnnotationFill Fill { get; }
        public IReadOnlyList<Vector2> Points { get; }
        public string Text => null;

        public RectCommand(Color32 color, float strokeWidth, AnnotationFill fill, Vector2 corner1, Vector2 corner2)
        {
            Color = color; StrokeWidth = strokeWidth; Fill = fill;
            Points = new[] { corner1, corner2 };
        }
    }

    internal sealed class EllipseCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Ellipse;
        public Color32 Color { get; }
        public float StrokeWidth { get; }
        public AnnotationFill Fill { get; }
        public IReadOnlyList<Vector2> Points { get; }
        public string Text => null;

        public EllipseCommand(Color32 color, float strokeWidth, AnnotationFill fill, Vector2 center, Vector2 radiusPoint)
        {
            Color = color; StrokeWidth = strokeWidth; Fill = fill;
            Points = new[] { center, radiusPoint };
        }
    }

    internal sealed class TextCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Text;
        public Color32 Color { get; }
        public float StrokeWidth => 0f;
        public AnnotationFill Fill => AnnotationFill.None;
        public IReadOnlyList<Vector2> Points { get; }
        public string Text { get; }

        public TextCommand(Color32 color, Vector2 position, string text)
        {
            Color = color; Points = new[] { position };
            Text = text ?? "";
        }
    }
}
