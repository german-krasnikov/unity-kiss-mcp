// Tests for PreviewBuilderRegistry priority / resolve / unregister.
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PreviewBuilderRegistryTests
    {
        sealed class DummyBuilder : IPreviewBuilder
        {
            public string Kind;
            public VisualElement Result;
            public bool CanBuild(string kindKey, string path) => kindKey == Kind;
            public VisualElement Build(PreviewRequest request, IPreviewContext context) => Result;
        }

        [SetUp]
        [TearDown]
        public void Reset() => PreviewBuilderRegistry.Reset();

        [Test]
        public void Resolve_NoBuilder_ReturnsNull()
        {
            Assert.IsNull(PreviewBuilderRegistry.Resolve("anything", "path"));
        }

        [Test]
        public void Resolve_RegisteredBuilder_ReturnsBuilder()
        {
            var builder = new DummyBuilder { Kind = "image" };
            PreviewBuilderRegistry.Register(builder);
            Assert.AreSame(builder, PreviewBuilderRegistry.Resolve("image", "x.png"));
        }

        [Test]
        public void Resolve_LowerPriorityWins()
        {
            var low = new DummyBuilder { Kind = "asset" };
            var high = new DummyBuilder { Kind = "asset" };
            PreviewBuilderRegistry.Register(high, priority: 100);
            PreviewBuilderRegistry.Register(low, priority: 10);
            Assert.AreSame(low, PreviewBuilderRegistry.Resolve("asset", "x"));
        }

        [Test]
        public void Unregister_RemovesBuilder()
        {
            var builder = new DummyBuilder { Kind = "audio" };
            PreviewBuilderRegistry.Register(builder);
            Assert.IsTrue(PreviewBuilderRegistry.Unregister(builder));
            Assert.IsNull(PreviewBuilderRegistry.Resolve("audio", "x.wav"));
        }

        [Test]
        public void Build_NoBuilder_ReturnsNull()
        {
            var ctx = new TestPreviewContext();
            Assert.IsNull(PreviewBuilderRegistry.Build(new PreviewRequest("x", "y"), ctx));
        }

        sealed class TestPreviewContext : IPreviewContext
        {
            public IAssetPreviewService PreviewService => null;
            public IChipExistenceService ExistenceService => null;
            public System.Threading.CancellationToken CancellationToken => default;
        }
    }
}
