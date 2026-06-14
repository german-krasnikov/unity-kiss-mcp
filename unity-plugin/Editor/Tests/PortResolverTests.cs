using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PortResolverTests
    {
        // ── ResolvePort ───────────────────────────────────────────────────────

        [Test]
        public void ResolvePort_EnvOverride_ReturnsEnvValue()
        {
            Assert.AreEqual(9550, PortResolver.ResolvePort("9550", null, 9500));
        }

        [Test]
        public void ResolvePort_EnvOutOfRange_FallsToJson()
        {
            var result = PortResolver.ResolvePort("80", "{\"port\":9510}", 9500);
            Assert.AreEqual(9510, result);
        }

        [Test]
        public void ResolvePort_EnvInvalid_FallsToJson()
        {
            var result = PortResolver.ResolvePort("abc", "{\"port\":9510}", 9500);
            Assert.AreEqual(9510, result);
        }

        [Test]
        public void ResolvePort_JsonValid_ReturnsSavedPort()
        {
            Assert.AreEqual(9510, PortResolver.ResolvePort(null, "{\"port\":9510}", 9500));
        }

        [Test]
        public void ResolvePort_JsonMissingKey_FindsFreePort()
        {
            var result = PortResolver.ResolvePort(null, "{\"chatPort\":9501}", 9500);
            Assert.IsTrue(PortResolver.IsValidPort(result));
        }

        [Test]
        public void ResolvePort_JsonCorrupted_FindsFreePort()
        {
            var result = PortResolver.ResolvePort(null, "{garbage", 9500);
            Assert.IsTrue(PortResolver.IsValidPort(result));
        }

        [Test]
        public void ResolvePort_JsonNull_FindsFreePort()
        {
            var result = PortResolver.ResolvePort(null, null, 9500);
            Assert.IsTrue(PortResolver.IsValidPort(result));
        }

        [Test]
        public void ResolvePort_JsonPortOutOfRange_FindsFreePort()
        {
            var result = PortResolver.ResolvePort(null, "{\"port\":80}", 9500);
            Assert.IsTrue(PortResolver.IsValidPort(result));
        }

        // ── ResolveChatPort ───────────────────────────────────────────────────

        [Test]
        public void ResolveChatPort_EnvOverride_ReturnsEnvValue()
        {
            Assert.AreEqual(9560, PortResolver.ResolveChatPort("9560", null, 9500, 9501));
        }

        [Test]
        public void ResolveChatPort_EnvOutOfRange_FallsToJson()
        {
            var result = PortResolver.ResolveChatPort("99999", "{\"port\":9500,\"chatPort\":9501}", 9500, 9501);
            Assert.AreEqual(9501, result);
        }

        [Test]
        public void ResolveChatPort_JsonValid_ReturnsSaved()
        {
            Assert.AreEqual(9501, PortResolver.ResolveChatPort(null, "{\"port\":9500,\"chatPort\":9501}", 9500, 9502));
        }

        [Test]
        public void ResolveChatPort_JsonMissingChatPort_FindsFreePort()
        {
            var result = PortResolver.ResolveChatPort(null, "{\"port\":9500}", 9500, 9501);
            Assert.IsTrue(PortResolver.IsValidPort(result));
        }

        // ── ParsePortFromJson ─────────────────────────────────────────────────

        [Test]
        public void ParsePortFromJson_ValidPort_ReturnsValue()
        {
            Assert.AreEqual(9500, PortResolver.ParsePortFromJson("{\"port\":9500}", "port"));
        }

        [Test]
        public void ParsePortFromJson_MissingKey_ReturnsNull()
        {
            Assert.IsNull(PortResolver.ParsePortFromJson("{\"other\":1}", "port"));
        }

        [Test]
        public void ParsePortFromJson_WhitespaceVariants_Works()
        {
            Assert.AreEqual(9500, PortResolver.ParsePortFromJson("{\"port\" : 9500}", "port"));
        }

        [Test]
        public void ParsePortFromJson_EmptyString_ReturnsNull()
        {
            Assert.IsNull(PortResolver.ParsePortFromJson("", "port"));
        }

        [Test]
        public void ParsePortFromJson_Null_ReturnsNull()
        {
            Assert.IsNull(PortResolver.ParsePortFromJson(null, "port"));
        }

        // ── IsValidPort ───────────────────────────────────────────────────────

        [Test]
        public void IsValidPort_BelowMin_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsValidPort(1023));
        }

        [Test]
        public void IsValidPort_AboveMax_ReturnsFalse()
        {
            Assert.IsFalse(PortResolver.IsValidPort(65536));
        }

        [Test]
        public void IsValidPort_MinBound_ReturnsTrue()
        {
            Assert.IsTrue(PortResolver.IsValidPort(1024));
        }

        [Test]
        public void IsValidPort_MaxBound_ReturnsTrue()
        {
            Assert.IsTrue(PortResolver.IsValidPort(65535));
        }

        // ── FindFreePort ──────────────────────────────────────────────────────

        [Test]
        public void FindFreePort_ReturnsValidPort()
        {
            var port = PortResolver.FindFreePort(9500);
            Assert.IsTrue(PortResolver.IsValidPort(port));
        }

        [Test]
        public void FindFreePort_SkipsCollisionPort()
        {
            var port = PortResolver.FindFreePort(9500, skipPort: 9500);
            Assert.AreNotEqual(9500, port);
            Assert.IsTrue(PortResolver.IsValidPort(port));
        }

        // ── SavePorts ─────────────────────────────────────────────────────────

        [Test]
        public void SavePorts_WritesCorrectJson()
        {
            var path = Path.Combine(Path.GetTempPath(), "mcp_test_ports.json");
            PortResolver.SavePorts(path, 9500, 9501);
            var content = File.ReadAllText(path);
            Assert.AreEqual("{\"port\":9500,\"chatPort\":9501}", content);
            File.Delete(path);
        }

        [Test]
        public void SavePorts_CreatesDirectory()
        {
            var dir = Path.Combine(Path.GetTempPath(), "mcp_test_dir_" + System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "ports.json");
            PortResolver.SavePorts(path, 9500, 9501);
            Assert.IsTrue(File.Exists(path));
            Directory.Delete(dir, true);
        }

        [Test]
        public void SavePorts_OnCorruptInputFile_WritesValidJson()
        {
            // If MCP_Port.json is corrupt, SavePorts must still produce valid JSON.
            // ParsePortFromJson uses regex — extracts reloadPort=9601 from corrupt string despite broken JSON.
            var path = Path.Combine(Path.GetTempPath(), "mcp_corrupt_" + System.Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{\"port\":9500,\"chatPort\":9501}\"reloadPort\":9601}");

            PortResolver.SavePorts(path, 9500, 9501);

            var content = File.ReadAllText(path);
            Assert.IsTrue(content.TrimStart().StartsWith("{"), "must be valid JSON object");
            Assert.IsTrue(content.TrimEnd().EndsWith("}"), "must be valid JSON object");
            // port and chatPort must be correct.
            Assert.AreEqual(9500, PortResolver.ParsePortFromJson(content, "port"));
            Assert.AreEqual(9501, PortResolver.ParsePortFromJson(content, "chatPort"));
            File.Delete(path);
        }
    }
}
