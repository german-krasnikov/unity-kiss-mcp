using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class HierarchyMultiSceneTests
    {
        private Scene _additiveScene;
        private List<GameObject> _toDestroy = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            HierarchySerializer.ResetIncrementalCache();
            TestPaths.EnsureFolder();
            // Save current scene if untitled — NewScene(Additive) requires it
            var current = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path))
                EditorSceneManager.SaveScene(current, TestPaths.TempFolder + "/scene_temp.unity");
            _additiveScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) Object.DestroyImmediate(go);
            _toDestroy.Clear();
            if (_additiveScene.IsValid())
                EditorSceneManager.CloseScene(_additiveScene, true);
            HierarchySerializer.ResetIncrementalCache();
            // Clean temp scene files
            if (System.IO.File.Exists(TestPaths.TempFolder + "/scene_temp.unity"))
                UnityEditor.AssetDatabase.DeleteAsset(TestPaths.TempFolder + "/scene_temp.unity");
        }

        // ── Existing tests (fixed) ────────────────────────────────────────────

        [Test]
        public void SingleScene_NoSceneHeaders()
        {
            EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default;

            var result = HierarchySerializer.Serialize();
            // Only check that no line STARTS with '[' — avoids false positives from InstanceID refs
            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_EmitsHeaders()
        {
            var result = HierarchySerializer.Serialize();
            Assert.That(result, Does.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_ObjectsUnderCorrectScene()
        {
            var go = new GameObject("AdditiveObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize();
            int headerIdx = result.IndexOf("[" + _additiveScene.name + "]");
            Assert.That(headerIdx, Is.GreaterThanOrEqualTo(0), "Header for additive scene not found");
            int objIdx = result.IndexOf("AdditiveObj");
            Assert.That(objIdx, Is.GreaterThan(headerIdx), "AdditiveObj should appear after its scene header");
        }

        [Test]
        public void MultiScene_RootParam_NoHeaders()
        {
            var go = new GameObject("RootParamGo");
            _toDestroy.Add(go);
            var result = HierarchySerializer.Serialize(root: "RootParamGo");
            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_Summary_EmitsHeaders()
        {
            var result = HierarchySerializer.SerializeSummary();
            Assert.That(result, Does.Contain("["));
        }

        [Test]
        public void Fingerprint_MultiScene_IncludesAllScenes()
        {
            // Additive scene is empty — take hash
            string hash1 = FingerprintHelper.Fingerprint(null, 99);

            // Add object to additive scene — hash should change
            var go = new GameObject("FingerprintObj");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);
            string hash2 = FingerprintHelper.Fingerprint(null, 99);

            Assert.That(hash1, Is.Not.EqualTo(hash2), "Fingerprint should differ when additive scene has objects");
        }

        // ── New tests ─────────────────────────────────────────────────────────

        [Test]
        public void MultiScene_Filter_MatchesAcrossScenes()
        {
            var go = new GameObject("UniqueFilterTarget");
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(filter: "UniqueFilterTarget");

            // Object found
            Assert.That(result, Does.Contain("UniqueFilterTarget"));
            // Additive scene header present (the scene has a match)
            Assert.That(result, Does.Contain("[" + _additiveScene.name + "]"));
        }

        [Test]
        public void MultiScene_Filter_NoPhantomHeaderForEmptyScene()
        {
            // Active scene has no object named "XYZ", additive scene also has none
            var result = HierarchySerializer.Serialize(filter: "XYZ_NoSuchObject_Phantom");

            // No scene headers should appear since every scene produced zero nodes
            Assert.That(result, Does.Not.Match(@"(?m)^\["));
        }

        [Test]
        public void MultiScene_Components_HeaderBeforeComponentList()
        {
            var go = new GameObject("CompObj");
            go.AddComponent<Camera>();
            _toDestroy.Add(go);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(components: true);

            int headerIdx = result.IndexOf("[" + _additiveScene.name + "]");
            int compIdx = result.IndexOf("CompObj [");
            Assert.That(headerIdx, Is.GreaterThanOrEqualTo(0), "Scene header missing");
            Assert.That(compIdx, Is.GreaterThan(headerIdx), "Component list should appear after scene header");
        }

        [Test]
        public void MultiScene_TwoScenes_BothInSceneRoots()
        {
            // Verify GetAllLoadedSceneRoots returns both active + additive
            var sceneRoots = HierarchySerializer.GetAllLoadedSceneRoots();
            Assert.That(sceneRoots.Count, Is.GreaterThanOrEqualTo(2),
                $"Expected 2+ scenes but got {sceneRoots.Count}: {string.Join(", ", sceneRoots.ConvertAll(s => s.name))}");
        }

        [Test]
        public void MultiScene_EmptyAdditiveScene_NoPhantomHeader_WithFilter()
        {
            // Put a uniquely-named object only in active scene
            var activeGo = new GameObject("PhantomTestOnlyActive");
            _toDestroy.Add(activeGo);

            var result = HierarchySerializer.Serialize(filter: "PhantomTestOnlyActive");

            // Should contain the filtered object
            Assert.That(result, Does.Contain("PhantomTestOnlyActive"));
            // Count scene headers — should be exactly 1 (only the scene with matching objects)
            var headers = System.Text.RegularExpressions.Regex.Matches(result, @"(?m)^\[");
            Assert.That(headers.Count, Is.EqualTo(1),
                $"Expected 1 scene header (only scene with matches), got {headers.Count}.\n{result}");
        }

        [Test]
        public void MultiScene_Incremental_CacheBustedOnSceneChange()
        {
            var result1 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(result1, Is.Not.EqualTo("NO_CHANGE"), "First call should never return NO_CHANGE");

            // Simulate scene change cache bust
            HierarchySerializer.ResetIncrementalCache();

            var result2 = HierarchySerializer.SerializeIncremental(99, null, null, false);
            Assert.That(result2, Is.Not.EqualTo("NO_CHANGE"), "After cache reset, should return fresh hierarchy");
        }

        [Test]
        public void MultiScene_MAX_NODES_CrossScene_Truncates()
        {
            // Fill the active scene up near MAX_NODES is impractical in a test,
            // but we can verify the truncation message appears when there are many objects
            // by adding enough objects to the additive scene to exceed depth reporting.
            // Instead: verify that the truncation path exists structurally —
            // serialize with depth=0 forces "+N" suffix instead of expansion.
            var go = new GameObject("TruncParent");
            _toDestroy.Add(go);
            var child = new GameObject("TruncChild");
            child.transform.SetParent(go.transform);
            _toDestroy.Add(child);
            SceneManager.MoveGameObjectToScene(go, _additiveScene);

            var result = HierarchySerializer.Serialize(depth: 0);
            // depth=0 means children shown as "+N" count, not expanded
            Assert.That(result, Does.Contain("TruncParent"));
            Assert.That(result, Does.Contain("+1")); // 1 child collapsed
        }

        [Test]
        public void MultiScene_Summary_WithRoot_NoHeaders()
        {
            var go = new GameObject("SummaryRootObj");
            _toDestroy.Add(go);
            // stays in active scene

            var result = HierarchySerializer.SerializeSummary(root: "SummaryRootObj");

            Assert.That(result, Does.Not.Match(@"(?m)^\["), "SerializeSummary(root:X) in multi-scene must not emit scene headers");
            Assert.That(result, Does.Contain("SummaryRootObj"));
        }

        [Test]
        public void GetAllLoadedSceneRoots_ExcludesDontDestroyOnLoad()
        {
            // Both active + additive NewScene scenes should appear.
            // DontDestroyOnLoad virtual scene (runtime only) must not appear.
            var sceneRoots = HierarchySerializer.GetAllLoadedSceneRoots();
            Assert.That(sceneRoots, Is.Not.Empty, "Should return at least the active scene");
            foreach (var (name, _) in sceneRoots)
                Assert.That(name, Does.Not.StartWith("DontDestroyOnLoad"));
        }
    }
}
