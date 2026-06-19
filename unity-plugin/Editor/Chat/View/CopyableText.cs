// Attaches right-click "Copy" context menu to transcript elements.
// Reads copy text from VisualElement.userData: string for bubbles, ToolCallRecord for chips.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class CopyableText
    {
        // Label constant — single source of truth for tests and runtime.
        internal const string LabelCopyAsSent = "Copy as sent to LLM";

        internal static void Attach(VisualElement el)
        {
            el.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy", _ =>
                {
                    var text = ReadText(el);
                    if (!string.IsNullOrEmpty(text))
                    {
                        EditorGUIUtility.systemCopyBuffer = text;
                        CopyFlash.Show();
                    }
                });

                if (el.ClassListContains("msg-bubble--user"))
                {
                    evt.menu.AppendAction(LabelCopyAsSent, _ =>
                    {
                        var raw = el.userData is UserBubbleData u ? u.Llm : el.userData as string;
                        if (!string.IsNullOrEmpty(raw))
                        {
                            EditorGUIUtility.systemCopyBuffer = raw;
                            CopyFlash.Show();
                        }
                    });
                }
            }));
        }

        internal static void AttachToGroup(Foldout group, VisualElement groupBody)
        {
            if (groupBody == null) return;
            group.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy All Tools", _ =>
                {
                    var texts = new List<string>();
                    foreach (var child in groupBody.Children())
                    {
                        var t = ReadText(child);
                        if (!string.IsNullOrEmpty(t)) texts.Add(t);
                    }
                    var joined = CopyTextBuilder.ForToolGroup(texts);
                    if (!string.IsNullOrEmpty(joined))
                    {
                        EditorGUIUtility.systemCopyBuffer = joined;
                        CopyFlash.Show();
                    }
                });
            }));
        }

        // Test seam: returns the menu labels that would appear on a user bubble.
        internal static IReadOnlyList<string> GetUserBubbleMenuLabels()
            => new[] { "Copy", LabelCopyAsSent };

        private static string ReadText(VisualElement el)
        {
            if (el.userData is ToolCallRecord rec) return CopyTextBuilder.ForToolChip(rec);
            if (el.userData is UserBubbleData u)   return u.Display;
            if (el.userData is string s)            return s;
            return null;
        }
    }
}
