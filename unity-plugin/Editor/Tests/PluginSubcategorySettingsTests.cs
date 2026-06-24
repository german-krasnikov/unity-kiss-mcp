using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// Tests that per-tool EditorPrefs keys follow the "UnityMCP_Tool_{name}" contract
    /// and that toggling one tool does not affect its siblings.
    /// No UI instantiation — tests the key convention directly.
    /// </summary>
    [TestFixture]
    public class PluginSubcategorySettingsTests
    {
        private const string KeyA = "UnityMCP_Tool_tool_a";
        private const string KeyB = "UnityMCP_Tool_tool_b";

        [SetUp]
        public void SetUp()
        {
            EditorPrefs.SetBool(KeyA, true);
            EditorPrefs.SetBool(KeyB, true);
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(KeyA);
            EditorPrefs.DeleteKey(KeyB);
        }

        [Test]
        public void TogglingOneTool_WritesCorrectPrefKey()
        {
            EditorPrefs.SetBool(KeyA, false);
            Assert.IsFalse(EditorPrefs.GetBool(KeyA, true));
        }

        [Test]
        public void TogglingOneTool_SiblingStaysEnabled()
        {
            EditorPrefs.SetBool(KeyA, false);
            Assert.IsTrue(EditorPrefs.GetBool(KeyB, true),
                "Sibling tool_b must remain enabled when tool_a is disabled");
        }

        [Test]
        public void MCPSettings_IsToolEnabled_ReadsCorrectKey()
        {
            EditorPrefs.SetBool(KeyA, false);
            Assert.IsFalse(MCPSettings.IsToolEnabled("tool_a"),
                "MCPSettings.IsToolEnabled must read UnityMCP_Tool_{name}");
            Assert.IsTrue(MCPSettings.IsToolEnabled("tool_b"),
                "Sibling must still be enabled");
        }
    }
}
