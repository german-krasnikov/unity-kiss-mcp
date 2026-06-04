// Compose a compact [Unity State] block for injection at session start or turn resume.
// Plain text, NOT JSON. ~200-500 chars typical.
using System.Text;

namespace UnityMCP.Editor.Chat
{
    internal static class EditorStateSnapshot
    {
        internal const int SceneBudget = 500;

#if UNITY_INCLUDE_TESTS
        // Test seam: override scene source without touching HierarchySerializer.
        // Set before Capture() in a test; reset to null after.
        internal static System.Func<string> SceneProviderOverride;
#endif

        /// <summary>
        /// Returns a plain-text snapshot of scene, compile state, and recent console errors.
        /// Returns "" only when genuinely nothing to report (should not happen in practice).
        /// </summary>
        internal static string Capture()
        {
            var sb = new StringBuilder("[Unity State]\n");

            // Scene summary — capped to SceneBudget chars to avoid bloating --append-system-prompt.
#if UNITY_INCLUDE_TESTS
            var scene = SceneProviderOverride != null
                ? SceneProviderOverride()
                : HierarchySerializer.SerializeSummary();
#else
            var scene = HierarchySerializer.SerializeSummary();
#endif
            if (string.IsNullOrWhiteSpace(scene)) scene = "(empty)";
            else scene = scene.Trim();
            if (scene.Length > SceneBudget) scene = scene.Substring(0, SceneBudget) + "…(truncated)";
            sb.Append("Scene: ");
            sb.AppendLine(scene);

            // Compile state
            var errors = CompileErrorCapture.GetErrors();
            if (errors == "No compilation errors")
            {
                sb.AppendLine("Compile: clean");
            }
            else
            {
                sb.Append("Compile: ");
                sb.AppendLine(errors.Trim());
            }

            // Console errors (tail of last 5) — omit when none
            var consoleTail = ConsoleCapture.GetLogs(5, "error");
            if (!string.IsNullOrEmpty(consoleTail))
            {
                sb.Append("Console: ");
                sb.AppendLine(consoleTail.Trim());
            }

            return sb.ToString().TrimEnd();
        }
    }
}
