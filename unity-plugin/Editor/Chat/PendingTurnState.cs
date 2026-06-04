// Plain-text persistence struct for domain-reload-safe turn resumption.
// Serialized to Library/MCP_ChatPendingTurn.txt (gitignored, local).
// Format: pipe-delimited lines. Newlines in text are base64-encoded to survive the line format.
using System;

namespace UnityMCP.Editor.Chat
{
    internal struct PendingTurnState
    {
        public string      SessionId;
        public string      PendingText;
        public string[]    ChipPaths;
        public bool        AgentMode;
        public string      AgentName;
        public string      ActivityPhase;
        public int         UndoGroupId;    // -1 = none/legacy
        public long        SavedAtUtc;     // 0  = legacy/no-timestamp
        public BackendKind BackendKind;    // v3 — default Claude

        internal PendingTurnState(string sessionId, string pendingText, string[] chipPaths,
            bool agentMode, string agentName, string activityPhase,
            int undoGroupId = -1, long savedAtUtc = 0,
            BackendKind backendKind = BackendKind.Claude)
        {
            SessionId     = sessionId     ?? "";
            PendingText   = pendingText   ?? "";
            ChipPaths     = chipPaths     ?? Array.Empty<string>();
            AgentMode     = agentMode;
            AgentName     = agentName     ?? "";
            ActivityPhase = activityPhase ?? "";
            UndoGroupId   = undoGroupId;
            SavedAtUtc    = savedAtUtc;
            BackendKind   = backendKind;
        }

        // ── Serialization ─────────────────────────────────────────────────────
        // v1 header: SessionId|PendingTextB64|AgentMode|AgentNameB64|ActivityPhaseB64|ChipCount
        // v2 header: ...same...|UndoGroupId|SavedAtUtc
        // v3 header: ...same...|BackendKind (int)
        //            ChipPath0B64 ...
        // ALL string fields are B64-encoded so pipes/newlines never corrupt the header.

        internal string Serialize()
        {
            var textB64   = ToB64(PendingText);
            var chipCount = ChipPaths.Length;
            var lines     = new string[1 + chipCount];
            lines[0] = string.Join("|",
                SessionId,
                textB64,
                AgentMode ? "1" : "0",
                ToB64(AgentName ?? ""),
                ToB64(ActivityPhase ?? ""),
                chipCount.ToString(),
                UndoGroupId.ToString(),
                SavedAtUtc.ToString(),
                ((int)BackendKind).ToString());
            for (var i = 0; i < chipCount; i++)
                lines[1 + i] = ToB64(ChipPaths[i] ?? "");
            return string.Join("\n", lines);
        }

        internal static PendingTurnState? Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                var lines  = raw.Split('\n');
                var header = lines[0].Split('|');
                if (header.Length < 6) return null;

                var sessionId     = header[0];
                var pendingText   = FromB64(header[1]);
                var agentMode     = header[2] == "1";
                var agentName     = FromB64(header[3]);
                var activityPhase = FromB64(header[4]);
                var chipCount     = int.Parse(header[5]);

                // v2/v3 fields — length-based gate preserves back-compat.
                var undoGroupId = header.Length > 6 ? int.Parse(header[6])  : -1;
                var savedAtUtc  = header.Length > 7 ? long.Parse(header[7]) : 0L;
                var backendKind = header.Length > 8
                    ? (BackendKind)int.Parse(header[8])
                    : BackendKind.Claude;

                var chips = new string[chipCount];
                for (var i = 0; i < chipCount; i++)
                    chips[i] = (1 + i < lines.Length) ? FromB64(lines[1 + i]) : "";

                return new PendingTurnState(sessionId, pendingText, chips, agentMode, agentName,
                    activityPhase, undoGroupId, savedAtUtc, backendKind);
            }
            catch
            {
                return null;
            }
        }

        private static string ToB64(string s)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? ""));

        private static string FromB64(string s)
            => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s));
    }
}
