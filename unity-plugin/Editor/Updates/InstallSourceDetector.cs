using System.IO;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor
{
    internal static class InstallSourceDetector
    {
        internal enum Source { Local, Git, Registry, Embedded, Unknown }

#if UNITY_INCLUDE_TESTS
        static Source?  _sourceOverride;
        static string   _repoRootOverride;
        static bool     _hasOverride;

        internal static void SetSourceForTest(Source s)    { _sourceOverride = s; _hasOverride = true; }
        internal static void SetLocalRepoRootForTest(string r) { _repoRootOverride = r; _hasOverride = true; }
        internal static void ClearTestOverride()           { _sourceOverride = null; _repoRootOverride = null; _hasOverride = false; }
#endif

        internal static Source Detect()
        {
#if UNITY_INCLUDE_TESTS
            if (_hasOverride && _sourceOverride.HasValue) return _sourceOverride.Value;
#endif
            var info = PackageInfo.FindForAssembly(typeof(InstallSourceDetector).Assembly);
            if (info == null) return Source.Unknown;
            return info.source switch
            {
                PackageSource.Local    => Source.Local,
                PackageSource.Git      => Source.Git,
                PackageSource.Registry => Source.Registry,
                PackageSource.Embedded => Source.Embedded,
                _                      => Source.Unknown,
            };
        }

        internal static string LocalRepoRoot()
        {
#if UNITY_INCLUDE_TESTS
            if (_hasOverride && _repoRootOverride != null) return _repoRootOverride;
#endif
            var info = PackageInfo.FindForAssembly(typeof(InstallSourceDetector).Assembly);
            if (info?.source != PackageSource.Local) return null;
            var parent = Path.GetFullPath(Path.Combine(info.resolvedPath, ".."));
            return IsLocalRepoRoot(parent) ? parent : null;
        }

        /// <summary>Pure helper — checks if a directory looks like the MCP repo root.</summary>
        internal static bool IsLocalRepoRoot(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            return File.Exists(Path.Combine(dir, "install.py"));
        }
    }
}
