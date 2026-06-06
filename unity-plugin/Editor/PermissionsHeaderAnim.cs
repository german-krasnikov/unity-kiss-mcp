using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class PermissionsHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("shield-root");

            var lineL = new VisualElement(); lineL.AddToClassList("shield-line");
            var hub   = new VisualElement(); hub.AddToClassList("shield-hub");
            var lineR = new VisualElement(); lineR.AddToClassList("shield-line");

            var body    = new VisualElement(); body.AddToClassList("shield-body");
            var shackle = new VisualElement(); shackle.AddToClassList("lock-shackle");
            var bar     = new VisualElement(); bar.AddToClassList("lock-bar");
            var dot     = new VisualElement(); dot.AddToClassList("lock-dot");

            hub.Add(body); hub.Add(shackle); hub.Add(bar); hub.Add(dot);
            root.Add(lineL); root.Add(hub); root.Add(lineR);

            int beat = 0;
            scheduleHost.schedule.Execute(() =>
            {
                beat++;
                if (beat % 6 == 0) body.ToggleInClassList("shield-body--pulse");
                if (beat % 12 == 0) shackle.ToggleInClassList("lock-shackle--up");

                string state = MCPServer.IsRunning && MCPServer.IsClientConnected ? "up"
                    : MCPServer.IsRunning ? "listen" : "down";

                foreach (var el in new[] { body, shackle })
                {
                    el.RemoveFromClassList("shield--up");
                    el.RemoveFromClassList("shield--listen");
                    el.RemoveFromClassList("shield--down");
                    el.AddToClassList("shield--" + state);
                }
                bar.RemoveFromClassList("lock-bar--up");
                bar.RemoveFromClassList("lock-bar--listen");
                bar.RemoveFromClassList("lock-bar--down");
                bar.AddToClassList("lock-bar--" + state);
            }).Every(150);

            return root;
        }
    }
}
