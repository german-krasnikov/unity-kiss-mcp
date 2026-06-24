using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Single-click annotation mode. One click = placement complete.
    /// FinalizedPoints = [clickPoint] (1 vertex).
    /// PreviewVertices = [cursor] during hover.
    /// </summary>
    internal sealed class PointMode : IAnnotationMode
    {
        Vector2 _cursor;
        Vector2 _point;
        bool _gridSnap;
        readonly Vector2[] _preview = new Vector2[1];

        public AnnotationModeId Id => AnnotationModeId.Point;
        public IReadOnlyList<Vector2> PreviewVertices => _preview;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }
        public Vector2[] FinalizedPoints { get; private set; } = System.Array.Empty<Vector2>();

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _gridSnap = gridSnap;
            _cursor = gridSnap ? DrawingUtils.Snap(startXZ) : startXZ;
            _preview[0] = _cursor;
            IsActive = true;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                _cursor = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;
                _preview[0] = _cursor;
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                _point = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;
                FinalizedPoints = new[] { _point };
                IsComplete = true;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            IsActive = false;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
        }

        public bool CanConfirm => false;
        public void ConfirmPending() { }
    }
}
