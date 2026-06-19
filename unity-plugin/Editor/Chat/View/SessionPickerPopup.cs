// CLI→Chat session picker: shows a GenericMenu with recent CLI sessions.
// No external deps beyond UnityEditor (GenericMenu).
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class SessionPickerPopup
    {
        /// <summary>
        /// Show a GenericMenu listing recent CLI sessions for the given backend.
        /// onSelected is called with the chosen SessionEntry.
        /// </summary>
        internal static void Show(BackendKind kind, Action<SessionEntry> onSelected)
        {
            var entries = SessionScanner.Scan(kind);

            var menu = new GenericMenu();
            if (entries.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No sessions found"));
            }
            else
            {
                foreach (var entry in entries)
                {
                    var captured = entry; // closure capture
                    var label    = $"{entry.Title}  ({entry.Date})";
                    menu.AddItem(new GUIContent(label), false, () => onSelected(captured));
                }
            }
            menu.ShowAsContext();
        }
    }
}
