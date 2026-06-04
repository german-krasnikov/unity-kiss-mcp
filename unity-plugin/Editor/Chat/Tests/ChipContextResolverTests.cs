// TDD — RED first. Tests drive ChipContextResolver contract.
// EditMode tests: can create GameObjects and call ComponentSerializer.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChipContextResolverTests
    {
        private List<GameObject> _created;

        [SetUp]
        public void SetUp()
        {
            _created = new List<GameObject>();
            ChipContextResolver.FindObjectOverride = null; // reset seam
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            ChipContextResolver.FindObjectOverride = null;
        }

        private GameObject MakeGo(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform);
            _created.Add(go);
            return go;
        }

        [Test]
        public void ResolveOne_PathOnly_ReturnsPathAsIs()
        {
            var result = ChipContextResolver.ResolveOne("/Scene/Hero", ChipDepth.PathOnly);
            Assert.AreEqual("/Scene/Hero", result);
        }

        [Test]
        public void ResolveOne_Summary_SceneObject_ReturnsSummaryFormat()
        {
            var go = MakeGo("Hero");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/Hero", ChipDepth.Summary);

            // Must contain path and component type, enclosed in brackets, with correct tag
            StringAssert.StartsWith("[Context:", result);
            StringAssert.Contains("/Hero", result);
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void ResolveOne_Summary_NullObject_FallsBackToPathOnly()
        {
            ChipContextResolver.FindObjectOverride = _ => null;

            var result = ChipContextResolver.ResolveOne("/Missing/Object", ChipDepth.Summary);

            Assert.AreEqual("/Missing/Object", result);
        }

        [Test]
        public void ResolveOne_Full_ReturnsComponentDump()
        {
            var go = MakeGo("Tank");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/Tank", ChipDepth.Full);

            // SerializeAll produces "name: Tank" + component sections
            StringAssert.Contains("Tank", result);
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void ResolveOne_Full_ExceedsBudget_FallsToSummary()
        {
            var go = MakeGo("Fat");
            // Add many components to inflate the output
            for (var i = 0; i < 10; i++) go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;

            // Override budget to 1 char so anything exceeds it
            var result = ChipContextResolver.ResolveOne("/Fat", ChipDepth.Full, budgetOverride: 1);

            // Falls back to Summary: should start with '[' and not be the full dump
            Assert.IsTrue(result.StartsWith("["), "Fallback should be summary format");
        }

        [Test]
        public void ResolveOne_AssetPath_AlwaysPathOnly()
        {
            var result = ChipContextResolver.ResolveOne("Assets/Scripts/Foo.cs", ChipDepth.Full);
            Assert.AreEqual("Assets/Scripts/Foo.cs", result);
        }

        [Test]
        public void ResolveAll_SingleChip_UsesFull()
        {
            var go = MakeGo("Solo");
            go.AddComponent<Rigidbody>();
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveAll(new List<string> { "/Solo" });

            // Full includes serialized component data (not just a summary line)
            StringAssert.Contains("Solo", result);
        }

        [Test]
        public void ResolveAll_MultipleChips_UsesSummary()
        {
            var go1 = MakeGo("A");
            var go2 = MakeGo("B");
            go1.AddComponent<BoxCollider>();
            go2.AddComponent<Rigidbody>();
            ChipContextResolver.FindObjectOverride = p => p == "/A" ? go1 : go2;

            var result = ChipContextResolver.ResolveAll(new List<string> { "/A", "/B" });

            // Summary format: contains both paths, starts with '[' lines
            StringAssert.Contains("/A", result);
            StringAssert.Contains("/B", result);
        }

        [Test]
        public void ResolveAll_EmptyList_ReturnsEmpty()
        {
            var result = ChipContextResolver.ResolveAll(new List<string>());
            Assert.AreEqual("", result);
        }

        [Test]
        public void ResolveAll_MixedSceneAndAsset_CorrectDepths()
        {
            var go = MakeGo("Player");
            ChipContextResolver.FindObjectOverride = _ => go;
            var chips = new List<string> { "/Player", "Assets/Scripts/Foo.cs" };

            // 2 chips → Summary for scene, PathOnly for asset
            var result = ChipContextResolver.ResolveAll(chips);

            // Scene chip: summary brackets
            StringAssert.Contains("/Player", result);
            // Asset chip: path as-is
            StringAssert.Contains("Assets/Scripts/Foo.cs", result);
        }

        [Test]
        public void ResolveOne_DestroyedGameObject_FallsBackToPathOnly()
        {
            var go = new GameObject("Dead");
            Object.DestroyImmediate(go);
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/Dead", ChipDepth.Summary);

            // Destroyed object (Unity == null) → PathOnly
            Assert.AreEqual("/Dead", result);
        }

        [Test]
        public void Summary_Format_IsStructurallyValid()
        {
            var go = MakeGo("Check");
            go.AddComponent<BoxCollider>();
            ChipContextResolver.FindObjectOverride = _ => go;

            var chip    = ChipContextResolver.ResolveOne("/Check", ChipDepth.Summary);
            var sel     = SelectionSummary.Summarize(go);

            // Both must: start with '[', contain path, contain component name, end with ']'
            Assert.IsTrue(chip.StartsWith("["),  "chip must start with '['");
            Assert.IsTrue(sel.StartsWith("["),   "sel must start with '['");
            Assert.IsTrue(chip.EndsWith("]"),    "chip must end with ']'");
            Assert.IsTrue(sel.EndsWith("]"),     "sel must end with ']'");
            StringAssert.Contains("BoxCollider", chip);
            StringAssert.Contains("BoxCollider", sel);
        }

        // ── F4: instance ID tests ─────────────────────────────────────────────

        [Test]
        public void ResolveOne_PathOnly_SceneObject_IncludesInstanceID()
        {
            var go = MakeGo("SceneObj");
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/SceneObj", ChipDepth.PathOnly);

            // Must match "/SceneObj #<digits>"
            StringAssert.Contains("/SceneObj #", result);
            var parts = result.Split('#');
            Assert.AreEqual(2, parts.Length);
            Assert.IsTrue(int.TryParse(parts[1].Trim(), out _), "suffix must be an integer instanceID");
        }

        [Test]
        public void ResolveOne_PathOnly_AssetPath_NoInstanceID()
        {
            var result = ChipContextResolver.ResolveOne("Assets/Foo.prefab", ChipDepth.PathOnly);
            Assert.AreEqual("Assets/Foo.prefab", result);
            Assert.IsFalse(result.Contains("#"), "asset path must not contain instance ID");
        }

        [Test]
        public void ResolveOne_Summary_IncludesInstanceID()
        {
            var go = MakeGo("SummaryObj");
            ChipContextResolver.FindObjectOverride = _ => go;

            var result = ChipContextResolver.ResolveOne("/SummaryObj", ChipDepth.Summary);

            var idStr = go.GetInstanceID().ToString();
            StringAssert.Contains($"#{idStr}", result);
        }

        [Test]
        public void ResolveAll_MultiChip_EachHasInstanceID()
        {
            var go1 = MakeGo("Multi1");
            var go2 = MakeGo("Multi2");
            ChipContextResolver.FindObjectOverride = p => p.Contains("Multi1") ? go1 : go2;

            // 2 chips → Summary depth → each summary contains its instanceID
            var result = ChipContextResolver.ResolveAll(new List<string> { "/Multi1", "/Multi2" });

            StringAssert.Contains($"#{go1.GetInstanceID()}", result);
            StringAssert.Contains($"#{go2.GetInstanceID()}", result);
        }
    }
}
