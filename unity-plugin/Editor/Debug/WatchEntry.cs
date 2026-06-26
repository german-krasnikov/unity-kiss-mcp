using System;

namespace UnityMCP.Editor
{
    [Serializable]
    internal class WatchEntry
    {
        public string Id;
        public string Path;
        public string Component;
        public string Field;
        public string Condition;
        public string Action;
        public float IntervalMs;

        [NonSerialized] public object LastValue;
        [NonSerialized] public bool Triggered;
        [NonSerialized] public float LastSampleTime;
        [NonSerialized] public int ChangeCount;
        [NonSerialized] public int ErrorCount;
    }
}
