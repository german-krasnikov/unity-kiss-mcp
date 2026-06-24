using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
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
        public void TearDown()
        {
            PluginRegistry.Clear();
            // Restore built-in commands if any test called CommandRegistry.Clear()
            CommandRegistry.InitDefaults();
        }

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

        // ── GetCommandsForPlugin ─────────────────────────────────────────────

        [Test]
        public void GetCommandsForPlugin_ReturnsOnlyPluginCommands()
        {
            CommandRegistry.Clear();
            var plugin = new FakePlugin("MyPlugin", "myplugin");
            CommandRegistry.Register("myplugin", _ => "ok");
            CommandRegistry.Register("myplugin_action", _ => "ok");
            CommandRegistry.Register("other", _ => "ok");
            PluginRegistry.Register(plugin);

            var result = PluginRegistry.GetCommandsForPlugin(plugin);

            CollectionAssert.Contains(result, "myplugin");
            CollectionAssert.Contains(result, "myplugin_action");
            CollectionAssert.DoesNotContain(result, "other");
        }

        [Test]
        public void GetCommandsForPlugin_IsolatesPlugins()
        {
            CommandRegistry.Clear();
            var plugin1 = new FakePlugin("Plugin1", "p1");
            var plugin2 = new FakePlugin("Plugin2", "p2");
            CommandRegistry.Register("p1_cmd", _ => "ok");
            CommandRegistry.Register("p2_cmd", _ => "ok");
            PluginRegistry.Register(plugin1);
            PluginRegistry.Register(plugin2);

            var result1 = PluginRegistry.GetCommandsForPlugin(plugin1);
            var result2 = PluginRegistry.GetCommandsForPlugin(plugin2);

            CollectionAssert.Contains(result1, "p1_cmd");
            CollectionAssert.DoesNotContain(result1, "p2_cmd");
            CollectionAssert.Contains(result2, "p2_cmd");
            CollectionAssert.DoesNotContain(result2, "p1_cmd");
        }

        [Test]
        public void GetCommandsForPlugin_IncludesAdditionalCommands()
        {
            CommandRegistry.Clear();
            var plugin = new FakePluginWithExtra("ExtraPlugin", "extra");
            CommandRegistry.Register("extra_base", _ => "ok");
            CommandRegistry.Register("extra_cmd", _ => "ok");
            PluginRegistry.Register(plugin);

            var result = PluginRegistry.GetCommandsForPlugin(plugin);

            CollectionAssert.Contains(result, "extra_cmd");
        }

        private class FakePluginWithExtra : IMCPPlugin
        {
            public string Name { get; }
            public string CommandPrefix { get; }
            public FakePluginWithExtra(string name, string prefix) { Name = name; CommandPrefix = prefix; }
            public void RegisterCommands() { }
            public void OnDomainReload() { }
            public IReadOnlyList<string> AdditionalCommands => new[] { "extra_cmd" };
        }
    }
}
