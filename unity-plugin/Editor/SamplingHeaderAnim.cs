using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class SamplingHeaderAnim
    {
        // Pre-baked height ratios (0.2–1.0) per bar — organic feel, no Math.Random
        private static readonly float[][] _patterns =
        {
            new[] { 0.3f, 0.6f, 0.9f, 0.5f, 0.2f, 0.7f, 0.4f, 0.8f },
            new[] { 0.7f, 0.4f, 0.2f, 0.8f, 0.6f, 0.3f, 1.0f, 0.5f },
            new[] { 0.5f, 0.9f, 0.6f, 0.3f, 1.0f, 0.4f, 0.7f, 0.2f },
            new[] { 1.0f, 0.3f, 0.7f, 0.5f, 0.2f, 0.9f, 0.4f, 0.6f },
            new[] { 0.4f, 0.8f, 0.3f, 1.0f, 0.6f, 0.2f, 0.9f, 0.5f },
            new[] { 0.6f, 0.2f, 1.0f, 0.4f, 0.8f, 0.5f, 0.3f, 0.7f },
            new[] { 0.8f, 0.5f, 0.4f, 0.7f, 0.3f, 1.0f, 0.6f, 0.2f },
        };

        private const float MaxHeight = 40f;

        public static VisualElement Build(VisualElement scheduleHost)
        {
            var container = new VisualElement();
            container.AddToClassList("freq-root");

            var bars = new VisualElement[7];
            for (int i = 0; i < 7; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("freq-bar");
                container.Add(bar);
                bars[i] = bar;
            }

            int step = 0;
            scheduleHost.schedule.Execute(() =>
            {
                string conn = ArcadePalette.StateClass;
                for (int i = 0; i < 7; i++)
                {
                    bars[i].style.height = _patterns[i][step % _patterns[i].Length] * MaxHeight;
                    bars[i].RemoveFromClassList("conn-up");
                    bars[i].RemoveFromClassList("conn-listen");
                    bars[i].RemoveFromClassList("conn-down");
                    bars[i].AddToClassList(conn);
                }
                step++;
            }).Every(120);

            return container;
        }
    }
}
