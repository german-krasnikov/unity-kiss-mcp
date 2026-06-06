using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class ChatHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("wave-root");

            var lineL = new VisualElement(); lineL.AddToClassList("wave-line");
            var hub   = new VisualElement(); hub.AddToClassList("wave-hub");
            var lineR = new VisualElement(); lineR.AddToClassList("wave-line");

            var arcs = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                arcs[i] = new VisualElement();
                arcs[i].AddToClassList("wave-arc");
                arcs[i].AddToClassList("wave-arc-" + (i + 1));
                hub.Add(arcs[i]);
            }

            var dot = new VisualElement(); dot.AddToClassList("wave-dot");
            hub.Add(dot);

            root.Add(lineL); root.Add(hub); root.Add(lineR);

            int phase = 0;
            scheduleHost.schedule.Execute(() =>
            {
                phase = (phase + 1) % 12;
                // Phases 0-2: arc1 lit, 3-5: arc2 lit, 6-8: arc3 lit, 9-11: pause
                for (int i = 0; i < 3; i++)
                    arcs[i].EnableInClassList("wave-arc--lit", phase >= i * 3 && phase < (i + 1) * 3);

                string state = MCPServer.IsRunning && MCPServer.IsClientConnected ? "up"
                    : MCPServer.IsRunning ? "listen" : "down";
                foreach (var arc in arcs)
                {
                    arc.RemoveFromClassList("wave--up");
                    arc.RemoveFromClassList("wave--listen");
                    arc.RemoveFromClassList("wave--down");
                    arc.AddToClassList("wave--" + state);
                }
                dot.RemoveFromClassList("wave-dot--up");
                dot.RemoveFromClassList("wave-dot--listen");
                dot.RemoveFromClassList("wave-dot--down");
                dot.AddToClassList("wave-dot--" + state);
            }).Every(150);

            return root;
        }
    }
}
