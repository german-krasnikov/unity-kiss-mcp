// TDD tests for ChipKindDetector — pure unit tests using EditMode Unity objects.
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipKindDetectorTests
    {
        // ── Scene GameObject ──────────────────────────────────────────────────

        [Test]
        public void Detect_SceneGameObject_ReturnsHierarchy()
        {
            var go = new GameObject("TestGO");
            try
            {
                // Scene GO: not in AssetDatabase
                var kind = ChipKindDetector.Detect(go, null);
                Assert.AreEqual(ChipKind.Hierarchy, kind);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ── Script asset ──────────────────────────────────────────────────────

        [Test]
        public void Detect_MonoScript_ReturnsScript()
        {
            // Find any MonoScript in the project
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            if (guids.Length == 0) Assert.Ignore("No MonoScript assets found");
            var path   = AssetDatabase.GUIDToAssetPath(guids[0]);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.IsNotNull(script);

            var kind = ChipKindDetector.Detect(script, path);
            Assert.AreEqual(ChipKind.Script, kind);
        }

        // ── Scene asset ───────────────────────────────────────────────────────

        [Test]
        public void Detect_SceneAsset_ReturnsScene()
        {
            // Path-based detection — we don't need a real SceneAsset object
            var kind = ChipKindDetector.Detect(null, "Assets/Scenes/Main.unity");
            // null obj → falls to path suffix check → Asset (null obj returns Asset early)
            // The real path suffix check requires non-null obj to get past the null guard.
            // Test the path-suffix branch with a mock by using a plain Object.
            // Use ScriptableObject as a stand-in non-null object with a .unity path.
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                kind = ChipKindDetector.Detect(so, "Assets/Levels/Test.unity");
                Assert.AreEqual(ChipKind.Scene, kind);
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Prefab ────────────────────────────────────────────────────────────

        [Test]
        public void Detect_PrefabPath_ReturnsPrefab()
        {
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                var kind = ChipKindDetector.Detect(so, "Assets/Prefabs/Enemy.prefab");
                Assert.AreEqual(ChipKind.Prefab, kind);
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Material ──────────────────────────────────────────────────────────

        [Test]
        public void Detect_Material_ReturnsMaterial()
        {
            var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
            try
            {
                var kind = ChipKindDetector.Detect(mat, "Assets/Materials/Lava.mat");
                Assert.AreEqual(ChipKind.Material, kind);
            }
            finally { Object.DestroyImmediate(mat); }
        }

        // ── Texture ───────────────────────────────────────────────────────────

        [Test]
        public void Detect_Texture2D_ReturnsTexture()
        {
            var tex = new Texture2D(2, 2);
            try
            {
                var kind = ChipKindDetector.Detect(tex, "Assets/Textures/Icon.png");
                Assert.AreEqual(ChipKind.Texture, kind);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ── ScriptableObject ─────────────────────────────────────────────────

        [Test]
        public void Detect_ScriptableObject_ReturnsSO()
        {
            var so = ScriptableObject.CreateInstance<ScriptableObject>();
            try
            {
                // Use a path that isn't scene/prefab so those branches don't fire.
                var kind = ChipKindDetector.Detect(so, "Assets/Data/Config.asset");
                Assert.AreEqual(ChipKind.ScriptableObject, kind);
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── Unknown / fallback ────────────────────────────────────────────────

        [Test]
        public void Detect_UnknownAsset_ReturnsAsset()
        {
            var mesh = new Mesh();
            try
            {
                var kind = ChipKindDetector.Detect(mesh, "Assets/Models/Cube.fbx");
                Assert.AreEqual(ChipKind.Asset, kind);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Detect_NullObject_ReturnsAsset()
        {
            var kind = ChipKindDetector.Detect(null, "anything");
            Assert.AreEqual(ChipKind.Asset, kind);
        }

        // ── ShortPrefix ───────────────────────────────────────────────────────

        [Test]
        public void ShortPrefix_AllKinds_NotEmpty()
        {
            foreach (ChipKind k in System.Enum.GetValues(typeof(ChipKind)))
            {
                var prefix = ChipKindDetector.ShortPrefix(k);
                Assert.IsFalse(string.IsNullOrEmpty(prefix), $"ShortPrefix({k}) must not be empty");
            }
        }

        [Test]
        public void ShortPrefix_Hierarchy_ReturnsHierarchy()
        {
            Assert.AreEqual("hierarchy", ChipKindDetector.ShortPrefix(ChipKind.Hierarchy));
        }

        [Test]
        public void ShortPrefix_Script_ReturnsScript()
        {
            Assert.AreEqual("script", ChipKindDetector.ShortPrefix(ChipKind.Script));
        }
    }
}
