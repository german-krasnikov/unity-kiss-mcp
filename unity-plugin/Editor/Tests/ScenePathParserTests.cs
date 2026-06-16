using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScenePathParserTests
    {
        [Test]
        public void Parse_WithScene_ReturnsNameAndPath()
        {
            var result = ScenePathParser.Parse("GameplayScene:/Player/Weapon");
            Assert.AreEqual("GameplayScene", result.SceneName);
            Assert.AreEqual("Player/Weapon", result.LocalPath);
        }

        [Test]
        public void Parse_WithoutScene_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse("/Player/Weapon");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("/Player/Weapon", result.LocalPath);
        }

        [Test]
        public void Parse_ColonAtStart_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse(":/SomeObj");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual(":/SomeObj", result.LocalPath);
        }

        [Test]
        public void Parse_Null_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse(null);
            Assert.IsNull(result.SceneName);
            Assert.IsNull(result.LocalPath);
        }

        [Test]
        public void Parse_Empty_ReturnsNullScene()
        {
            var result = ScenePathParser.Parse("");
            Assert.IsNull(result.SceneName);
            Assert.AreEqual("", result.LocalPath);
        }

        [Test]
        public void Parse_LeadingSlashStripped()
        {
            var result = ScenePathParser.Parse("Scene://Root/Child");
            Assert.AreEqual("Scene", result.SceneName);
            Assert.AreEqual("Root/Child", result.LocalPath);
        }

        [Test]
        public void Parse_NoLeadingSlash()
        {
            var result = ScenePathParser.Parse("Scene:/Root");
            Assert.AreEqual("Scene", result.SceneName);
            Assert.AreEqual("Root", result.LocalPath);
        }
    }
}
