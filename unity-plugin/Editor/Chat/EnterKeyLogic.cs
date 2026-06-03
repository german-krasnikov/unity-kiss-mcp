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
        /// Dedup decision for Enter: Unity can fire two KeyDownEvents per physical
        /// press (keyCode=Return then character='\n'). The flag prevents the echo
        /// from re-acting.
        /// Returns (action, suppress=true): suppress means the caller must call
        /// StopPropagation/PreventDefault so the char never reaches the inner editor.
        /// </summary>
        public static (EnterAction action, bool suppress) DecideEnter(bool altHeld, bool alreadyHandled)
        {
            if (alreadyHandled) return (EnterAction.Ignore, true); // echo → swallow, no re-action
            return (altHeld ? EnterAction.Newline : EnterAction.Send, true);
        }

        /// <summary>Returns true when the character alone signals an Enter press.</summary>
        public static bool IsEnterChar(char c) => c == '\n' || c == '\r';

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
