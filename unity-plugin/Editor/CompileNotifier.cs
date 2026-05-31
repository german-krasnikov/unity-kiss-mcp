using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class CompileNotifier
    {
        private const string StartKey = "MCP_CompileStart";
        private const string DurationKey = "MCP_LastDuration";

        static CompileNotifier()
        {
            CompilationPipeline.compilationStarted += _ =>
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);

            CompilationPipeline.compilationFinished += _ =>
            {
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start > 0f)
                    SessionState.SetFloat(DurationKey, (float)EditorApplication.timeSinceStartup - start);
                SessionState.SetFloat(StartKey, 0f);
            };
        }

        public static bool IsCompiling => SessionState.GetFloat(StartKey, 0f) > 0f;

        public static float ElapsedSeconds => IsCompiling
            ? (float)EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f)
            : 0f;

        public static float LastDurationSeconds => SessionState.GetFloat(DurationKey, 0f);

        public static string GetStatus()
        {
            if (IsCompiling)
                return "compiling|" + ElapsedSeconds.ToString("F1", CultureInfo.InvariantCulture);
            var last = LastDurationSeconds;
            return last > 0f ? "idle|" + last.ToString("F1", CultureInfo.InvariantCulture) : "idle|0";
        }
    }
}
