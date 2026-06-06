using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class ToolsHeaderAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("anim-tools");

            var knobs = new VisualElement[5];
            for (int i = 0; i < 5; i++)
            {
                var track = new VisualElement();
                track.AddToClassList("toggle-track");
                var knob = new VisualElement();
                knob.AddToClassList("toggle-knob");
                track.Add(knob);
                container.Add(track);
                knobs[i] = knob;
            }

            int step = 0;
            scheduleHost.schedule.Execute(() =>
            {
                string conn = MCPServer.IsRunning && MCPServer.IsClientConnected ? "conn-up"
                    : MCPServer.IsRunning ? "conn-listen" : "conn-down";

                int active = step % 5;
                for (int i = 0; i < 5; i++)
                {
                    knobs[i].EnableInClassList("on", i == active);
                    knobs[i].RemoveFromClassList("conn-up");
                    knobs[i].RemoveFromClassList("conn-listen");
                    knobs[i].RemoveFromClassList("conn-down");
                    if (i == active) knobs[i].AddToClassList(conn);
                }
                step++;
            }).Every(400);

            return container;
        }
    }
}
