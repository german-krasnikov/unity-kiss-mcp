using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    internal struct RenderState
    {
        public bool                   IsDrawing;
        public bool                   IsPreview;
        public DrawingModeId          Mode;
        public IReadOnlyList<Vector2> Vertices;
        public GameObject[]           MatchedObjects;
        public bool                   GridSnap;
        public Vector2                CursorXZ;       // for PbP close indicator
        public int                    VertexCount;    // simplified count for HUD
        public int                    RawVertexCount;
        public float                  Area;
        public int                    ObjectCount;
        public int                    TokenEstimate;
    }
}
