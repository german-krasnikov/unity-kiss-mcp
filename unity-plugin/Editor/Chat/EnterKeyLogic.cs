// Pure helpers for Enter-key send/newline logic. No UnityEngine deps, NUnit-testable.
namespace UnityMCP.Editor.Chat
{
    public enum EnterAction { Ignore, Send, Newline }

    public static class EnterKeyLogic
    {
        /// <summary>
        /// Classifies a key event. Return/KeypadEnter + no alt = Send;
        /// + alt held = Newline; any other key = Ignore.
        /// </summary>
        public static EnterAction Classify(bool isReturnOrKeypadEnter, bool altHeld)
        {
            if (!isReturnOrKeypadEnter) return EnterAction.Ignore;
            return altHeld ? EnterAction.Newline : EnterAction.Send;
        }

        /// <summary>
        /// Inserts '\n' at caret position. Returns (newText, newCaret).
        /// Safe for empty text and caret at any valid position.
        /// </summary>
        public static (string text, int caret) InsertNewline(string text, int caret)
        {
            text  = text  ?? "";
            caret = System.Math.Clamp(caret, 0, text.Length);
            var newText = text.Substring(0, caret) + '\n' + text.Substring(caret);
            return (newText, caret + 1);
        }
    }
}
