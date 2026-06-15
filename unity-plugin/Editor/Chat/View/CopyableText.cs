// Attaches right-click "Copy" context menu to transcript elements.
// Reads copy text from VisualElement.userData: string for bubbles, ToolCallRecord for chips.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class CopyableText
    {
        internal static void Attach(VisualElement el)
        {
            el.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy", _ =>
                {
                    var text = ReadText(el);
                    if (!string.IsNullOrEmpty(text))
                        EditorGUIUtility.systemCopyBuffer = text;
                });

                if (el.ClassListContains("msg-bubble--user"))
                {
                    evt.menu.AppendAction("Show LLM payload", _ =>
                    {
                        var raw = el.userData is UserBubbleData u ? u.Llm : el.userData as string;
                        if (!string.IsNullOrEmpty(raw))
                            UnityEngine.Debug.Log($"[MCP Chat] LLM payload:\n{raw}");
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
                        EditorGUIUtility.systemCopyBuffer = joined;
                });
            }));
        }

        private static string ReadText(VisualElement el)
        {
            if (el.userData is ToolCallRecord rec) return CopyTextBuilder.ForToolChip(rec);
            if (el.userData is UserBubbleData u)   return u.Display;
            if (el.userData is string s)            return s;
            return null;
        }
    }
}
