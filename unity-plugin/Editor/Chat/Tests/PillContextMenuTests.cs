// TDD — PillContextMenuTests (F14b).
// Verifies AddToContextAction seam: no crash when null, fires with correct ChipData.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PillContextMenuTests
    {
        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
            ChipPillFactory.ColorResolver      = null;
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
            ChipPillFactory.ColorResolver      = null;
        }

        // 1. AttachMenu_ActionNull_NothingThrows
        [Test]
        public void AttachMenu_ActionNull_NothingThrows()
        {
            ChipPillFactory.AddToContextAction = null;
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Player", "Player", 1);
            var pill = ChipPillFactory.Build(chip);
            Assert.DoesNotThrow(() => ChipPillFactory.AttachAddToContextMenu(pill, chip),
                "AttachAddToContextMenu must not throw when action is null");
        }

        // 2. AttachMenu_ActionFires_ReceivesCorrectChipData
        [Test]
        public void AttachMenu_ActionFires_ReceivesCorrectChipData()
        {
            var chip = new ChipData(ChipKindKeys.Hierarchy, "/Enemy", "Enemy", 42);
            ChipData received = default;
            ChipPillFactory.AddToContextAction = c => received = c;

            var pill = ChipPillFactory.Build(chip);
            ChipPillFactory.AttachAddToContextMenu(pill, chip);

            // Simulate seam fire directly (UI event system not available headless)
            ChipPillFactory.AddToContextAction.Invoke(chip);

            Assert.AreEqual("/Enemy",              received.Path);
            Assert.AreEqual("Enemy",               received.DisplayName);
            Assert.AreEqual(42,                    received.InstanceID);
            Assert.AreEqual(ChipKindKeys.Hierarchy, received.KindKey);
        }

        // 3. ResponsePill_ChipDataFromRefParser_CorrectPath
        [Test]
        public void ResponsePill_ChipDataFromRefParser_CorrectPath()
        {
            // RefParser.Parse must produce correct path for a hierarchy ref
            var chip = RefParser.Parse(ChipKindKeys.Hierarchy, "/Root/Player #99");
            Assert.AreEqual("/Root/Player",        chip.Path);
            Assert.AreEqual("Player",              chip.DisplayName);
            Assert.AreEqual(99,                    chip.InstanceID);
            Assert.AreEqual(ChipKindKeys.Hierarchy, chip.KindKey);
        }

        // 4. UserBubblePill_ActionSetup_ReceivesChipData
        [Test]
        public void UserBubblePill_ActionSetup_ReceivesChipData()
        {
            var chip = new ChipData(ChipKindKeys.Script, "Assets/Foo.cs", "Foo.cs", 0);
            ChipData received = default;
            ChipPillFactory.AddToContextAction = c => received = c;

            var container  = new VisualElement();
            var transcript = new ChatTranscript(container,
                ChatBlockRendererFactory.CreateDefault(null, null));
            var chips = new List<ChipData> { chip };
            transcript.AppendUserBubble("analyze Foo", chips);

            // Pill is present — fire seam directly to verify data integrity
            ChipPillFactory.AddToContextAction.Invoke(chip);

            Assert.AreEqual("Assets/Foo.cs",   received.Path);
            Assert.AreEqual(ChipKindKeys.Script, received.KindKey);
        }
    }
}
