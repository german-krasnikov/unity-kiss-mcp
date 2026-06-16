// TDD — Issues #5 + #7: Multi-scene chip paths & navigation.
// IsAssetPath tests use reflection (private static).
// SceneObjectFinder.FindGameObject null/empty path guards are pure logic.
using NUnit.Framework;
using System.Reflection;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MultiSceneChipTests
    {
        // ── IsAssetPath helpers ──────────────────────────────────────────────

        private static readonly MethodInfo _isAssetPath =
            typeof(ChipContextResolver).GetMethod("IsAssetPath",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static bool IsAssetPath(string path)
            => (bool)_isAssetPath.Invoke(null, new object[] { path });

        // ── IsAssetPath — unit (no Unity scene needed) ───────────────────────

        [Test]
        public void IsAssetPath_SceneQualified_ReturnsFalse()
            => Assert.IsFalse(IsAssetPath("GameplayScene:/Player"));

        [Test]
        public void IsAssetPath_UnqualifiedScenePath_ReturnsFalse()
            => Assert.IsFalse(IsAssetPath("/Player"));

        [Test]
        public void IsAssetPath_AssetPath_ReturnsTrue()
            => Assert.IsTrue(IsAssetPath("Assets/Foo.cs"));

        [Test]
        public void IsAssetPath_PackagePath_ReturnsTrue()
            => Assert.IsTrue(IsAssetPath("Packages/com.x/Y.cs"));

        [Test]
        public void IsAssetPath_Null_ReturnsFalse()
            => Assert.IsFalse(IsAssetPath(null));

        [Test]
        public void IsAssetPath_Empty_ReturnsFalse()
            => Assert.IsFalse(IsAssetPath(""));

        [Test]
        public void IsAssetPath_DeepSceneQualified_ReturnsFalse()
            => Assert.IsFalse(IsAssetPath("MainScene:/Root/Child/Leaf"));

        // ── SceneObjectFinder null/empty guards — pure logic ─────────────────

        [Test]
        public void FindGameObject_Null_ReturnsNull()
            => Assert.IsNull(SceneObjectFinder.FindGameObject(null));

        [Test]
        public void FindGameObject_Empty_ReturnsNull()
            => Assert.IsNull(SceneObjectFinder.FindGameObject(""));

        // ── HierarchyChipProvider display ────────────────────────────────────

        [Test]
        public void DisplayFormat_MultiScene_ProducesSceneBadge()
            => Assert.AreEqual("[GameplayScene] Player",
                HierarchyChipProvider.FormatHierarchyDisplay("GameplayScene:/Player", "Player"));

        [Test]
        public void DisplayFormat_SingleScene_LeafOnly()
            => Assert.AreEqual("Player",
                HierarchyChipProvider.FormatHierarchyDisplay("/Player", "Player"));
    }
}
