using System.Collections.Generic;

namespace UnityMCP.Editor.Chat
{
    internal readonly struct MentionCandidate
    {
        public readonly ChipData Chip;
        public readonly long     Score;
        public readonly string   IconName;

        public MentionCandidate(ChipData chip, long score, string iconName)
        {
            Chip     = chip;
            Score    = score;
            IconName = iconName;
        }
    }

    internal interface IMentionSource
    {
        void Search(string query, int maxResults, List<MentionCandidate> results);
        void RefreshIfDirty();
    }
}
