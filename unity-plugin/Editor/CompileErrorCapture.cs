using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;

namespace UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class CompileErrorCapture
    {
        static readonly List<string> _errors = new();
        const int MaxErrors = 50;

        static CompileErrorCapture()
        {
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnCompilationFinished;
        }

        static void OnCompilationStarted(object obj) => _errors.Clear();

        static void OnCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var msg in messages)
            {
                if (msg.type != CompilerMessageType.Error) continue;
                if (_errors.Count >= MaxErrors) break;
                _errors.Add($"{msg.file}:{msg.line}:{msg.column}: {msg.message}");
            }
        }

        public static string GetErrors()
        {
            if (_errors.Count == 0) return "No compilation errors";
            var sb = new StringBuilder();
            sb.AppendLine($"{_errors.Count} compilation error(s):");
            foreach (var e in _errors) sb.AppendLine(e);
            return sb.ToString().TrimEnd();
        }

        public static void Clear() => _errors.Clear();
    }
}
