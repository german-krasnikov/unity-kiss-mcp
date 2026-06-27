using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginConfigTests
    {
        private const string Id1 = "TestPlugin_Alpha";
        private const string Id2 = "TestPlugin_Beta";
        private const string Key = "test_key";

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(PluginConfig.BuildKey(Id1, Key));
            EditorPrefs.DeleteKey(PluginConfig.BuildKey(Id2, Key));
        }

        [Test]
        public void SetString_ThenGet_RoundTrips()
        {
            PluginConfig.SetString(Id1, Key, "hello");
            Assert.AreEqual("hello", PluginConfig.GetString(Id1, Key));
        }

        [Test]
        public void GetString_MissingKey_ReturnsDefault()
            => Assert.AreEqual("def", PluginConfig.GetString(Id1, Key, "def"));

        [Test]
        public void SetBool_ThenGet_RoundTrips()
        {
            PluginConfig.SetBool(Id1, Key, false);
            Assert.IsFalse(PluginConfig.GetBool(Id1, Key, defaultValue: true));
        }

        [Test]
        public void SetInt_ThenGet_RoundTrips()
        {
            PluginConfig.SetInt(Id1, Key, 42);
            Assert.AreEqual(42, PluginConfig.GetInt(Id1, Key));
        }

        [Test]
        public void SetFloat_ThenGet_RoundTrips()
        {
            PluginConfig.SetFloat(Id1, Key, 3.14f);
            Assert.AreEqual(3.14f, PluginConfig.GetFloat(Id1, Key), delta: 0.001f);
        }

        [Test]
        public void Delete_AfterSet_ReturnsDefault()
        {
            PluginConfig.SetString(Id1, Key, "val");
            PluginConfig.Delete(Id1, Key);
            Assert.AreEqual("def", PluginConfig.GetString(Id1, Key, "def"));
        }

        [Test]
        public void TwoPlugins_SameKey_StoredSeparately()
        {
            PluginConfig.SetString(Id1, Key, "alpha_value");
            PluginConfig.SetString(Id2, Key, "beta_value");
            Assert.AreEqual("alpha_value", PluginConfig.GetString(Id1, Key));
            Assert.AreEqual("beta_value", PluginConfig.GetString(Id2, Key));
        }

        [Test]
        public void BuildKey_ContainsPluginIdAndKey()
        {
            var k = PluginConfig.BuildKey("MyPlugin", "my_key");
            StringAssert.Contains("MyPlugin", k);
            StringAssert.Contains("my_key", k);
        }

        [Test]
        public void BuildKey_PrefixNotCollidingWithMCPSettings()
        {
            var k = PluginConfig.BuildKey("any", "any");
            StringAssert.DoesNotStartWith("UnityMCP_", k);
        }
    }
}
