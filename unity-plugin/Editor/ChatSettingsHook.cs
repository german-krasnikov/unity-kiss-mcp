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

        public static event Action<VisualElement> OnBuild;

        internal static void Invoke(VisualElement root) => OnBuild?.Invoke(root);

        public static bool IsChatEnabled()
        {
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.GetScriptingDefineSymbols(
                NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone), out var symbols);
#else
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Split(';');
#endif
            foreach (var s in symbols)
                if (s.Trim() == ChatDefine) return true;
            return false;
        }

        public static void SetChatEnabled(bool enabled)
        {
#if UNITY_2021_2_OR_NEWER
            var named = NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Standalone);
            PlayerSettings.GetScriptingDefineSymbols(named, out var current);
#else
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone).Split(';');
#endif
            var list = new List<string>(current);
            list.RemoveAll(s => s.Trim() == ChatDefine);
            if (enabled) list.Add(ChatDefine);
            var joined = string.Join(";", list);

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(named, joined);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, joined);
#endif
            UnityEngine.Debug.Log($"[MCP Chat] {ChatDefine} define {(enabled ? "added" : "removed")}. Unity will recompile.");
        }
    }
}
