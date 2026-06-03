// UIToolkit glue over EnterKeyLogic — attaches Enter-to-send behavior to a TextField.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class EnterKeySend
    {
        /// <summary>
        /// Registers a TrickleDown KeyDown callback so Enter sends and Alt+Enter inserts a newline.
        /// TrickleDown phase + StopImmediatePropagation prevents the inner text element from
        /// inserting its own newline before our handler runs.
        /// </summary>
        internal static void Attach(TextField field, Action onSend)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool isEnter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
                var action = EnterKeyLogic.Classify(isEnter, evt.altKey);
                if (action == EnterAction.Ignore) return;

                evt.StopPropagation();
                evt.StopImmediatePropagation();
                evt.PreventDefault();

                if (action == EnterAction.Send)
                {
                    onSend();
                }
                else // Newline
                {
                    var (t, c) = EnterKeyLogic.InsertNewline(field.value ?? "", field.cursorIndex);
                    field.value = t;
                    field.cursorIndex = field.selectIndex = c;
                }
            }, TrickleDown.TrickleDown);
        }
    }
}
