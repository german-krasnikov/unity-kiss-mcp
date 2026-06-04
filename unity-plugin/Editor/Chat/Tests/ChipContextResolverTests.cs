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

        // ── F10: FormatChipRef ─────────────────────────────────────────────────

        [Test]
        public void FormatChipRef_Hierarchy_BracketFormatWithInstanceID()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKind.Hierarchy, "/World/Player", 12345);
            Assert.AreEqual("[hierarchy:/World/Player #12345]", result);
        }

        [Test]
        public void FormatChipRef_Script_NameOnly_NoBracketID()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKind.Script, "PlayerController", 0);
            Assert.AreEqual("[script:PlayerController]", result);
        }

        [Test]
        public void FormatChipRef_Scene_FullAssetPath()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKind.Scene, "Assets/Scenes/Main.unity", 0);
            Assert.AreEqual("[scene:Assets/Scenes/Main.unity]", result);
        }

        [Test]
        public void FormatChipRef_Asset_AssetPath()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKind.Asset, "Assets/Fonts/Arial.ttf", 0);
            Assert.AreEqual("[asset:Assets/Fonts/Arial.ttf]", result);
        }

        [Test]
        public void FormatChipRef_Hierarchy_ZeroInstanceID_OmitsHashSuffix()
        {
            // instanceID 0 = no object found; should omit the # suffix
            var result = ChipContextResolver.FormatChipRef(ChipKind.Hierarchy, "/Player", 0);
            Assert.AreEqual("[hierarchy:/Player]", result);
        }

        [Test]
        public void FormatChipRef_ScriptableObject_UsesSoPrefix()
        {
            var result = ChipContextResolver.FormatChipRef(ChipKind.ScriptableObject, "Assets/Data/Cfg.asset", 0);
            Assert.AreEqual("[so:Assets/Data/Cfg.asset]", result);
        }

        // ── F10: EmitTyped ────────────────────────────────────────────────────

        [Test]
        public void EmitTyped_DepthNone_ReturnsEmpty()
        {
            var result = ChipContextResolver.EmitTyped(ChipKind.Script, "Assets/Foo.cs", 0, "none", (p, d) => "RESOLVED");
            Assert.AreEqual("", result);
        }

        [Test]
        public void EmitTyped_DepthPath_ReturnsBracketOnly()
        {
            var result = ChipContextResolver.EmitTyped(ChipKind.Script, "Assets/Foo.cs", 0, "path", (p, d) => "RESOLVED");
            Assert.AreEqual("[script:Assets/Foo.cs]", result);
        }

        [Test]
        public void EmitTyped_DepthPath_Hierarchy_IncludesInstanceID()
        {
            var result = ChipContextResolver.EmitTyped(ChipKind.Hierarchy, "/Player", 123, "path", (p, d) => "RESOLVED");
            Assert.AreEqual("[hierarchy:/Player #123]", result);
        }

        [Test]
        public void EmitTyped_DepthSummary_StartsWithBracketThenResolved()
        {
            var result = ChipContextResolver.EmitTyped(ChipKind.Hierarchy, "/Player", 123, "summary",
                (p, d) => "summary-text");
            StringAssert.StartsWith("[hierarchy:/Player #123]", result);
            StringAssert.Contains("summary-text", result);
        }

        [Test]
        public void EmitTyped_DepthFull_StartsWithBracketThenResolved()
        {
            var result = ChipContextResolver.EmitTyped(ChipKind.Hierarchy, "/Player", 0, "full",
                (p, d) => "full-dump");
            StringAssert.StartsWith("[hierarchy:/Player]", result);
            StringAssert.Contains("full-dump", result);
        }

        [Test]
        public void EmitTyped_DepthSummary_Asset_BracketAndAssetPath()
        {
            // Assets don't have scene resolution; resolveFn still called but result may be path
            var result = ChipContextResolver.EmitTyped(ChipKind.Script, "Assets/Foo.cs", 0, "summary",
                (p, d) => p); // resolveFn returns path as-is for assets
            StringAssert.StartsWith("[script:Assets/Foo.cs]", result);
        }

        [Test]
        public void EmitTyped_UnknownDepth_TreatedAsPath()
        {
            // Any unrecognized depth string → treat as "path" (safe fallback)
            var result = ChipContextResolver.EmitTyped(ChipKind.Asset, "Assets/X.png", 0, "bogus", (p, d) => "R");
            Assert.AreEqual("[asset:Assets/X.png]", result);
        }
    }
}
