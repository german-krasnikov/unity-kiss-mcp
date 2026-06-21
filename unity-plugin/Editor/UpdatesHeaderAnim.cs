using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class UpdatesHeaderAnim
    {
        private const int BarCount = 5;
        private const float MaxHeight = 36f;
        private const float MinHeight = 4f;

        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("anim-updates");

            var bars = new VisualElement[BarCount];
            for (int i = 0; i < BarCount; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("upload-bar");
                container.Add(bar);
                bars[i] = bar;
            }

            int step = 0;
            scheduleHost.schedule.Execute(() =>
            {
                string conn = ArcadePalette.StateClass;
                int active = step % BarCount;

                for (int i = 0; i < BarCount; i++)
                {
                    // Bars before active are full; active bar is mid-fill; rest are empty
                    float ratio = i < active ? 1.0f : i == active ? 0.5f : 0.1f;
                    bars[i].style.height = MinHeight + ratio * (MaxHeight - MinHeight);

                    bars[i].RemoveFromClassList("conn-up");
                    bars[i].RemoveFromClassList("conn-listen");
                    bars[i].RemoveFromClassList("conn-down");
                    bars[i].AddToClassList(conn);
                    bars[i].EnableInClassList("upload-bar--active", i == active);
                }
                step++;
            }).Every(200);

            return container;
        }
    }
}
