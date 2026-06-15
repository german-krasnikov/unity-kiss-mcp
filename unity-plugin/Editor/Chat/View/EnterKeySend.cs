// UIToolkit glue over EnterKeyLogic — attaches Enter-to-send behavior to a TextField.
// Uses a dedup flag to handle Unity's double KeyDown (keyCode=Return then character='\n')
// so the char echo is suppressed and no stray newline reaches the inner text editor.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class EnterKeySend
    {
        /// <summary>
        /// Registers TrickleDown KeyDown/KeyUp callbacks.
        /// Enter = send (no newline ever written). Alt+Enter = insert exactly one '\n'.
        /// The <c>handled</c> flag deduplicates the two KeyDownEvents Unity fires per press.
        /// </summary>
        internal static void Attach(TextField field, Action onSend)
        {
            bool handled = false;

            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool isEnter = evt.keyCode == KeyCode.Return
                            || evt.keyCode == KeyCode.KeypadEnter
                            || EnterKeyLogic.IsEnterChar(evt.character);

                if (!isEnter) { handled = false; return; } // non-Enter key resets dedup

                var (action, suppress) = EnterKeyLogic.DecideEnter(evt.altKey, handled);
                if (suppress)
                {
                    evt.StopPropagation();
                    evt.StopImmediatePropagation();
                }

                if (action == EnterAction.Send)
                {
                    handled = true;
                    onSend();
                }
                else if (action == EnterAction.Newline)
                {
                    handled = true;
                    var (t, c) = EnterKeyLogic.InsertNewline(field.value ?? "", field.cursorIndex);
                    field.value = t;
                    field.cursorIndex = field.selectIndex = c;
                }
                // Ignore: already handled (echo) — event already suppressed above
            }, TrickleDown.TrickleDown);

            // Reset dedup on key-up so the next physical press is treated fresh.
            field.RegisterCallback<KeyUpEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    handled = false;
            }, TrickleDown.TrickleDown);
        }
    }
}
