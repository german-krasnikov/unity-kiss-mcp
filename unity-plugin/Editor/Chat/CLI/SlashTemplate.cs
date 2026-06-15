// Data types for slash-command templates — pure value types, no Unity API.
using System;

namespace UnityMCP.Editor.Chat
{
    [Flags]
    internal enum ContextGather
    {
        None         = 0,
        CompileErrors = 1,
        Selection    = 2,
        SceneState   = 4,
        Console      = 8,
    }

    internal readonly struct SlashTemplate
    {
        internal readonly string        Name;
        internal readonly string        Prefill;
        internal readonly ContextGather Gather;

        internal SlashTemplate(string name, string prefill, ContextGather gather)
        {
            Name    = name;
            Prefill = prefill;
            Gather  = gather;
        }
    }
}
