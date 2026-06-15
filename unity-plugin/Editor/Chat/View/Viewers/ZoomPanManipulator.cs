// Zoom + pan manipulator for UIToolkit content elements (image/mermaid viewers).
// Uses style.scale / style.translate (not deprecated transform.*).
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public class ZoomPanManipulator : Manipulator
    {
        public float Zoom { get; private set; } = 1f;
        public float PanX { get; private set; }
        public float PanY { get; private set; }

        private bool   _dragging;
        private Vector2 _lastPos;

        private readonly VisualElement _content;

        public ZoomPanManipulator(VisualElement content)
        {
            _content = content;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<WheelEvent>(OnWheel);
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<WheelEvent>(OnWheel);
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        private void Apply()
        {
            if (_content == null) return;
            _content.style.scale     = new StyleScale(new Scale(new Vector2(Zoom, Zoom)));
            _content.style.translate = new StyleTranslate(new Translate(PanX, PanY));
        }

        public void Reset()
        {
            Zoom = 1f; PanX = 0f; PanY = 0f;
            Apply();
        }

        private void OnWheel(WheelEvent evt)
        {
            float delta = -evt.delta.y * 0.05f;
            Zoom = Mathf.Clamp(Zoom + delta, 0.1f, 10f);
            Apply();
            evt.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.button != 0) return;
            _dragging = true;
            _lastPos  = evt.mousePosition;
            target.CaptureMouse();
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_dragging) return;
            var delta = evt.mousePosition - _lastPos;
            _lastPos = evt.mousePosition;
            PanX += delta.x;
            PanY += delta.y;
            Apply();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (!_dragging) return;
            _dragging = false;
            target.ReleaseMouse();
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            if (_dragging) { _dragging = false; target.ReleaseMouse(); }
        }
    }
}
