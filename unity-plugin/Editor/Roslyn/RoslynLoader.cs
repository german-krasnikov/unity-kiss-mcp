using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>Lazy Roslyn DLL loader — extracted from CodeExecutor to share with RoslynWorkspace.</summary>
    internal static class RoslynLoader
    {
        private static Assembly _core;
        private static Assembly _compiler;
        private static bool     _loaded;

        public static Assembly RoslynCore     => _core;
        public static Assembly RoslynCompiler => _compiler;

        /// <summary>Load Roslyn DLLs on first call. Returns true if successful, false if DLLs not found.</summary>
        public static bool EnsureRoslyn()
        {
            if (_loaded) return true;

            var base_ = EditorApplication.applicationContentsPath;
            var candidates = new[] {
                Path.Combine(base_, "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"),
                Path.Combine(base_, "Resources", "Scripting", "MonoBleedingEdge", "lib", "mono", "msbuild", "Current", "bin", "Roslyn"),
                Path.Combine(base_, "Resources", "Scripting", "DotNetSdkRoslyn"),
                Path.Combine(base_, "DotNetSdkRoslyn"),
            };

            var roslynDir = candidates.FirstOrDefault(Directory.Exists);
            if (roslynDir == null) return false;

            try
            {
                _core     = LoadAssembly(roslynDir, "Microsoft.CodeAnalysis.dll");
                _compiler = LoadAssembly(roslynDir, "Microsoft.CodeAnalysis.CSharp.dll");
                _loaded   = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static Assembly LoadAssembly(string dir, string name)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path))
                throw new InvalidOperationException($"Roslyn DLL not found: {path}");
            return Assembly.LoadFrom(path);
        }
    }
}
