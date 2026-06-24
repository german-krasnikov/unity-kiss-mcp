using UnityEngine;
using UnityMCP.Editor.UI;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal static class AnnotationIcons
    {
        private const int S = 18;

        private static Texture2D _pen, _line, _arrow, _rect, _ellipse, _text, _erase;
        private static Texture2D _undo, _redo, _clear, _cube3d, _send;
        private static Texture2D _widthS, _widthM, _widthL;

        internal static Texture2D Pen     => _pen     ??= MakePen();
        internal static Texture2D Line    => _line    ??= MakeLine();
        internal static Texture2D Arrow   => _arrow   ??= MakeArrow();
        internal static Texture2D Rect    => _rect    ??= MakeRect();
        internal static Texture2D Ellipse => _ellipse ??= MakeEllipse();
        internal static Texture2D Text    => _text    ??= MakeText();
        internal static Texture2D Erase   => _erase   ??= MakeErase();
        internal static Texture2D Undo    => _undo    ??= MakeUndo();
        internal static Texture2D Redo    => _redo    ??= MakeRedo();
        internal static Texture2D Clear   => _clear   ??= MakeClear();
        internal static Texture2D Cube3D  => _cube3d  ??= MakeCube3D();
        internal static Texture2D Send    => _send    ??= MakeSend();
        internal static Texture2D WidthS  => _widthS  ??= MakeWidthDot(2);
        internal static Texture2D WidthM  => _widthM  ??= MakeWidthDot(4);
        internal static Texture2D WidthL  => _widthL  ??= MakeWidthDot(6);

        private static Texture2D MakePen() =>
            IconCanvas.New()
                .Poly((3,4),(7,12),(11,6),(15,14))
                .Build();

        private static Texture2D MakeLine() =>
            IconCanvas.New()
                .Line(3, 3, 14, 14)
                .Build();

        private static Texture2D MakeArrow() =>
            IconCanvas.New()
                .Line(3, 14, 14, 3)
                .Line(14, 3,  9, 4)
                .Line(14, 3, 13, 8)
                .Build();

        private static Texture2D MakeRect() =>
            IconCanvas.New()
                .Closed((3,4),(14,4),(14,13),(3,13))
                .Build();

        private static Texture2D MakeEllipse() =>
            IconCanvas.New()
                .Circle(9, 9, 6, 4)
                .Build();

        private static Texture2D MakeText() =>
            IconCanvas.New()
                .Line(4, 13, 14, 13)   // top crossbar
                .Line(9, 13,  9,  3)   // stem
                .Line(7,  3, 11,  3)   // top serif
                .Build();

        private static Texture2D MakeErase() =>
            IconCanvas.New()
                .Closed((3,5),(10,5),(15,13),(8,13))   // outline
                .Line(7, 5, 12, 13)                    // interior divider — 2px (was 1px)
                .Build();

        private static Texture2D MakeUndo() =>
            IconCanvas.New()
                .Poly((5,7),(7,13),(11,14),(14,11))   // arc body
                .Line(5, 7, 3, 10)                    // arrow left
                .Line(5, 7, 8,  9)                    // arrow right
                .Build();

        private static Texture2D MakeRedo() =>
            IconCanvas.New()
                .Poly((13,7),(11,13),(7,14),(4,11))   // arc body
                .Line(13, 7, 15, 10)                  // arrow right
                .Line(13, 7, 10,  9)                  // arrow left
                .Build();

        private static Texture2D MakeClear() =>
            IconCanvas.New()
                .Poly((5,4),(5,13))                    // left side
                .Poly((5,4),(13,4),(13,13))            // top + right side
                .Line(4, 13, 14, 13)                   // rim
                .Closed((7,14),(7,16),(11,16),(11,14)) // lid (handle)
                .Line(8, 12,  8,  6)                   // inner line 1 — 2px (was 1px)
                .Line(10, 12, 10,  6)                  // inner line 2 — 2px (was 1px)
                .Build();

        private static Texture2D MakeCube3D() =>
            IconCanvas.New()
                .Closed((3,3),(10,3),(10,10),(3,10))   // front face
                .Closed((7,7),(14,7),(14,14),(7,14))   // back face
                .Line(3,  3,  7,  7)
                .Line(10, 3, 14,  7)
                .Line(10,10, 14, 14)
                .Line(3, 10,  7, 14)
                .Build();

        private static Texture2D MakeSend() =>
            IconCanvas.New()
                .Closed((2,9),(15,14),(6,4))   // outer triangle (wing shape)
                .Line(6, 4, 6, 11)             // fold line
                .Build();

        private static Texture2D MakeWidthDot(int radius) =>
            IconCanvas.New()
                .Disc(S / 2f, S / 2f, radius)
                .Build();
    }
}
