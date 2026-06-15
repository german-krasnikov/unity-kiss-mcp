// Holds both the display text (short names, for Copy + label rendering)
// and the actual LLM payload (full paths + bracket block, for "Show LLM payload").
namespace UnityMCP.Editor.Chat
{
    internal sealed class UserBubbleData
    {
        internal readonly string Display;
        internal readonly string Llm;

        internal UserBubbleData(string display, string llm)
        {
            Display = display;
            Llm     = llm;
        }
    }
}
