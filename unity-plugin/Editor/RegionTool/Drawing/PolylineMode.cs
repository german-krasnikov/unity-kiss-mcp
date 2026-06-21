using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Click-chain polyline mode. No auto-closing (unlike PointByPointMode).
    /// Click = add vertex. Enter or double-click at >= 2 vertices = finalize.
    /// Backspace = remove last vertex.
    /// FinalizedPoints = open line (first != last).
    /// </summary>
    internal sealed class PolylineMode : IAnnotationMode
    {
        readonly List<Vector2> _vertices = new();
        Vector2 _cursor;
        bool _gridSnap;
        readonly List<Vector2> _preview = new();

        public AnnotationModeId Id => AnnotationModeId.Polyline;
        public IReadOnlyList<Vector2> PreviewVertices => _preview;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }
        public Vector2[] FinalizedPoints { get; private set; } = System.Array.Empty<Vector2>();

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _vertices.Clear();
            _gridSnap = gridSnap;
            _cursor = gridSnap ? DrawingUtils.Snap(startXZ) : startXZ;
            // First vertex added on first MouseDown, not here (matches PointMode/MeasurementMode).
            IsActive = true;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
            RebuildPreview();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                _cursor = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;
                RebuildPreview();
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var xz = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;

                if (e.clickCount >= 2 && _vertices.Count >= 1)
                {
                    _vertices.Add(xz);
                    Commit();
                    return true;
                }

                _vertices.Add(xz);
                _cursor = xz;
                RebuildPreview();
                return true;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
            {
                if (_vertices.Count >= 2)
                    Commit();
                return true;
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Backspace)
            {
                if (_vertices.Count > 0)
                    _vertices.RemoveAt(_vertices.Count - 1);
                RebuildPreview();
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _vertices.Clear();
            _preview.Clear();
            IsActive = false;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
        }

        void Commit()
        {
            FinalizedPoints = _vertices.ToArray();
            IsComplete = true;
            RebuildPreview();
        }

        void RebuildPreview()
        {
            _preview.Clear();
            _preview.AddRange(_vertices);
            if (IsActive && !IsComplete)
                _preview.Add(_cursor);
        }
    }
}
