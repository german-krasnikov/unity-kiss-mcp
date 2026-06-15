// ChipData: identity + metadata for a single inline chip.
// KindKey is the string identity (H6). InlineChipTracker removed in Wave 0 (replaced by InlineChipModel).
using System;

namespace UnityMCP.Editor.Chat
{
    /// <summary>Identity + metadata for a single inline chip. KindKey is the string identity (H6).</summary>
    public readonly struct ChipData
    {
        public readonly string KindKey;
        public readonly string Path;
        public readonly string DisplayName;
        public readonly int    InstanceID;

        public ChipData(string kindKey, string path, string displayName, int instanceID)
        {
            KindKey     = kindKey     ?? ChipKindKeys.Asset;
            Path        = path        ?? "";
            DisplayName = displayName ?? "";
            InstanceID  = instanceID;
        }
    }
}
