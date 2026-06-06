using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class MCPHubDivider
    {
        // scheduleHost must be panel-attached for animation to fire
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var row = new VisualElement();
            row.AddToClassList("hub-divider");

            var lineLeft = new VisualElement();
            lineLeft.AddToClassList("hub-divider-line");

            var spike = new VisualElement();
            spike.AddToClassList("hub-divider-spike");
            spike.style.rotate = new StyleRotate(new Rotate(new Angle(45f, AngleUnit.Degree)));

            var lineRight = new VisualElement();
            lineRight.AddToClassList("hub-divider-line");

            row.Add(lineLeft);
            row.Add(spike);
            row.Add(lineRight);

            // Animate: toggle beat class every 1800ms
            scheduleHost.schedule.Execute(() =>
            {
                spike.ToggleInClassList("beat");
                // Connection-aware color class
                spike.RemoveFromClassList("beat-up");
                spike.RemoveFromClassList("beat-listen");
                spike.RemoveFromClassList("beat-down");
                if (MCPServer.IsRunning && MCPServer.IsClientConnected)
                    spike.AddToClassList("beat-up");
                else if (MCPServer.IsRunning)
                    spike.AddToClassList("beat-listen");
                else
                    spike.AddToClassList("beat-down");
            }).Every(1800);

            return row;
        }
    }
}
