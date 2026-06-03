// Draggable splitter that resizes the input panel; transcript above flexes to fill the rest.
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private const float InputMinH = InputHeightCalc.CompactH, InputMaxH = 500f;

        private VisualElement BuildResizeHandle(VisualElement target)
        {
            var handle = new VisualElement();
            handle.AddToClassList("resize-handle");

            // Centered grip pill (purely visual affordance; brightens on hover via USS).
            var grip = new VisualElement(); grip.AddToClassList("resize-handle-grip");
            handle.Add(grip);

            var drag = false;
            float startY = 0f, startH = 0f;

            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                drag = true; startY = e.position.y; startH = target.resolvedStyle.height;
                handle.CapturePointer(e.pointerId); e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!drag) return;
                var h = Mathf.Clamp(startH + (startY - e.position.y), InputMinH, InputMaxH);
                target.style.height = h;
                _heightCalc.SetManual(h);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                drag = false; handle.ReleasePointer(e.pointerId); e.StopPropagation();
            });
            return handle;
        }
    }
}
