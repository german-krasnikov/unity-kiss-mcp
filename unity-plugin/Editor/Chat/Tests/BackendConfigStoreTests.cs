// TDD tests for BackendConfigStore. Pure unit — no Unity API, no I/O beyond temp files.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class BackendConfigStoreTests
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(Path.GetTempPath(), $"BackendConfigTest_{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void BackendConfigStore_Load_DefaultsWhenFileAbsent()
        {
            var store = BackendConfigStore.Load(_tempPath);

            Assert.IsNotNull(store);
            Assert.IsNotNull(store.Claude);
            Assert.IsNotNull(store.Codex);
            Assert.AreEqual("plan",              store.Claude.PermissionMode);
            Assert.AreEqual("",                  store.Claude.Model);
            Assert.AreEqual("",                  store.Claude.ExtraArgs);
            Assert.AreEqual("danger-full-access", store.Codex.PermissionMode);
            Assert.AreEqual(30,                  store.Codex.StartupTimeoutSec);
            Assert.AreEqual("",                  store.Codex.Model);
            Assert.AreEqual("",                  store.Codex.ExtraArgs);
        }

        [Test]
        public void BackendConfigStore_SaveLoad_RoundTrip()
        {
            var original = new BackendConfigStore
            {
                Claude = new ClaudeBackendConfig { Model = "claude-opus-4", PermissionMode = "acceptEdits", ExtraArgs = "--debug" },
                Codex  = new CodexBackendConfig  { Model = "o3", StartupTimeoutSec = 60, ExtraArgs = "--verbose" }
            };

            original.Save(_tempPath);
            var loaded = BackendConfigStore.Load(_tempPath);

            Assert.AreEqual("claude-opus-4",  loaded.Claude.Model);
            Assert.AreEqual("acceptEdits",     loaded.Claude.PermissionMode);
            Assert.AreEqual("--debug",         loaded.Claude.ExtraArgs);
            Assert.AreEqual("o3",              loaded.Codex.Model);
            Assert.AreEqual(60,                loaded.Codex.StartupTimeoutSec);
            Assert.AreEqual("--verbose",       loaded.Codex.ExtraArgs);
        }

        // ── F10: ChipConfig ───────────────────────────────────────────────────

        [Test]
        public void ChipConfig_DefaultsOnLoad()
        {
            // Load from missing file → defaults apply
            var store = BackendConfigStore.Load(_tempPath);
            Assert.IsNotNull(store.Chips);
            Assert.AreEqual("summary", store.Chips.HierarchyDepth);
            Assert.AreEqual("path",    store.Chips.ScriptDepth);
            Assert.AreEqual("path",    store.Chips.SceneDepth);
            Assert.AreEqual("path",    store.Chips.PrefabDepth);
            Assert.AreEqual("path",    store.Chips.AssetDepth);
        }

        [Test]
        public void ChipConfig_RoundTrip_CustomDepths()
        {
            var original = new BackendConfigStore
            {
                Chips = new ChipConfig
                {
                    HierarchyDepth = "full",
                    ScriptDepth    = "none",
                    SceneDepth     = "summary",
                    PrefabDepth    = "path",
                    AssetDepth     = "none",
                }
            };

            original.Save(_tempPath);
            var loaded = BackendConfigStore.Load(_tempPath);

            Assert.AreEqual("full",    loaded.Chips.HierarchyDepth);
            Assert.AreEqual("none",    loaded.Chips.ScriptDepth);
            Assert.AreEqual("summary", loaded.Chips.SceneDepth);
            Assert.AreEqual("path",    loaded.Chips.PrefabDepth);
            Assert.AreEqual("none",    loaded.Chips.AssetDepth);
        }

        [Test]
        public void ChipConfig_MissingField_DefaultsFallback()
        {
            // Write a JSON without the "Chips" field — simulates old file format.
            // JsonUtility should leave Chips as default-constructed (not null).
            var oldJson = "{\"Claude\":{\"PermissionMode\":\"plan\",\"Model\":\"\",\"ExtraArgs\":\"\"},\"Codex\":{\"Model\":\"\",\"PermissionMode\":\"danger-full-access\",\"StartupTimeoutSec\":30,\"ExtraArgs\":\"\"}}";
            System.IO.File.WriteAllText(_tempPath, oldJson);

            var loaded = BackendConfigStore.Load(_tempPath);

            // Chips must be non-null and have defaults
            Assert.IsNotNull(loaded.Chips);
            Assert.AreEqual("summary", loaded.Chips.HierarchyDepth);
            Assert.AreEqual("path",    loaded.Chips.ScriptDepth);
        }

        // ── F10: ChipConfig.DepthFor ──────────────────────────────────────────

        [Test]
        public void DepthFor_Hierarchy_ReturnsHierarchyDepth()
        {
            var cfg = new ChipConfig { HierarchyDepth = "full" };
            Assert.AreEqual("full", cfg.DepthFor(ChipKind.Hierarchy));
        }

        [Test]
        public void DepthFor_Script_ReturnsScriptDepth()
        {
            var cfg = new ChipConfig { ScriptDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKind.Script));
        }

        [Test]
        public void DepthFor_Scene_ReturnsSceneDepth()
        {
            var cfg = new ChipConfig { SceneDepth = "summary" };
            Assert.AreEqual("summary", cfg.DepthFor(ChipKind.Scene));
        }

        [Test]
        public void DepthFor_Prefab_ReturnsPrefabDepth()
        {
            var cfg = new ChipConfig { PrefabDepth = "path" };
            Assert.AreEqual("path", cfg.DepthFor(ChipKind.Prefab));
        }

        [Test]
        public void DepthFor_Material_ReturnsAssetDepth()
        {
            // Material/Texture/SO/Asset → all fall through to AssetDepth
            var cfg = new ChipConfig { AssetDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKind.Material));
        }

        [Test]
        public void DepthFor_Texture_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "summary" };
            Assert.AreEqual("summary", cfg.DepthFor(ChipKind.Texture));
        }

        [Test]
        public void DepthFor_ScriptableObject_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "path" };
            Assert.AreEqual("path", cfg.DepthFor(ChipKind.ScriptableObject));
        }

        [Test]
        public void DepthFor_Asset_ReturnsAssetDepth()
        {
            var cfg = new ChipConfig { AssetDepth = "none" };
            Assert.AreEqual("none", cfg.DepthFor(ChipKind.Asset));
        }

        [Test]
        public void DepthFor_Default_HierarchyIsSummary()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("summary", cfg.DepthFor(ChipKind.Hierarchy));
        }

        [Test]
        public void DepthFor_Default_ScriptIsPath()
        {
            var cfg = new ChipConfig();
            Assert.AreEqual("path", cfg.DepthFor(ChipKind.Script));
        }
    }
}
