// Stress tests for assembly structure — Wizard asmdef (SH-1) + MovedFrom sourceAssembly (CP-6).
// EditMode only. Every test < 15 lines.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class WizardAssemblyStructureTests
    {
        // T-H: Wizard asmdef has autoReferenced: false (regression guard — was true before SH-1)
        [Test]
        public void WizardAsmdef_AutoReferenced_IsFalse()
        {
            var p = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..",
                "unity-plugin/Editor/Wizard/UnityMCP.Editor.Wizard.asmdef"));
            if (!File.Exists(p)) { Assert.Ignore($"asmdef not found: {p}"); return; }
            var json = File.ReadAllText(p);
            StringAssert.Contains("\"autoReferenced\": false", json,
                "SH-1: Wizard asmdef must have autoReferenced=false to isolate compile errors");
        }

        // T-I: [MovedFrom] sourceAssembly is exactly "UnityMCP.Editor" (not "UnityMCP.Editor.Wizard")
        [Test]
        public void MCPStatusWindow_MovedFrom_SourceAssembly_IsUnityMCPEditor()
        {
            var attrs = typeof(MCPStatusWindow)
                .GetCustomAttributes(typeof(MovedFromAttribute), inherit: false);
            Assert.IsNotEmpty(attrs, "MCPStatusWindow must have [MovedFrom] attribute");
            var p = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..",
                "unity-plugin/Editor/Wizard/MCPStatusWindow.cs"));
            if (!File.Exists(p)) { Assert.Ignore($"Source not found: {p}"); return; }
            var src = File.ReadAllText(p);
            StringAssert.Contains("sourceAssembly: \"UnityMCP.Editor\"", src,
                "CP-6: sourceAssembly must be the OLD assembly (UnityMCP.Editor), not the new one");
        }
    }
}
