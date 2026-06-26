using System.Collections.Generic;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal sealed partial class PerfWindow
    {
        private VisualElement _renderGrid;
        private Label _pipelineBadge;
        private Label _compareResult;

        private VisualElement BuildRenderingTab()
        {
            var root = new VisualElement();
            root.AddToClassList("perf-section");

            // Pipeline badge + Refresh button
            var topRow = new VisualElement();
            topRow.AddToClassList("perf-render-top");
            _pipelineBadge = new Label("--");
            _pipelineBadge.AddToClassList("perf-pipeline-badge");
            topRow.Add(_pipelineBadge);
            var refreshBtn = new Button(RefreshRendering) { text = "Refresh" };
            refreshBtn.AddToClassList("perf-btn");
            topRow.Add(refreshBtn);
            root.Add(topRow);

            // 2-column card grid
            _renderGrid = new VisualElement();
            _renderGrid.AddToClassList("perf-stats-grid");
            root.Add(_renderGrid);

            // Baseline / Compare controls
            var ctrlRow = new VisualElement();
            ctrlRow.AddToClassList("perf-ctrl-row");
            var baselineBtn = new Button(SaveRenderBaseline) { text = "Save Baseline" };
            baselineBtn.AddToClassList("perf-btn");
            ctrlRow.Add(baselineBtn);
            var compareBtn = new Button(CompareRendering) { text = "Compare" };
            compareBtn.AddToClassList("perf-btn");
            ctrlRow.Add(compareBtn);
            root.Add(ctrlRow);

            _compareResult = new Label("");
            _compareResult.AddToClassList("perf-compare-result");
            root.Add(_compareResult);

            RefreshRendering();
            return root;
        }

        private void RefreshRendering()
        {
            string pipeline = RenderPipelineInspector.DetectPipeline().ToUpperInvariant();
            _pipelineBadge.text = pipeline;

            string raw = RenderAnalyzer.Execute("{\"action\":\"stats\"}");
            _renderGrid.Clear();

            var fields = new Dictionary<string, string>
            {
                ["draw"]    = "Draw Calls",
                ["batches"] = "Batches",
                ["setpass"] = "Set Pass",
                ["shadows"] = "Shadow Cast",
                ["tris"]    = "Triangles",
                ["verts"]   = "Vertices",
            };

            foreach (var kv in fields)
                _renderGrid.Add(MakeStatCard(kv.Value, ExtractRenderValue(raw, kv.Key)));
        }

        private void SaveRenderBaseline()
        {
            // Calling stats again writes the new baseline internally
            RenderAnalyzer.Execute("{\"action\":\"stats\"}");
        }

        private void CompareRendering()
        {
            string result = RenderAnalyzer.Execute("{\"action\":\"compare\"}");
            _compareResult.text = result.Length > 300 ? result[..300] : result;
        }

        private static VisualElement MakeStatCard(string title, string value)
        {
            var card = new VisualElement();
            card.AddToClassList("perf-stat-card");
            var titleLbl = new Label(title);
            titleLbl.AddToClassList("perf-card-title");
            var valLbl = new Label(value);
            valLbl.AddToClassList("perf-card-value");
            card.Add(titleLbl);
            card.Add(valLbl);
            return card;
        }

        private static string ExtractRenderValue(string raw, string key)
        {
            int idx = raw.IndexOf(key + "=", System.StringComparison.Ordinal);
            if (idx < 0) return "--";
            int start = idx + key.Length + 1;
            int end = raw.IndexOf(' ', start);
            return end < 0 ? raw[start..] : raw[start..end];
        }
    }
}
