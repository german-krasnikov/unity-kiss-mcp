using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Two-click distance ruler. First click = A, second click = B → auto-commit.
    /// FinalizedPoints = [A, B] (2 vertices).
    /// PreviewVertices = [A, cursor] after first click.
    /// </summary>
    internal sealed class MeasurementMode : IAnnotationMode
    {
        Vector2 _a;
        Vector2 _cursor;
        bool _hasA;
        bool _gridSnap;
        readonly List<Vector2> _preview = new(2);

        public AnnotationModeId Id => AnnotationModeId.Measurement;
        public IReadOnlyList<Vector2> PreviewVertices => _preview;
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }
        public Vector2[] FinalizedPoints { get; private set; } = System.Array.Empty<Vector2>();

        public void Begin(Vector2 startXZ, bool gridSnap)
        {
            _gridSnap = gridSnap;
            _cursor = gridSnap ? DrawingUtils.Snap(startXZ) : startXZ;
            _hasA = false;
            IsActive = true;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
            _preview.Clear();
        }

        public bool OnEvent(Event e, Vector2 currentXZ)
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                _cursor = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;
                if (_hasA) UpdatePreview();
                return true;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                var xz = _gridSnap ? DrawingUtils.Snap(currentXZ) : currentXZ;

                if (!_hasA)
                {
                    _a = xz;
                    _hasA = true;
                    _cursor = xz;
                    UpdatePreview();
                }
                else
                {
                    FinalizedPoints = new[] { _a, xz };
                    IsComplete = true;
                    _preview.Clear();
                    _preview.Add(_a);
                    _preview.Add(xz);
                }
                return true;
            }

            return false;
        }

        public void Reset()
        {
            _hasA = false;
            _preview.Clear();
            IsActive = false;
            IsComplete = false;
            FinalizedPoints = System.Array.Empty<Vector2>();
        }

        public bool CanConfirm => false;
        public void ConfirmPending() { }

        void UpdatePreview()
        {
            _preview.Clear();
            _preview.Add(_a);
            _preview.Add(_cursor);
        }
    }
}
