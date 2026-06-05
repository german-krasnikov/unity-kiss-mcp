// TDD — InlineChipModel tests (Wave 0, replaces InlineChipTrackerTests.cs).
// Pure C# data model: no Unity rendering dependency. All headless.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class InlineChipModelTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // ── Count ─────────────────────────────────────────────────────────────

        [Test]
        public void AddChip_IncreasesCount()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void AddMultiple_PreservesOrder()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));
            m.Add(new ChipData(ChipKindKeys.Script,    "/C", "C", 3));

            Assert.AreEqual(3, m.Count);
            Assert.AreEqual("/A", m.Chips[0].Path);
            Assert.AreEqual("/B", m.Chips[1].Path);
            Assert.AreEqual("/C", m.Chips[2].Path);
        }

        [Test]
        public void RemoveAt_DecrementsCount()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));

            m.RemoveAt(0);

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual("/B", m.Chips[0].Path);
        }

        [Test]
        public void RemoveAt_OutOfRange_NoThrow()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Asset, "/A", "A", 1));

            Assert.DoesNotThrow(() => m.RemoveAt(-1));
            Assert.DoesNotThrow(() => m.RemoveAt(99));
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void Clear_EmptiesAll()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/A", "A", 1));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/B", "B", 2));
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/C", "C", 3));

            m.Clear();

            Assert.AreEqual(0, m.Count);
        }

        // ── SerializePayload ──────────────────────────────────────────────────

        [Test]
        public void SerializePayload_UsesRegistryFormatPayload()
        {
            // Script chip — FormatPayload returns [script:path]
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0));

            var payload = m.SerializePayload(new ChipConfig());

            StringAssert.Contains("[script:Assets/Foo.cs]", payload);
        }

        [Test]
        public void SerializePayload_SkipsNoneDepth()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0));

            // Force all depths to "none"
            var cfg = new ChipConfig
            {
                ScriptDepth    = "none",
                HierarchyDepth = "none",
                SceneDepth     = "none",
                PrefabDepth    = "none",
                AssetDepth     = "none"
            };

            var payload = m.SerializePayload(cfg);

            Assert.IsEmpty(payload);
        }

        [Test]
        public void SerializePayload_MultipleChips_NewlineSeparated()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/A.cs", "A.cs", 0));
            m.Add(new ChipData(ChipKindKeys.Script, "Assets/B.cs", "B.cs", 0));

            var payload = m.SerializePayload(new ChipConfig());

            StringAssert.Contains("[script:Assets/A.cs]", payload);
            StringAssert.Contains("[script:Assets/B.cs]", payload);
            // The two payloads must be separated by a newline
            StringAssert.Contains("\n", payload);
        }

        // ── Serialize / Restore ───────────────────────────────────────────────

        [Test]
        public void Serialize_Roundtrip()
        {
            var m = new InlineChipModel();
            m.Add(new ChipData(ChipKindKeys.Hierarchy, "/World/Player", "Player", 1));
            m.Add(new ChipData(ChipKindKeys.Script,    "Assets/Foo.cs", "Foo.cs", 0));

            var (paths, kindKeys) = m.SerializeForReload();

            var m2 = new InlineChipModel();
            m2.RestoreFromReload(paths, kindKeys);

            Assert.AreEqual(2,                        m2.Count);
            Assert.AreEqual("/World/Player",           m2.Chips[0].Path);
            Assert.AreEqual(ChipKindKeys.Hierarchy,    m2.Chips[0].KindKey);
            Assert.AreEqual("Assets/Foo.cs",           m2.Chips[1].Path);
            Assert.AreEqual(ChipKindKeys.Script,       m2.Chips[1].KindKey);
        }

        [Test]
        public void Restore_EmptyArrays_NoChips()
        {
            var m = new InlineChipModel();
            m.RestoreFromReload(new string[0], new string[0]);
            Assert.AreEqual(0, m.Count);
        }

        [Test]
        public void Restore_V3BackCompat_EmptyKindKey()
        {
            // v3 format: kindKey is "" — model must preserve empty string (not null, not remapped)
            var m = new InlineChipModel();
            m.RestoreFromReload(new[] { "Assets/foo.fbx" }, new[] { "" });

            Assert.AreEqual(1, m.Count);
            // Path must survive
            Assert.AreEqual("Assets/foo.fbx", m.Chips[0].Path);
            // Empty kindKey must be preserved exactly (v3 back-compat contract)
            Assert.IsNotNull(m.Chips[0].KindKey);
            Assert.AreEqual("", m.Chips[0].KindKey);
        }
    }
}
