// Tiny pure helper — caches the last text dispatched to the backend.
// DispatchTurn sets it; SaveStateBeforeReload reads it.
// Separated so unit tests can drive without an EditorWindow context.
namespace UnityMCP.Editor.Chat
{
    internal sealed class SentTextCache
    {
        private string _value = "";

        internal void Set(string text) => _value = text ?? "";

        internal string Get() => _value;
    }
}
