using UnityEngine.UIElements;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Pulsing red dot shown when recording is active.
    /// Animation is pure USS @keyframes — zero C# per-frame code.
    /// </summary>
    internal sealed class RecordIndicator : VisualElement
    {
        internal RecordIndicator() => AddToClassList("record-dot");

        internal void SetRecording(bool active) =>
            EnableInClassList("record-dot--active", active);
    }
}
