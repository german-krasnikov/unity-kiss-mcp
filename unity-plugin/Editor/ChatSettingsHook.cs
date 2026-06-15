using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Event hook that lets Chat (and future optional modules) inject a settings section
    /// into MCPSettingsUI WITHOUT reversing the dependency. Core fires it; Chat subscribes.
    /// Zero subscribers = no-op (safe to delete the Chat module entirely).
    ///
    /// Also owns the UNITY_MCP_CHAT define get/set — it lives in core (always compiled) so the
    /// enable toggle stays reachable even when the Chat asmdef is OFF (no chicken-and-egg).
    /// </summary>
    public static class ChatSettingsHook
    {
        public const string ChatDefine = "UNITY_MCP_CHAT";

        public static event Action<VisualElement> OnBuildConnection;

        internal static void InvokeConnection(VisualElement root) => OnBuildConnection?.Invoke(root);
        internal static bool HasConnectionSubscribers => OnBuildConnection != null;
        internal static void ResetConnectionEvent() => OnBuildConnection = null;

        internal static void InvokeConnectionViaReflection(VisualElement root)
        {
            try
            {
                var t = System.Type.GetType(
                    "UnityMCP.Editor.Chat.ChatSettingsSection, UnityMCP.Editor.Chat.View");
                if (t == null) return;
                var m = t.GetMethod("BuildContent",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(VisualElement) }, null);
                m?.Invoke(null, new object[] { root });
            }
            catch { }
        }

        public static bool IsChatBinaryAvailable()
        {
            try
            {
                var t = System.Type.GetType("UnityMCP.Editor.Chat.ChatBinaryResolver, UnityMCP.Editor.Chat");
                if (t == null) return false;
                var method = t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(bool) }, null);
                return method?.Invoke(null, new object[] { false }) as string != null;
            }
            catch { return false; }
        }

        public static bool IsChatEnabled()
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(group), out var symbols);
#else
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';');
#endif
            foreach (var s in symbols)
                if (s.Trim() == ChatDefine) return true;
            return false;
        }

        public static void SetChatEnabled(bool enabled)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
            var named = NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.GetScriptingDefineSymbols(named, out var current);
#else
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';');
#endif
            var joined = enabled ? AddDefine(string.Join(";", current), ChatDefine)
                                 : RemoveDefine(string.Join(";", current), ChatDefine);

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(named, joined);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, joined);
#endif
            UnityEngine.Debug.Log($"[MCP Chat] {ChatDefine} define {(enabled ? "added" : "removed")}. Unity will recompile.");
        }

        // Pure helpers — testable without Unity API. Semicolon-separated define lists.
        internal static string AddDefine(string defines, string symbol)
        {
            var list = SplitDefines(defines);
            if (list.Contains(symbol)) return defines; // idempotent
            list.Add(symbol);
            return string.Join(";", list);
        }

        internal static string RemoveDefine(string defines, string symbol)
        {
            var list = SplitDefines(defines);
            // Exact match only — never remove FOOBAR when removing FOO
            list.RemoveAll(s => s == symbol);
            return string.Join(";", list);
        }

        private static List<string> SplitDefines(string defines)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(defines)) return result;
            foreach (var s in defines.Split(';'))
            {
                var t = s.Trim();
                if (t.Length > 0) result.Add(t);
            }
            return result;
        }
    }
}
