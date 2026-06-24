using UnityEngine;
using UnityMCP.Editor.UI;

namespace UnityMCP.Editor.RegionTool
{
    internal static class RegionIcons
    {
        private const int S = 16;

        private static Texture2D _lasso, _rect, _circle, _pbp;

        internal static Texture2D Lasso  => _lasso  ??= MakeLasso();
        internal static Texture2D Rect   => _rect   ??= MakeRect();
        internal static Texture2D Circle => _circle ??= MakeCircle();
        internal static Texture2D PbP    => _pbp    ??= MakePbP();

        private static Texture2D MakeLasso() =>
            IconCanvas.New(S)
                .Closed((8,13),(3,10),(2,6),(4,3),(7,1),(11,2),(13,5),(13,9),(10,12))
                .Line(8, 13, 7, 11)   // hook tail
                .Build();

        private static Texture2D MakeRect() =>
            IconCanvas.New(S)
                .Closed((2,3),(13,3),(13,12),(2,12))
                .Build();

        private static Texture2D MakeCircle() =>
            IconCanvas.New(S)
                .Circle(8, 8, 5, 5)
                .Build();

        private static Texture2D MakePbP() =>
            IconCanvas.New(S)
                .Closed((8,13),(2,9),(4,3),(12,3),(14,9))
                .Dot(8,13).Dot(2,9).Dot(4,3).Dot(12,3).Dot(14,9)
                .Build();
    }
}
