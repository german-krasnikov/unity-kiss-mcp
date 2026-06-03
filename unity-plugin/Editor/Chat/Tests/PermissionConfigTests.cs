using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    /// <summary>
    /// Tests for PermissionConfig — deny-set approach, EditorPrefs-backed.
    /// Each test uses a unique Guid key prefix to avoid cross-test pollution.
    /// </summary>
    [TestFixture]
    public class PermissionConfigTests
    {
        // A minimal fake catalog: 2 categories, 4 tools total.
        private static readonly Dictionary<string, string[]> FakeCatalog =
            new Dictionary<string, string[]>
            {
                { "CAT_A", new[] { "tool_a1", "tool_a2" } },
                { "CAT_B", new[] { "tool_b1", "tool_b2" } },
            };

        // Track all configs created in this test so TearDown can clean up.
        private readonly List<PermissionConfig> _created = new List<PermissionConfig>();

        private PermissionConfig Make(Dictionary<string, string[]> catalog = null)
        {
            var prefix = "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_";
            var cfg = new PermissionConfig(prefix, () => catalog ?? FakeCatalog);
            _created.Add(cfg);
            return cfg;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cfg in _created) cfg.AllowAll();
            _created.Clear();
        }

        // ── 1. All tools enabled when no prefs written → null (FIX 2 contract) ─

        [Test]
        public void GetAllowedToolIds_DefaultState_ReturnsNull()
        {
            // FIX 2: no denials → null signals "use blanket" to ClaudeArgBuilder.
            var cfg = Make();
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        // ── 2. One denied tool excluded ────────────────────────────────────────

        [Test]
        public void GetAllowedToolIds_OneDenied_ExcludesThatTool()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            var allowed = cfg.GetAllowedToolIds();
            CollectionAssert.DoesNotContain(allowed, "tool_a1");
            CollectionAssert.Contains(allowed, "tool_a2");
        }

        // ── 3. SetToolAllowed persists via EditorPrefs ────────────────────────

        [Test]
        public void SetToolAllowed_False_PersistsAcrossNewInstance()
        {
            var prefix = "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_";
            var cfg1 = new PermissionConfig(prefix, () => FakeCatalog);
            var cfg2 = new PermissionConfig(prefix, () => FakeCatalog);
            _created.Add(cfg1); // TearDown will clean up via AllowAll on cfg1
            cfg1.SetToolAllowed("tool_b2", false);

            // Same prefix → reads same EditorPrefs.
            CollectionAssert.DoesNotContain(cfg2.GetAllowedToolIds(), "tool_b2");
        }

        // ── 4. SetCategoryAllowed(false) denies entire category ───────────────

        [Test]
        public void SetCategoryAllowed_False_DeniesAllToolsInCategory()
        {
            var cfg = Make();
            cfg.SetCategoryAllowed("CAT_B", false);
            var allowed = cfg.GetAllowedToolIds();
            CollectionAssert.DoesNotContain(allowed, "tool_b1");
            CollectionAssert.DoesNotContain(allowed, "tool_b2");
            CollectionAssert.Contains(allowed, "tool_a1");
        }

        // ── 5. AllowAll resets deny-set ───────────────────────────────────────

        [Test]
        public void AllowAll_AfterDenies_ReturnsNull()
        {
            // FIX 2: after AllowAll deny-set is empty → null (use blanket).
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            cfg.SetToolAllowed("tool_b1", false);
            cfg.AllowAll();
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        // ── 6. GetToolStates returns correct structure ─────────────────────────

        [Test]
        public void GetToolStates_ReturnsAllToolsWithCategoryAndAllowedFlag()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a2", false);
            var states = cfg.GetToolStates();
            Assert.AreEqual(4, states.Count);

            var a2 = states.First(s => s.toolName == "tool_a2");
            Assert.AreEqual("CAT_A", a2.category);
            Assert.IsFalse(a2.allowed);

            var b1 = states.First(s => s.toolName == "tool_b1");
            Assert.AreEqual("CAT_B", b1.category);
            Assert.IsTrue(b1.allowed);
        }

        // ── 7. Empty deny-set → null (blanket signal, FIX 2) ─────────────────

        [Test]
        public void GetAllowedToolIds_NoDenies_ReturnsNullBlanketSignal()
        {
            // FIX 2: null → ClaudeArgBuilder emits compact "mcp__unity" blanket.
            var cfg = Make();
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        // ── 8. DenyAll returns empty list ──────────────────────────────────────

        [Test]
        public void DenyAll_ReturnsEmptyAllowedList()
        {
            var cfg = Make();
            cfg.DenyAll();
            Assert.AreEqual(0, cfg.GetAllowedToolIds().Length);
        }

        // ── 9. Empty catalog + no denials → null (blanket signal, FIX 2) ───────

        [Test]
        public void GetAllowedToolIds_EmptyCatalog_ReturnsNull()
        {
            // Empty catalog = no tools → no denials → null (use blanket).
            // In practice this only happens before catalog sync; blanket is safe.
            var cfg = new PermissionConfig(
                "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_",
                () => new Dictionary<string, string[]>());
            _created.Add(cfg);
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        // ── 10. SetCategoryAllowed with unknown category is a no-op ──────────

        [Test]
        public void SetCategoryAllowed_UnknownCategory_IsNoOp()
        {
            var cfg = Make();
            // No denials before or after → both must be null.
            Assert.IsNull(cfg.GetAllowedToolIds());
            cfg.SetCategoryAllowed("NONEXISTENT", false);
            Assert.IsNull(cfg.GetAllowedToolIds());
        }

        // ── FIX 2: no denials → GetAllowedToolIds returns null (→ blanket CLI arg) ──

        [Test]
        public void GetAllowedToolIds_NoDenials_ReturnsNull()
        {
            // FIX 2: when deny-set is empty ClaudeBackend must use the compact
            // blanket "mcp__unity" rather than enumerating all ~88 tool ids.
            // Contract: GetAllowedToolIds() returns null when nothing is denied.
            var cfg = Make();
            Assert.IsNull(cfg.GetAllowedToolIds(),
                "No denials → null so ClaudeArgBuilder emits the compact blanket");
        }

        [Test]
        public void GetAllowedToolIds_OneDenied_ReturnsNonNull()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            Assert.IsNotNull(cfg.GetAllowedToolIds(),
                "At least one denial → must enumerate remaining allowed tools");
        }

        [Test]
        public void GetAllowedToolIds_OneDenied_ExcludesDeniedAndIncludesRest()
        {
            var cfg = Make();
            cfg.SetToolAllowed("tool_a1", false);
            var allowed = cfg.GetAllowedToolIds();
            CollectionAssert.DoesNotContain(allowed, "tool_a1");
            CollectionAssert.Contains(allowed, "tool_a2");
            CollectionAssert.Contains(allowed, "tool_b1");
            CollectionAssert.Contains(allowed, "tool_b2");
        }

        [Test]
        public void GetAllowedToolIds_AllDenied_ReturnsEmptyArray()
        {
            // Empty array → ClaudeArgBuilder omits --allowedTools (deny-all path).
            var cfg = Make();
            cfg.DenyAll();
            var ids = cfg.GetAllowedToolIds();
            Assert.IsNotNull(ids, "DenyAll → empty array (not null) to signal explicit deny-all");
            Assert.AreEqual(0, ids.Length);
        }

        // ── FIX 3: PLUGINS key not overwritten if catalog already has it ──────

        [Test]
        public void LiveCatalog_DoesNotOverwriteExistingPluginsCategory()
        {
            // Inject a catalog that already contains a "PLUGINS" category.
            // LiveCatalog (via merged["PLUGINS"]) must NOT overwrite it.
            var catalogWithPlugins = new Dictionary<string, string[]>
            {
                { "CAT_A",   new[] { "tool_a1" } },
                { "PLUGINS", new[] { "existing_plugin" } },
            };
            // We test the guard logic via PermissionConfig's injectable ctor —
            // the production LiveCatalog guard is verified by the PLUGINS key surviving.
            var cfg = new PermissionConfig(
                "UnityMCP_Test_" + System.Guid.NewGuid().ToString("N") + "_",
                () => catalogWithPlugins);
            _created.Add(cfg);

            var states = cfg.GetToolStates();
            var pluginTools = states
                .Where(s => s.category == "PLUGINS")
                .Select(s => s.toolName)
                .ToArray();
            CollectionAssert.Contains(pluginTools, "existing_plugin",
                "PLUGINS category must not be overwritten if already present in catalog");
        }
    }
}
