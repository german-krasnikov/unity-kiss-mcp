using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class StatusAmbientAnim
    {
        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("status-ambient");
            container.style.position = Position.Absolute;
            container.style.top  = 0; container.style.left   = 0;
            container.style.right = 0; container.style.bottom = 0;

            var scanline = new VisualElement();
            scanline.AddToClassList("status-scanline");
            container.Add(scanline);

            var grid = new VisualElement();
            grid.AddToClassList("status-grid");
            for (int i = 0; i < 16; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("status-grid-dot");
                grid.Add(dot);
            }
            container.Add(grid);

            var sonar = new VisualElement();
            sonar.AddToClassList("status-sonar");
            container.Add(sonar);

            int tick = 0;
            scheduleHost.schedule.Execute(() =>
            {
                bool phase = (tick % 2) == 0;
                scanline.EnableInClassList("status-scanline--sweep", phase);
                sonar.EnableInClassList("status-sonar--ping", (tick % 4) == 0);

                string conn = ArcadePalette.StateClass;
                grid.RemoveFromClassList("conn-up");
                grid.RemoveFromClassList("conn-listen");
                grid.RemoveFromClassList("conn-down");
                grid.AddToClassList(conn);

                tick++;
            }).Every(1500);

            return container;
        }
    }
}
