using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor
{
    internal sealed partial class PerfWindow
    {
        private PerfGraphElement _fpsGraph;
        private VisualElement _cpuFill, _gpuFill;
        private Label _cpuLabel, _gpuLabel;
        private Label _frameCur, _frameAvg, _frameP99, _frameMax;
        private RecordIndicator _recordDot;
        private Button _recordBtn;
        private readonly FrameRingBuffer _perfBuf = new(120);

        private VisualElement BuildPerformanceTab()
        {
            var root = new VisualElement();
            root.AddToClassList("perf-section");

            // FPS line graph
            root.Add(MakeSectionLabel("FPS"));
            _fpsGraph = new PerfGraphElement(120) { MinValue = 0f, MaxValue = 120f };
            _fpsGraph.style.width = Length.Percent(100);
            root.Add(_fpsGraph);

            // CPU + GPU bars side by side
            var barsRow = new VisualElement();
            barsRow.AddToClassList("perf-bars-row");
            barsRow.Add(MakeBarCard("CPU", out _cpuFill, out _cpuLabel));
            barsRow.Add(MakeBarCard("GPU", out _gpuFill, out _gpuLabel));
            root.Add(barsRow);

            // cur / avg / p99 / max
            var statsRow = new VisualElement();
            statsRow.AddToClassList("perf-stats-row");
            statsRow.Add(MakeStatCell("cur", out _frameCur));
            statsRow.Add(MakeStatCell("avg", out _frameAvg));
            statsRow.Add(MakeStatCell("p99", out _frameP99));
            statsRow.Add(MakeStatCell("max", out _frameMax));
            root.Add(statsRow);

            // Record row
            var recRow = new VisualElement();
            recRow.AddToClassList("perf-record-row");
            _recordDot = new RecordIndicator();
            recRow.Add(_recordDot);
            _recordBtn = new Button(ToggleRecord) { text = "Record" };
            _recordBtn.AddToClassList("perf-record-btn");
            recRow.Add(_recordBtn);
            root.Add(recRow);

            // 5Hz refresh — tight enough for visuals, easy on the main thread
            root.schedule.Execute(TickPerformance).Every(200);
            return root;
        }

        private void TickPerformance()
        {
            var f = ProfilerBridge.CollectFrame();
            float fps = f.DeltaTime > 0f ? 1f / f.DeltaTime : 0f;
            _fpsGraph.PushValue(fps);
            _perfBuf.Add(f);

            UpdateBarFill(_cpuFill, _cpuLabel, f.CpuMs, 33.3f, PerfThresholds.FrameTimeBand(f.CpuMs));
            if (f.GpuMs >= 0f)
                UpdateBarFill(_gpuFill, _gpuLabel, f.GpuMs, 33.3f, PerfThresholds.FrameTimeBand(f.GpuMs));
            else
                _gpuLabel.text = "N/A";

            _frameCur.text = $"{f.CpuMs:F1}ms";
            if (_perfBuf.Count >= 10)
            {
                var s = ProfileAnalyzer.Compute(_perfBuf.ToArray());
                _frameAvg.text = $"{s.CpuAvg:F1}ms";
                _frameP99.text = $"{s.CpuP99:F1}ms";
                _frameMax.text = $"{s.CpuMax:F1}ms";
            }

            bool isRec = ProfileRecorder.Dispatch("status", "{}").StartsWith("recording");
            _recordDot.SetRecording(isRec);
            _recordBtn.text = isRec ? "Stop" : "Record";
        }

        private static void ToggleRecord()
        {
            bool isRec = ProfileRecorder.Dispatch("status", "{}").StartsWith("recording");
            if (isRec)
                ProfileRecorder.Dispatch("stop", "{}");
            else
                ProfileRecorder.Dispatch("start", "{\"mode\":\"manual\"}");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        internal static Label MakeSectionLabel(string text)
        {
            var l = new Label(text);
            l.AddToClassList("perf-section-label");
            return l;
        }

        private static VisualElement MakeBarCard(string title, out VisualElement fill, out Label label)
        {
            var card = new VisualElement();
            card.AddToClassList("perf-bar-card");
            var hdr = new Label(title);
            hdr.AddToClassList("perf-metric-label");
            card.Add(hdr);
            var track = new VisualElement();
            track.AddToClassList("perf-bar-track");
            fill = new VisualElement();
            fill.AddToClassList("perf-bar-fill");
            fill.style.width = Length.Percent(0);
            track.Add(fill);
            card.Add(track);
            label = new Label("--");
            label.AddToClassList("perf-bar-label");
            card.Add(label);
            return card;
        }

        private static VisualElement MakeStatCell(string key, out Label value)
        {
            var cell = new VisualElement();
            cell.AddToClassList("perf-stat-cell");
            var keyLbl = new Label(key);
            keyLbl.AddToClassList("perf-stat-key");
            cell.Add(keyLbl);
            value = new Label("--");
            value.AddToClassList("perf-stat-val");
            cell.Add(value);
            return cell;
        }

        private static void UpdateBarFill(VisualElement fill, Label label, float val, float maxVal, string band)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(val / maxVal) * 100f);
            label.text = $"{val:F1}ms";
            fill.EnableInClassList("perf-band--good", band == "good");
            fill.EnableInClassList("perf-band--warn", band == "warn");
            fill.EnableInClassList("perf-band--crit", band == "crit");
        }
    }
}
