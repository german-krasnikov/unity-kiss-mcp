using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.arch.2 / CS5.test.2 — PluginRegistry ────────────────────────────

    [TestFixture]
    public class PluginRegistryTests
    {
        private class FakePlugin : IMCPPlugin
        {
            public string Name { get; }
            public string CommandPrefix { get; }
            public int RegisterCommandsCallCount;
            public int OnDomainReloadCallCount;
            public bool OnDomainReloadThrows;

            public FakePlugin(string name, string prefix = "fake")
            {
                Name = name;
                CommandPrefix = prefix;
            }

            public void RegisterCommands() => RegisterCommandsCallCount++;

            public void OnDomainReload()
            {
                OnDomainReloadCallCount++;
                if (OnDomainReloadThrows) throw new InvalidOperationException("simulated");
            }

            public IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
        }

        [TearDown]
        public void TearDown() => PluginRegistry.Clear();

        [Test]
        public void Register_DuplicateName_OnlyOneRegistered()
        {
            var p1 = new FakePlugin("MyPlugin");
            var p2 = new FakePlugin("MyPlugin");

            PluginRegistry.Register(p1);
            PluginRegistry.Register(p2);

            Assert.AreEqual(1, PluginRegistry.GetAll().Count);
        }

        [Test]
        public void Register_ThenRegisterAllPlugins_RegisterCommandsCalledOnce()
        {
            // Register() must NOT call RegisterCommands();
            // only RegisterAllPlugins() should call it — exactly once.
            var plugin = new FakePlugin("TestPlugin");

            PluginRegistry.Register(plugin);
            PluginRegistry.RegisterAllPlugins();

            Assert.AreEqual(1, plugin.RegisterCommandsCallCount,
                "RegisterCommands should be called exactly once (by RegisterAllPlugins only)");
        }

        [Test]
        public void Register_DoesNotCallRegisterCommands()
        {
            var plugin = new FakePlugin("TestPlugin2");

            PluginRegistry.Register(plugin);

            Assert.AreEqual(0, plugin.RegisterCommandsCallCount,
                "Register() must not call RegisterCommands() — that is RegisterAllPlugins() responsibility");
        }

        [Test]
        public void RegisterAllPlugins_MultipleCalls_EachCallRegistersOnce()
        {
            var plugin = new FakePlugin("TestPlugin3");
            PluginRegistry.Register(plugin);

            PluginRegistry.RegisterAllPlugins();
            PluginRegistry.RegisterAllPlugins();

            Assert.AreEqual(2, plugin.RegisterCommandsCallCount,
                "Each RegisterAllPlugins() call invokes RegisterCommands() once per plugin");
        }

        [Test]
        public void OnDomainReload_ExceptionInPlugin_DoesNotPropagate()
        {
            var plugin = new FakePlugin("BadPlugin") { OnDomainReloadThrows = true };
            PluginRegistry.Register(plugin);

            // OnDomainReload swallows the exception but logs it as LogError — expect it
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("BadPlugin.*OnDomainReload failed"));

            Assert.DoesNotThrow(() => PluginRegistry.OnDomainReload(),
                "OnDomainReload must swallow plugin exceptions");
        }

        [Test]
        public void IsPluginCommand_MatchesPrefix()
        {
            var plugin = new FakePlugin("PrefixPlugin", "myplugin");
            PluginRegistry.Register(plugin);

            Assert.IsTrue(PluginRegistry.IsPluginCommand("myplugin"));
            Assert.IsTrue(PluginRegistry.IsPluginCommand("myplugin_action"));
            Assert.IsFalse(PluginRegistry.IsPluginCommand("other_command"));
        }
    }
}
