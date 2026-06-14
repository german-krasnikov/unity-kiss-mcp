// ReloadDomainStamp — MVID:mtime stamp for UnityMCP.Reload assembly.
// Pure function, zero deps on UnityMCP.Editor (CS0122 trap avoided: all public).
using System;
using System.IO;
using UnityEditor;

namespace UnityMCP.Reload
{
    /// <summary>
    /// Computes and reads the domain reload stamp for the UnityMCP.Reload assembly.
    /// Stamp format: "{mvid}:{mtime_ticks}" — changes on every recompile.
    /// NOTE: tracks UnityMCP.Reload.dll, not UnityMCP.Editor.dll.
    /// To read main-package stamp use SessionState key "MCP_DomainStamp" directly.
    /// </summary>
    public static class ReloadDomainStamp
    {
        // SessionState key where main SyncHelper writes its stamp.
        // Hardcoded: must match SyncHelper.cs StampKey constant.
        private const string MainStampKey = "MCP_DomainStamp";

        /// <summary>
        /// Computes stamp for the reload assembly itself (UnityMCP.Reload.dll).
        /// Returns "{mvid}:{mtime_ticks}" or "" if assembly not loaded yet.
        /// </summary>
        public static string ComputeStamp()
        {
            var asm = typeof(ReloadDomainStamp).Assembly;
            if (asm == null) return "";
            var mvid = asm.ManifestModule.ModuleVersionId;
            var loc  = asm.Location;
            long mtime = 0;
            if (!string.IsNullOrEmpty(loc) && File.Exists(loc))
                mtime = new FileInfo(loc).LastWriteTimeUtc.Ticks;
            return $"{mvid}:{mtime}";
        }

        /// <summary>
        /// Reads the main package (UnityMCP.Editor) domain stamp from SessionState.
        /// Written by SyncHelper.OnAfterReload in the main package.
        /// Returns "" if main package never loaded or SessionState cleared.
        /// </summary>
        public static string MainDomainStamp =>
            SessionState.GetString(MainStampKey, "");

        /// <summary>
        /// Gets the MVID of the UnityMCP.Editor assembly via reflection (thread-safe).
        /// Returns MVID string or "absent" if assembly not loaded.
        /// </summary>
        public static string MainAsmdefMvid()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "UnityMCP.Editor")
                    return asm.ManifestModule.ModuleVersionId.ToString();
            }
            return "absent";
        }
    }
}
