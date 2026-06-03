// Immutable record of one tool call: name, id, assembled args, optional result.
// Pure: zero UnityEngine deps. Used as chip.userData in the transcript.
namespace UnityMCP.Editor.Chat
{
    public readonly struct ToolCallRecord
    {
        public string Name       { get; }
        public string Id         { get; }
        public string ArgsJson   { get; }
        public string ResultText { get; }   // null until result arrives
        public bool   IsOk       { get; }
        public bool   HasResult  => ResultText != null;

        public ToolCallRecord(string name, string id, string argsJson,
            string resultText = null, bool isOk = true)
        {
            Name = name; Id = id; ArgsJson = argsJson;
            ResultText = resultText; IsOk = isOk;
        }

        public ToolCallRecord WithResult(string text, bool ok) =>
            new ToolCallRecord(Name, Id, ArgsJson, text, ok);
    }
}
