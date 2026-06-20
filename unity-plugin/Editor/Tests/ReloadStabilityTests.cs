// Stress tests for reload stability: ComputeStamp multi-assembly (SD-1) + PackageJson version (CP-5).
// EditMode only. Every test < 15 lines.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    // ── Group 1: ComputeStamp multi-assembly (SD-1) ───────────────────────────

    [TestFixture]
    public class ComputeStampStressTests
    {
        // T-A: stamp always contains ';' (SD-1: each MVID appended with ';')
        [Test]
        public void ComputeStamp_AlwaysContainsSemicolon()
        {
            var stamp = SyncHelper.ComputeStamp();
            StringAssert.Contains(";", stamp,
                "SD-1: each MVID is appended with ';'; stamp must always contain it");
        }

        // T-B: two consecutive calls return identical strings (no side effects)
        [Test]
        public void ComputeStamp_IsDeterministic()
        {
            var s1 = SyncHelper.ComputeStamp();
            var s2 = SyncHelper.ComputeStamp();
            Assert.AreEqual(s1, s2, "ComputeStamp must be deterministic within same domain");
        }
    }

    // ── Group 2: Package structure (CP-5) ────────────────────────────────────

    [TestFixture]
    public class PackageStructureTests
    {
        // T-J: package.json version is exactly 0.42.0 (regression guard for CP-5)
        [Test]
        public void PackageJson_Version_Is0420()
        {
            var p = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "..", "..", "unity-plugin/package.json"));
            if (!System.IO.File.Exists(p)) { Assert.Ignore($"package.json not found: {p}"); return; }
            var json = System.IO.File.ReadAllText(p);
            StringAssert.Contains("\"version\": \"0.42.0\"", json,
                "CP-5: package.json must be 0.42.0 to force DLL cache invalidation after asmdef split");
        }
    }
}
