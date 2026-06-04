using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    public class CodeExecutorSecurityTests
    {
        // ── Blocked patterns ─────────────────────────────────────────────────

        [Test]
        public void SecurityScan_EnvironmentExit_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan("Environment.Exit(0); return null;"));

        [Test]
        public void SecurityScan_UsingSystemDiagnostics_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.Diagnostics;\nclass X { static void Run() { Process.Start(\"calc\"); } }"));

        [Test]
        public void SecurityScan_UsingSystemIO_Throws()
            => Assert.Throws<System.InvalidOperationException>(
                () => CodeExecutor.SecurityScan(
                    "using System.IO;\nclass X { static void Run() { File.Delete(\"x\"); } }"));

        // ── Legit snippets ───────────────────────────────────────────────────

        [Test]
        public void SecurityScan_FindGameObject_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan("return GameObject.Find(\"Player\")?.name;"));

        [Test]
        public void SecurityScan_UnityEditorSelection_DoesNotThrow()
            => Assert.DoesNotThrow(
                () => CodeExecutor.SecurityScan(
                    "return UnityEditor.Selection.activeGameObject?.name ?? \"none\";"));
    }
}
