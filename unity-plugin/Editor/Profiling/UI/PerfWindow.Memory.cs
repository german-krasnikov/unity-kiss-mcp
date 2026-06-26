using System;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor
{
    internal sealed partial class PerfWindow
    {
        private VisualElement _monoFill;
        private Label _monoLabel;
        private Label _gcLabel;
        private Label _texLabel;
        private Label _totalLabel;
        private int _lastGcCount;

        private VisualElement BuildMemoryTab()
        {
            var root = new VisualElement();
            root.AddToClassList("perf-section");

            // Mono heap fill bar
            root.Add(MakeSectionLabel("Mono Heap"));
            var monoTrack = new VisualElement();
            monoTrack.AddToClassList("perf-bar-track");
            _monoFill = new VisualElement();
            _monoFill.AddToClassList("perf-bar-fill");
            _monoFill.style.width = Length.Percent(0);
            monoTrack.Add(_monoFill);
            root.Add(monoTrack);
            _monoLabel = new Label("--");
            _monoLabel.AddToClassList("perf-mem-label");
            root.Add(_monoLabel);

            // GC Gen0 counter — flashes red on new collection
            root.Add(MakeSectionLabel("GC Gen0 Collections"));
            _gcLabel = new Label("0");
            _gcLabel.AddToClassList("perf-mem-label");
            root.Add(_gcLabel);

            // GPU texture memory
            root.Add(MakeSectionLabel("GPU Texture Memory"));
            _texLabel = new Label("--");
            _texLabel.AddToClassList("perf-mem-label");
            root.Add(_texLabel);

            // Total managed allocations
            root.Add(MakeSectionLabel("Total Managed"));
            _totalLabel = new Label("--");
            _totalLabel.AddToClassList("perf-mem-label");
            root.Add(_totalLabel);

            _lastGcCount = GC.CollectionCount(0);
            // 2Hz refresh — memory queries are heavier than frame counters
            root.schedule.Execute(TickMemory).Every(500);
            return root;
        }

        private void TickMemory()
        {
            var f = ProfilerBridge.CollectFrame();

            long usedBytes = f.MonoUsedBytes;
            long heapBytes = Profiler.GetMonoHeapSizeLong();
            float ratio = heapBytes > 0 ? (float)usedBytes / heapBytes : 0f;

            _monoFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
            string memBand = ratio > 0.9f ? "crit" : ratio > 0.7f ? "warn" : "good";
            _monoFill.EnableInClassList("perf-band--good", memBand == "good");
            _monoFill.EnableInClassList("perf-band--warn", memBand == "warn");
            _monoFill.EnableInClassList("perf-band--crit", memBand == "crit");
            _monoLabel.text = $"{usedBytes / 1_048_576L} / {heapBytes / 1_048_576L} MB";

            // Flash on new GC collections
            int gcNow = GC.CollectionCount(0);
            if (gcNow != _lastGcCount)
            {
                _gcLabel.text = gcNow.ToString();
                ArcadeAnim.FlashClass(_gcLabel, "perf-gc-flash", 400);
                _lastGcCount = gcNow;
            }

            long texBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            _texLabel.text = $"{texBytes / 1_048_576L} MB";

            long totalBytes = Profiler.GetTotalAllocatedMemoryLong();
            _totalLabel.text = $"{totalBytes / 1_048_576L} MB";
        }
    }
}
