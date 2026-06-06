using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ChatHeaderAnimTests
    {
        private VisualElement _root;

        [SetUp]
        public void SetUp() => _root = ChatHeaderAnim.Build(new VisualElement());

        [Test]
        public void Build_RootHasWaveRootClass() =>
            Assert.IsTrue(_root.ClassListContains("wave-root"));

        [Test]
        public void Build_RootHasThreeChildren() =>
            Assert.AreEqual(3, _root.childCount);

        [Test]
        public void Build_LineLHasWaveLineClass() =>
            Assert.IsTrue(_root.ElementAt(0).ClassListContains("wave-line"));

        [Test]
        public void Build_LineRHasWaveLineClass() =>
            Assert.IsTrue(_root.ElementAt(2).ClassListContains("wave-line"));

        [Test]
        public void Build_HubHasFourChildren() =>
            Assert.AreEqual(4, _root.ElementAt(1).childCount);

        [Test]
        public void Build_HubArcsHaveWaveArcClass()
        {
            var hub = _root.ElementAt(1);
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(hub.ElementAt(i).ClassListContains("wave-arc"));
        }

        [Test]
        public void Build_HubLastChildHasWaveDotClass() =>
            Assert.IsTrue(_root.ElementAt(1).ElementAt(3).ClassListContains("wave-dot"));
    }
}
