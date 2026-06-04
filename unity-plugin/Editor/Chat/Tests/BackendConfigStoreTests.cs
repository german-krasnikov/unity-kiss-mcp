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
    }
}
