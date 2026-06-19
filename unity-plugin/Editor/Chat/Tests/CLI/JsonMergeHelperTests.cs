// TDD tests for JsonMergeHelper.
// Pure unit tests — no FS, no Unity API.
using System.Linq;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class JsonMergeHelperTests
    {
        // ── ReplaceEntry: key not found ───────────────────────────────────────

        [Test]
        public void ReplaceEntry_KeyNotFound_ReturnsNull()
        {
            var json = "{\"other\":{\"a\":1}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "missing", "{\"x\":2}");
            Assert.IsNull(result, "Must return null when key is absent");
        }

        [Test]
        public void ReplaceEntry_EmptyJson_ReturnsNull()
        {
            Assert.IsNull(JsonMergeHelper.ReplaceEntry("", "key", "{}"));
        }

        [Test]
        public void ReplaceEntry_NullJson_ReturnsNull()
        {
            Assert.IsNull(JsonMergeHelper.ReplaceEntry(null, "key", "{}"));
        }

        // ── ReplaceEntry: basic replacement ──────────────────────────────────

        [Test]
        public void ReplaceEntry_ReplacesValue_PreservesKey()
        {
            var json = "{\"unity-mcp\":{\"command\":\"python3\",\"port\":9501}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-mcp", "{\"command\":\"python3\",\"port\":9900}");
            StringAssert.Contains("\"unity-mcp\"", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
        }

        [Test]
        public void ReplaceEntry_PreservesOtherEntries()
        {
            var json = "{\"blender\":{\"cmd\":\"blender-mcp\"},\"unity-mcp\":{\"port\":9501}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-mcp", "{\"port\":9900}");
            StringAssert.Contains("\"blender\"", result);
            StringAssert.Contains("blender-mcp", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
        }

        // ── ReplaceEntry: brace balance ───────────────────────────────────────

        [Test]
        public void ReplaceEntry_OutputBracesAreBalanced()
        {
            var json = "{\"unity-mcp\":{\"command\":\"python3\",\"env\":{\"UNITY_MCP_PORT\":\"9501\"}}}";
            var fresh = "{\"command\":\"python3\",\"env\":{\"UNITY_MCP_PORT\":\"9900\"}}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-mcp", fresh);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Output braces must be balanced");
        }

        [Test]
        public void ReplaceEntry_NestedBracesInValue_HandledCorrectly()
        {
            // Value contains nested braces — depth matching must not stop early
            var json = "{\"unity-mcp\":{\"env\":{\"KEY\":\"old\"},\"args\":[]}}";
            var fresh = "{\"env\":{\"KEY\":\"new\"},\"args\":[]}";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-mcp", fresh);
            StringAssert.Contains("\"new\"", result);
            StringAssert.DoesNotContain("\"old\"", result);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Brace count must be balanced with nested value braces");
        }

        [Test]
        public void ReplaceEntry_MultipleOtherEntries_AllPreserved()
        {
            var json =
                "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"blender\": { \"command\": \"blender-mcp\" },\n" +
                "    \"unity-mcp\": { \"command\": \"python3\", \"env\": { \"UNITY_MCP_PORT\": \"9501\" } },\n" +
                "    \"figma\": { \"command\": \"figma-mcp\" }\n" +
                "  }\n" +
                "}\n";
            var fresh = "{ \"command\": \"python3\", \"env\": { \"UNITY_MCP_PORT\": \"9900\" } }";
            var result = JsonMergeHelper.ReplaceEntry(json, "unity-mcp", fresh);

            StringAssert.Contains("blender-mcp", result);
            StringAssert.Contains("figma-mcp", result);
            StringAssert.Contains("9900", result);
            StringAssert.DoesNotContain("9501", result);
            Assert.AreEqual(result.Count(c => c == '{'), result.Count(c => c == '}'),
                "Brace count must be balanced");
        }
    }
}
