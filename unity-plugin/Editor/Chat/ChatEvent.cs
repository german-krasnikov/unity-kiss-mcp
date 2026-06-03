namespace UnityMCP.Editor.Chat
{
    public enum ChatEventKind
    {
        /// <summary>Streamed text token from assistant.</summary>
        TextDelta,
        /// <summary>Tool invocation started (name known, args may still be partial).</summary>
        ToolStart,
        /// <summary>Tool invocation result (success or failure).</summary>
        ToolResult,
        /// <summary>Turn fully complete — carries cost + usage.</summary>
        TurnDone,
        /// <summary>Error from parser, process, or API.</summary>
        Error,
    }

    /// <summary>Immutable event emitted by ChatStreamParser.</summary>
    public readonly struct ChatEvent
    {
        public ChatEventKind Kind       { get; }
        public string        Text       { get; }  // TextDelta: token | ToolStart: tool name | Error: message
        public string        ArgsJson   { get; }  // ToolStart: partial accumulated args
        public bool          IsOk       { get; }  // ToolResult/TurnDone: success flag
        public string        SessionId  { get; }  // TurnDone/Init
        public float         CostUsd    { get; }  // TurnDone
        public int           InputTokens  { get; }
        public int           OutputTokens { get; }

        private ChatEvent(ChatEventKind kind, string text = null, string argsJson = null,
            bool isOk = true, string sessionId = null,
            float costUsd = 0f, int inputTokens = 0, int outputTokens = 0)
        {
            Kind         = kind;
            Text         = text;
            ArgsJson     = argsJson;
            IsOk         = isOk;
            SessionId    = sessionId;
            CostUsd      = costUsd;
            InputTokens  = inputTokens;
            OutputTokens = outputTokens;
        }

        public static ChatEvent TextDelta(string token) =>
            new ChatEvent(ChatEventKind.TextDelta, text: token);

        public static ChatEvent ToolStart(string name, string argsJson = "") =>
            new ChatEvent(ChatEventKind.ToolStart, text: name, argsJson: argsJson ?? "");

        public static ChatEvent ToolResult(bool ok) =>
            new ChatEvent(ChatEventKind.ToolResult, isOk: ok);

        public static ChatEvent TurnDone(string sessionId, float costUsd, int inputTokens, int outputTokens) =>
            new ChatEvent(ChatEventKind.TurnDone, sessionId: sessionId, isOk: true,
                costUsd: costUsd, inputTokens: inputTokens, outputTokens: outputTokens);

        public static ChatEvent Error(string message) =>
            new ChatEvent(ChatEventKind.Error, text: message, isOk: false);
    }
}
