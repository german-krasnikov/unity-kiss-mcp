// Plain-text persistence struct for domain-reload-safe turn resumption.
// Serialized to Library/MCP_ChatPendingTurn.txt (gitignored, local).
// Format: pipe-delimited lines. Newlines in text are base64-encoded to survive the line format.
// v4: chip lines become "PathB64|KindKeyB64". Pipe-free B64 makes | a safe separator.
//     Back-compat: v3 chip lines have no pipe → KindKey="" → re-detect via registry.
// v5: chip lines become "PathB64|KindKeyB64|OffsetStr". Offsets default to 0 for v4/v3.
using System;

namespace UnityMCP.Editor.Chat
{
    internal struct PendingTurnState
    {
        public string      SessionId;
        public string      PendingText;
        public string[]    ChipPaths;
        public string[]    KindKeys;        // v4: parallel to ChipPaths
        public int[]       ChipTextOffsets; // v5: parallel to ChipPaths; null/empty = pre-v5 → all 0
        public bool        AgentMode;
        public string      AgentName;
        public string      ActivityPhase;
        public int         UndoGroupId;    // -1 = none/legacy
        public long        SavedAtUtc;     // 0  = legacy/no-timestamp
        public BackendKind BackendKind;    // v3 — default Claude

        internal PendingTurnState(string sessionId, string pendingText, string[] chipPaths,
            bool agentMode, string agentName, string activityPhase,
            int undoGroupId = -1, long savedAtUtc = 0,
            BackendKind backendKind = BackendKind.Claude,
            string[] kindKeys = null, int[] chipTextOffsets = null)
        {
            SessionId        = sessionId     ?? "";
            PendingText      = pendingText   ?? "";
            ChipPaths        = chipPaths     ?? Array.Empty<string>();
            KindKeys         = kindKeys      ?? Array.Empty<string>();
            ChipTextOffsets  = chipTextOffsets; // null = v4 back-compat
            AgentMode        = agentMode;
            AgentName        = agentName     ?? "";
            ActivityPhase    = activityPhase ?? "";
            UndoGroupId      = undoGroupId;
            SavedAtUtc       = savedAtUtc;
            BackendKind      = backendKind;
        }

        // ── Staleness guard ───────────────────────────────────────────────────
        internal static bool IsStale(PendingTurnState state, long nowUtc, long thresholdSec = 60)
        {
            return state.ActivityPhase != "Idle"
                && state.SavedAtUtc > 0
                && nowUtc - state.SavedAtUtc > thresholdSec;
        }

        // ── Serialization ─────────────────────────────────────────────────────
        // v1 header: SessionId|PendingTextB64|AgentMode|AgentNameB64|ActivityPhaseB64|ChipCount
        // v2 header: ...same...|UndoGroupId|SavedAtUtc
        // v3 header: ...same...|BackendKind (int)
        //            chip lines: ChipPathB64
        // v4 header: same as v3
        //            chip lines: ChipPathB64|KindKeyB64  (pipe separator, B64 safe)
        // v5 header: same as v4
        //            chip lines: ChipPathB64|KindKeyB64|OffsetStr

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
            {
                var kindKey = (KindKeys != null && i < KindKeys.Length) ? KindKeys[i] : "";
                var offset  = (ChipTextOffsets != null && i < ChipTextOffsets.Length)
                    ? ChipTextOffsets[i].ToString() : "0";
                // v5: PathB64|KindKeyB64|OffsetStr
                lines[1 + i] = ToB64(ChipPaths[i] ?? "") + "|" + ToB64(kindKey ?? "") + "|" + offset;
            }
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

                var undoGroupId = header.Length > 6 ? int.Parse(header[6])  : -1;
                var savedAtUtc  = header.Length > 7 ? long.Parse(header[7]) : 0L;
                var backendKind = header.Length > 8
                    ? (BackendKind)int.Parse(header[8])
                    : BackendKind.Claude;
                // F28: map legacy CodexAppServer (int=2) to Codex (int=1)
                if ((int)backendKind == 2) backendKind = BackendKind.Codex;

                var chips    = new string[chipCount];
                var kindKeys = new string[chipCount];
                var offsets  = new int[chipCount];
                for (var i = 0; i < chipCount; i++)
                {
                    if (1 + i >= lines.Length) { chips[i] = ""; kindKeys[i] = ""; offsets[i] = 0; continue; }
                    var chipLine  = lines[1 + i];
                    var pipeIdx   = chipLine.IndexOf('|');
                    if (pipeIdx >= 0)
                    {
                        chips[i] = FromB64(chipLine.Substring(0, pipeIdx));
                        var rest  = chipLine.Substring(pipeIdx + 1);
                        var pipe2 = rest.IndexOf('|');
                        if (pipe2 >= 0)
                        {
                            // v5: KindKeyB64|OffsetStr
                            kindKeys[i] = FromB64(rest.Substring(0, pipe2));
                            int.TryParse(rest.Substring(pipe2 + 1), out offsets[i]);
                        }
                        else
                        {
                            // v4: KindKeyB64 only
                            kindKeys[i] = FromB64(rest);
                            offsets[i]  = 0;
                        }
                    }
                    else
                    {
                        // v3 back-compat: line is just PathB64
                        chips[i]    = FromB64(chipLine);
                        kindKeys[i] = "";
                        offsets[i]  = 0;
                    }
                }

                return new PendingTurnState(sessionId, pendingText, chips, agentMode, agentName,
                    activityPhase, undoGroupId, savedAtUtc, backendKind, kindKeys, offsets);
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
