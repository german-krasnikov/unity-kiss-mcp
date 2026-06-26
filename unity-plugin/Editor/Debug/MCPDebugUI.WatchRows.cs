using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal partial class MCPDebugUI
    {
        // Per-watch float sample history for sparklines (id → recent samples).
        private readonly Dictionary<string, List<float>> _sparklineData = new();

        private void RefreshWatchRows()
        {
            if (_watchRowsContainer == null) return;
            _watchRowsContainer.Clear();

            foreach (var (id, entry) in WatchRegistry.All)
            {
                var row = BuildWatchRow(id, entry);
                _watchRowsContainer.Add(row);
            }
        }

        private VisualElement BuildWatchRow(string id, WatchEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("watch-row");

            if (entry.Triggered)
                row.AddToClassList("watch-triggered");
            else if (entry.ChangeCount > 0)
                row.AddToClassList("watch-changed");

            // path.component.field label
            var nameLabel = new Label($"{entry.Path}.{entry.Component}.{entry.Field}");
            nameLabel.AddToClassList("watch-label");
            row.Add(nameLabel);

            // current value
            var valueLabel = new Label(entry.LastValue?.ToString() ?? "–");
            valueLabel.AddToClassList("watch-value");
            row.Add(valueLabel);

            // change count delta
            var deltaLabel = new Label($"Δ{entry.ChangeCount}");
            deltaLabel.AddToClassList("watch-delta");
            row.Add(deltaLabel);

            // sparkline (only for numeric values)
            SampleSparkline(id, entry.LastValue);
            if (_sparklineData.TryGetValue(id, out var samples) && samples.Count > 0)
            {
                var spark = new Label(SparklineHelper.Generate(samples));
                spark.AddToClassList("sparkline");
                row.Add(spark);
            }

            // condition badge
            if (!string.IsNullOrEmpty(entry.Condition))
            {
                var badge = new Label(entry.Condition);
                badge.AddToClassList("watch-condition");
                row.Add(badge);
            }

            // remove button — capture id by value
            var capturedId = id;
            var removeBtn = new Button(() => {
                WatchRegistry.Remove(capturedId);
                _sparklineData.Remove(capturedId);
            }) { text = "×" };
            removeBtn.AddToClassList("watch-remove");
            row.Add(removeBtn);

            return row;
        }

        private void SampleSparkline(string id, object value)
        {
            if (!float.TryParse(value?.ToString(), out float f)) return;
            if (!_sparklineData.ContainsKey(id))
                _sparklineData[id] = new List<float>();
            var samples = _sparklineData[id];
            samples.Add(f);
            if (samples.Count > 32) samples.RemoveAt(0);
        }
    }
}
