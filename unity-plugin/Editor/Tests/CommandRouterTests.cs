// TDD: CommandRouter pure-logic tests — no TCP required, EditMode only.
// Covers: IsAllowedDuringCompile, IsAlwaysAllowed, SuggestNext,
//         CommandRegistry flags, BuildResponse (via Process stub),
//         CommandSchema validation coverage.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterTests
    {
        // ── IsAllowedDuringCompile ────────────────────────────────────────────

        [TestCase("ping",              ExpectedResult = true)]
        [TestCase("get_version",       ExpectedResult = true)]
        [TestCase("get_console",       ExpectedResult = true)]
        [TestCase("screenshot",        ExpectedResult = true)]
        [TestCase("get_enabled_tools", ExpectedResult = true)]
        [TestCase("compile_status",    ExpectedResult = true)]
        [TestCase("get_disabled_tools",ExpectedResult = true)]
        [TestCase("set_tool_catalog",  ExpectedResult = true)]
        public bool IsAllowedDuringCompile_AllowedCommands(string cmd)
            => CommandRouter.IsAllowedDuringCompile(cmd);

        [TestCase("create_object")]
        [TestCase("set_property")]
        [TestCase("delete_object")]
        [TestCase("get_hierarchy")]
        [TestCase("batch")]
        [TestCase("execute_code")]
        public void IsAllowedDuringCompile_BlockedCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRouter.IsAllowedDuringCompile(cmd));

        // ── IsAlwaysAllowed ───────────────────────────────────────────────────

        [TestCase("ping",              ExpectedResult = true)]
        [TestCase("get_version",       ExpectedResult = true)]
        [TestCase("get_enabled_tools", ExpectedResult = true)]
        [TestCase("get_disabled_tools",ExpectedResult = true)]
        [TestCase("set_tool_catalog",  ExpectedResult = true)]
        public bool IsAlwaysAllowed_KnownBypass_ReturnsTrue(string cmd)
            => CommandRouter.IsAlwaysAllowed(cmd);

        [TestCase("get_hierarchy")]
        [TestCase("set_property")]
        [TestCase("create_object")]
        public void IsAlwaysAllowed_NormalCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRouter.IsAlwaysAllowed(cmd));

        // ── SuggestNext ───────────────────────────────────────────────────────

        [TestCase("set_property",    "get_console level=Error")]
        [TestCase("create_object",   "get_hierarchy depth=1")]
        [TestCase("wire_event",      "validate_references")]
        [TestCase("unwire_event",    "get_component")]
        [TestCase("manage_component","get_components_list")]
        [TestCase("delete_object",   "get_hierarchy depth=1")]
        [TestCase("set_parent",      "get_hierarchy depth=1")]
        [TestCase("batch",           "get_console level=Error")]
        public void SuggestNext_MutatingCommand_ReturnsSuggestion(string cmd, string expected)
            => Assert.AreEqual(expected, CommandRouter.SuggestNext(cmd));

        [TestCase("ping")]
        [TestCase("get_hierarchy")]
        [TestCase("get_component")]
        [TestCase("unknown_cmd")]
        public void SuggestNext_ReadCommand_ReturnsNull(string cmd)
            => Assert.IsNull(CommandRouter.SuggestNext(cmd));

        // ── CommandRegistry flags ─────────────────────────────────────────────

        [TestCase("create_object",  ExpectedResult = true)]
        [TestCase("delete_object",  ExpectedResult = true)]
        [TestCase("set_property",   ExpectedResult = true)]
        [TestCase("manage_component", ExpectedResult = true)]
        [TestCase("wire_event",     ExpectedResult = true)]
        [TestCase("set_active",     ExpectedResult = true)]
        public bool Registry_IsMutating_MutatingCommands(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("ping",          ExpectedResult = false)]
        [TestCase("get_hierarchy", ExpectedResult = false)]
        [TestCase("get_component", ExpectedResult = false)]
        [TestCase("get_console",   ExpectedResult = false)]
        public bool Registry_IsMutating_ReadCommands_ReturnFalse(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("invoke_method",        ExpectedResult = true)]
        [TestCase("set_runtime_property", ExpectedResult = true)]
        [TestCase("wait_until",           ExpectedResult = true)]
        [TestCase("move_to",              ExpectedResult = true)]
        [TestCase("query_state",          ExpectedResult = true)]
        [TestCase("run_playtest",         ExpectedResult = true)]
        public bool Registry_IsRuntime_RuntimeCommands(string cmd)
            => CommandRegistry.IsRuntime(cmd);

        [TestCase("ping")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        public void Registry_IsRuntime_NonRuntimeCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRegistry.IsRuntime(cmd));

        // ── CommandRegistry.IsRegistered ─────────────────────────────────────

        [TestCase("ping")]
        [TestCase("get_hierarchy")]
        [TestCase("set_property")]
        [TestCase("batch")]
        [TestCase("execute_code")]
        [TestCase("run_tests")]
        [TestCase("screenshot")]
        public void Registry_IsRegistered_KnownCommands_ReturnsTrue(string cmd)
            => Assert.IsTrue(CommandRegistry.IsRegistered(cmd));

        [Test]
        public void Registry_IsRegistered_UnknownCommand_ReturnsFalse()
            => Assert.IsFalse(CommandRegistry.IsRegistered("totally_unknown_xyz"));

        // ── Process: compiling guard blocks non-allowed commands ──────────────

        [Test]
        public void Process_WhileCompiling_BlockedCommand_ReturnsRetryResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("retry"), result);
            }
            finally { CommandRouter.IsCompiling = () => UnityEditor.EditorApplication.isCompiling; }
        }

        [Test]
        public void Process_WhileCompiling_PingAllowed_ReturnsPong()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"2\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                Assert.IsTrue(result.Contains("pong"), result);
            }
            finally { CommandRouter.IsCompiling = () => UnityEditor.EditorApplication.isCompiling; }
        }

        // ── Process: play-mode guard blocks mutating commands ─────────────────

        [Test]
        public void Process_InPlayMode_MutatingCommand_ReturnsError()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => true;
            try
            {
                var json = "{\"id\":\"3\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("Play mode"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = () => UnityEditor.EditorApplication.isCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Process: runtime guard blocks runtime-only commands outside play ──

        [Test]
        public void Process_OutsidePlayMode_RuntimeCommand_ReturnsError()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var json = "{\"id\":\"4\",\"cmd\":\"invoke_method\",\"args\":{\"path\":\"/X\",\"component\":\"C\",\"method\":\"M\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("Play Mode"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = () => UnityEditor.EditorApplication.isCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── BuildResponse (via ping): short data stays inline ─────────────────

        [Test]
        public void Process_Ping_ShortData_InlineResponse()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var json = "{\"id\":\"5\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                // Short response: no file field, data inline
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                Assert.IsFalse(result.Contains("\"file\""), result);
                Assert.IsTrue(result.Contains("pong"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = () => UnityEditor.EditorApplication.isCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── CommandSchema: all registered commands have schema entries ────────

        [Test]
        public void Schema_AllRegisteredCommands_HaveSchemaOrPassValidation()
        {
            // Every registered command must either be in schema or pass null-args validation
            // (not return "Unknown command"). Plugin-registered commands are exempt.
            var failures = new System.Collections.Generic.List<string>();
            foreach (var cmd in CommandRegistry.GetAllCommands())
            {
                var err = CommandSchema.Validate(cmd, "{}");
                // "Unknown command" means schema gap; missing-required is acceptable
                if (err != null && err.StartsWith("Unknown command"))
                    failures.Add(cmd + ": " + err);
            }
            Assert.IsEmpty(failures, "Commands missing schema: " + string.Join(", ", failures));
        }

        // ── CommandSchema.ExtractKeys ─────────────────────────────────────────

        [Test]
        public void ExtractKeys_EmptyJson_ReturnsEmpty()
            => Assert.IsEmpty(CommandSchema.ExtractKeys("{}"));

        [Test]
        public void ExtractKeys_SingleKey_ReturnsThatKey()
        {
            var keys = CommandSchema.ExtractKeys("{\"path\":\"/Obj\"}");
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual("path", keys[0]);
        }

        [Test]
        public void ExtractKeys_MultipleKeys_ReturnsAll()
        {
            var keys = CommandSchema.ExtractKeys("{\"path\":\"/Obj\",\"component\":\"Transform\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.Contains("path", keys);
            Assert.Contains("component", keys);
        }

        [Test]
        public void ExtractKeys_NullJson_ReturnsEmpty()
            => Assert.IsEmpty(CommandSchema.ExtractKeys(null));

        // ── Step 2: sync + sync_status commands (#11, #12) ───────────────────

        // #11: sync and sync_status are registered
        [TestCase("sync",        ExpectedResult = true)]
        [TestCase("sync_status", ExpectedResult = true)]
        public bool Sync_Commands_Registered(string cmd)
            => CommandRegistry.IsRegistered(cmd);

        // #12: sync_status is allowed during compile
        [TestCase("sync_status", ExpectedResult = true)]
        public bool SyncStatus_Allowed_During_Compile(string cmd)
            => CommandRouter.IsAllowedDuringCompile(cmd);
    }
}
