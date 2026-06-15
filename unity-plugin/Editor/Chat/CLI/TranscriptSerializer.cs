// F21: Serialization helpers for transcript reload-survival via SessionState.
using System;
using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal struct TranscriptEntry
    {
        internal enum Kind { User = 0, Assistant = 1 }
        internal Kind   EntryKind;
        internal string Text;
        internal string ChipsData;  // \x1F-delimited KindKey\x1FPath\x1FDisplayName triplets
        internal string LlmPayload; // full-path payload sent to LLM (nullable; null = same as Text)
    }

    internal static class TranscriptSerializer
    {
        // Format per line: Kind|Base64(Text)|Base64(ChipsData)|Base64(LlmPayload)
        // Old format (3 columns) is backward-compat — missing 4th column → LlmPayload=null.
        internal static string Serialize(List<TranscriptEntry> entries)
        {
            if (entries == null || entries.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                var textB64  = ToB64(e.Text ?? "");
                var chipsB64 = string.IsNullOrEmpty(e.ChipsData) ? "" : ToB64(e.ChipsData);
                var llmB64   = string.IsNullOrEmpty(e.LlmPayload) ? "" : ToB64(e.LlmPayload);
                sb.Append((int)e.EntryKind).Append('|').Append(textB64).Append('|')
                  .Append(chipsB64).Append('|').Append(llmB64).Append('\n');
            }
            return sb.ToString();
        }

        internal static List<TranscriptEntry> Deserialize(string data)
        {
            var result = new List<TranscriptEntry>();
            if (string.IsNullOrEmpty(data)) return result;
            foreach (var line in data.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('|');
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out var kindInt)) continue;
                if (kindInt < 0 || kindInt > 1) continue;
                string text;
                try { text = FromB64(parts[1]); } catch { continue; }
                string chipsData = parts.Length > 2 && !string.IsNullOrEmpty(parts[2])
                    ? TryFromB64(parts[2]) : null;
                string llmPayload = parts.Length > 3 && !string.IsNullOrEmpty(parts[3])
                    ? TryFromB64(parts[3]) : null;
                result.Add(new TranscriptEntry
                {
                    EntryKind  = (TranscriptEntry.Kind)kindInt,
                    Text       = text,
                    ChipsData  = chipsData,
                    LlmPayload = llmPayload,
                });
            }
            return result;
        }

        private const char Sep = '\x1F'; // unit separator — safe in DisplayName/Path

        internal static string SerializeChips(IReadOnlyList<ChipData> chips)
        {
            if (chips == null || chips.Count == 0) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < chips.Count; i++)
            {
                if (i > 0) sb.Append(Sep);
                sb.Append(chips[i].KindKey).Append(Sep).Append(chips[i].Path).Append(Sep).Append(chips[i].DisplayName);
            }
            return sb.ToString();
        }

        internal static List<ChipData> DeserializeChips(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            var parts = data.Split(Sep);
            var list  = new List<ChipData>();
            for (int i = 0; i + 2 < parts.Length; i += 3)
                list.Add(new ChipData(parts[i], parts[i + 1], parts[i + 2], 0));
            return list.Count > 0 ? list : null;
        }

        private static string ToB64(string s)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

        private static string FromB64(string s)
            => Encoding.UTF8.GetString(Convert.FromBase64String(s));

        private static string TryFromB64(string s)
        {
            try { return FromB64(s); } catch { return null; }
        }
    }
}
