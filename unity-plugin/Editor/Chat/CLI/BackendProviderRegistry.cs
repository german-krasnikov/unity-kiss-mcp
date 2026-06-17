// Auto-discovers IBackendProvider implementations via TypeCache.
// Test seam: set _override before calling All/Get.
using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityMCP.Editor.Chat
{
    internal static class BackendProviderRegistry
    {
        private static List<IBackendProvider> _cache;

#if UNITY_INCLUDE_TESTS
        // Inject known providers in unit tests (TypeCache is Unity-only).
        internal static List<IBackendProvider> Override;
        internal static void ResetForTests() { _cache = null; Override = null; }
#endif

        /// <summary>All discovered providers, sorted by SortOrder.</summary>
        internal static IReadOnlyList<IBackendProvider> All
        {
            get
            {
#if UNITY_INCLUDE_TESTS
                if (Override != null) return Override;
#endif
                return _cache ?? (_cache = Discover());
            }
        }

        /// <summary>Find provider by ProviderId. Returns null if not found.</summary>
        internal static IBackendProvider Get(string providerId)
        {
            foreach (var p in All)
                if (string.Equals(p.ProviderId, providerId, StringComparison.Ordinal))
                    return p;
            return null;
        }

        /// <summary>Map BackendKind enum to ProviderId string.</summary>
        internal static string KindToId(BackendKind kind)
        {
            switch (kind)
            {
                case BackendKind.Codex:    return "codex";
                case BackendKind.Gemini:   return "gemini";
                case BackendKind.Kimi:     return "kimi";
                case BackendKind.OpenCode: return "opencode";
                default:                   return "claude";
            }
        }

        private static List<IBackendProvider> Discover()
        {
            var result = new List<IBackendProvider>();
#if UNITY_EDITOR
            foreach (var type in TypeCache.GetTypesDerivedFrom<IBackendProvider>())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                try { result.Add((IBackendProvider)Activator.CreateInstance(type)); }
                catch { /* skip broken providers */ }
            }
#endif
            result.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return result;
        }
    }
}
