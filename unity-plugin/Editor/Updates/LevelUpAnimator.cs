using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class LevelUpAnimator
    {
        const int TotalTicks  = 37;
        const int SparkCount  = 5;
        static readonly int[]         SparkTicks = { 5, 12, 20, 28, 35 };
        static readonly System.Random Rng        = new System.Random();

        internal static VisualElement Build(
            VisualElement scheduleHost,
            string fromVersion,
            string toVersion,
            Action onComplete)
        {
            var root = new VisualElement();
            root.AddToClassList("lvlup-anim-root");

            var versionLabel = new Label($"v{fromVersion}  →  v{toVersion}");
            versionLabel.AddToClassList("lvlup-version");
            root.Add(versionLabel);

            var track = new VisualElement();
            track.AddToClassList("lvlup-xp-track");
            var fill = new VisualElement();
            fill.AddToClassList("lvlup-xp-fill");
            fill.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
            track.Add(fill);
            root.Add(track);

            var sparkContainer = new VisualElement();
            sparkContainer.AddToClassList("lvlup-spark-container");
            for (int i = 0; i < SparkCount; i++)
            {
                var spark = new VisualElement();
                spark.AddToClassList("lvlup-spark");
                sparkContainer.Add(spark);
            }
            root.Add(sparkContainer);

            int tick = 0;
            int sparkIdx = 0;
            IVisualElementScheduledItem handle = null;
            handle = scheduleHost.schedule.Execute(() =>
            {
                tick++;
                float progress = Math.Min(tick / (float)TotalTicks, 1f);
                fill.style.width = new StyleLength(new Length(progress * 100f, LengthUnit.Percent));

                foreach (int st in SparkTicks)
                {
                    if (tick == st) RandomizeSpark(sparkContainer, ref sparkIdx);
                }

                if (tick == 15)
                    versionLabel.AddToClassList("lvlup-version-flash");

                if (tick >= TotalTicks)
                {
                    fill.AddToClassList("lvlup-xp-fill--done");
                    handle?.Pause();
                    onComplete?.Invoke();
                }
            }).Every(40);

#if UNITY_INCLUDE_TESTS
            _lastOnComplete = onComplete;
#endif
            return root;
        }

#if UNITY_INCLUDE_TESTS
        static Action _lastOnComplete;

        /// <summary>Test-only: fire onComplete exactly as the scheduler would at TotalTicks.</summary>
        internal static void SimulateCompletion()
        {
            var cb = _lastOnComplete;
            _lastOnComplete = null;
            cb?.Invoke();
        }
#endif

        static void RandomizeSpark(VisualElement container, ref int idx)
        {
            var spark = container[idx % SparkCount];
            idx++;
            spark.RemoveFromClassList("lvlup-spark--a");
            spark.RemoveFromClassList("lvlup-spark--b");
            spark.style.left = new StyleLength(new Length(Rng.Next(5, 90), LengthUnit.Percent));
            spark.style.top  = new StyleLength(new Length(Rng.Next(10, 80), LengthUnit.Percent));
            spark.AddToClassList(idx % 2 == 0 ? "lvlup-spark--a" : "lvlup-spark--b");
        }
    }
}
