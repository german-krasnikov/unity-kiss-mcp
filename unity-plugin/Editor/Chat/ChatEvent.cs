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
        /// <summary>content_block_stop for a tool_use block — args assembly complete.</summary>
        ToolArgsComplete,
        /// <summary>Turn fully complete — carries cost + usage.</summary>
        TurnDone,
        /// <summary>Session established (system/init). Non-terminal: activity animation must keep running.</summary>
        SessionInit,
        /// <summary>Error from parser, process, or API.</summary>
        Error,
    }

    /// <summary>Immutable event emitted by ChatStreamParser.</summary>
    public readonly struct ChatEvent
    {
        public ChatEventKind Kind         { get; }
        public string        Text         { get; }  // TextDelta: token | ToolStart: name | ToolResult: result text | Error: message
        public string        ArgsJson     { get; }  // ToolStart: partial args fragment
        public bool          IsOk         { get; }  // ToolResult/TurnDone: success flag
        public string        SessionId    { get; }  // TurnDone/Init
        public string        ToolId       { get; }  // tool_use_id (ToolStart, ToolResult)
        public float         CostUsd      { get; }  // TurnDone
        public int           InputTokens  { get; }
        public int           OutputTokens { get; }

        private ChatEvent(ChatEventKind kind, string text = null, string argsJson = null,
            bool isOk = true, string sessionId = null, string toolId = null,
            float costUsd = 0f, int inputTokens = 0, int outputTokens = 0)
        {
            Kind         = kind;
            Text         = text;
            ArgsJson     = argsJson;
            IsOk         = isOk;
            SessionId    = sessionId;
            ToolId       = toolId;
            CostUsd      = costUsd;
            InputTokens  = inputTokens;
            OutputTokens = outputTokens;
        }

        public static ChatEvent TextDelta(string token) =>
            new ChatEvent(ChatEventKind.TextDelta, text: token);

        public static ChatEvent ToolStart(string name, string argsJson = "", string toolId = null) =>
            new ChatEvent(ChatEventKind.ToolStart, text: name, argsJson: argsJson ?? "", toolId: toolId);

        /// <param name="toolId">tool_use_id from the stream</param>
        /// <param name="resultText">tool output content</param>
        /// <param name="ok">false if is_error was true</param>
        public static ChatEvent ToolResult(string toolId, string resultText, bool ok) =>
            new ChatEvent(ChatEventKind.ToolResult, text: resultText, toolId: toolId, isOk: ok);

        public static ChatEvent ToolArgsComplete() =>
            new ChatEvent(ChatEventKind.ToolArgsComplete);

        public static ChatEvent TurnDone(string sessionId, float costUsd, int inputTokens, int outputTokens) =>
            new ChatEvent(ChatEventKind.TurnDone, sessionId: sessionId, isOk: true,
                costUsd: costUsd, inputTokens: inputTokens, outputTokens: outputTokens);

        public static ChatEvent SessionInit(string sessionId) =>
            new ChatEvent(ChatEventKind.SessionInit, sessionId: sessionId, isOk: true);

        public static ChatEvent Error(string message) =>
            new ChatEvent(ChatEventKind.Error, text: message, isOk: false);
    }
}
