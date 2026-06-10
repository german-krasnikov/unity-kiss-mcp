using System.Globalization;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class CompileNotifier
    {
        private const string StartKey    = "MCP_CompileStart";
        private const string DurationKey = "MCP_LastDuration";
        private const string FailedKey   = "MCP_CompileFailed";

        static CompileNotifier()
        {
            CompilationPipeline.compilationStarted += _ =>
            {
                SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetBool(FailedKey, false);
            };

            CompilationPipeline.compilationFinished += _ =>
            {
                var start = SessionState.GetFloat(StartKey, 0f);
                if (start > 0f)
                    SessionState.SetFloat(DurationKey, (float)EditorApplication.timeSinceStartup - start);
                SessionState.SetFloat(StartKey, 0f);
                // Discriminate failed vs success (ref §9: compilationFinished fires on FAIL too)
                if (EditorUtility.scriptCompilationFailed)
                    SessionState.SetBool(FailedKey, true);
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
            var durStr = last > 0f ? last.ToString("F1", CultureInfo.InvariantCulture) : "0";
            // Add fail discriminator so callers can distinguish failed-idle from success-idle
            if (SessionState.GetBool(FailedKey, false))
                return "idle-failed|" + durStr;
            return last > 0f ? "idle|" + durStr : "idle|0";
        }
    }
}
