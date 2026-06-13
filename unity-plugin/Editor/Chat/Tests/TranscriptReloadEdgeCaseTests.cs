// TDD — F21 gap-fill: serialization edge cases and cap behaviour.
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class TranscriptReloadEdgeCaseTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() { ChipKindRegistry.ResetToBuiltIns(); ChipPillFactory.ColorResolver = null; }

        private ChatTranscript Make(out VisualElement c)
        {
            c = new VisualElement();
            return new ChatTranscript(c, ChatBlockRendererFactory.CreateDefault(null, null));
        }

        // 1. cap: 210 messages → serialized max 200, tail preserved
        [Test]
        public void SerializeForReload_CapsToMaxMessages()
        {
            var t = Make(out _);
            for (int i = 0; i < 210; i++)
                t.AppendUserBubble($"msg{i}");

            var data = t.SerializeForReload();
            var entries = TranscriptSerializer.Deserialize(data);

            Assert.AreEqual(200, entries.Count, "must cap at 200");
            // tail preserved: last message should be msg209
            Assert.IsTrue(entries[199].Text.Contains("msg209"), "tail must be preserved");
            // head trimmed: msg0..msg9 are gone
            Assert.IsFalse(entries[0].Text.Contains("msg0"), "msg0 must be trimmed");
        }

        // 2. restore does not double entries (single instance, restore twice — CH4.test.6 fix)
        [Test]
        public void RestoreFromReload_DoesNotDoubleEntries()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("a");
            t1.AppendUserBubble("b");
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data);
            var data2 = t2.SerializeForReload();

            var entries1 = TranscriptSerializer.Deserialize(data);
            var entries2 = TranscriptSerializer.Deserialize(data2);
            Assert.AreEqual(entries1.Count, entries2.Count,
                "double restore must not duplicate entries");
        }

        // 2b. RestoreFromReload on the SAME instance twice must not accumulate entries
        [Test]
        public void RestoreFromReload_SameInstance_CalledTwice_DoesNotDouble()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("x");
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data); // first restore
            t2.RestoreFromReload(data); // second restore on same instance

            var entries = TranscriptSerializer.Deserialize(t2.SerializeForReload());
            Assert.AreEqual(1, entries.Count,
                "calling RestoreFromReload twice on the same instance must not double entries");
        }

        // 3. serialize → restore → serialize is idempotent
        [Test]
        public void SerializeForReload_Idempotent_DoubleRoundTrip()
        {
            var t1 = Make(out _);
            t1.AppendUserBubble("hello");
            t1.AppendOrExtendAssistant("world");
            t1.FinalizeAssistant();
            var data1 = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data1);
            var data2 = t2.SerializeForReload();

            Assert.AreEqual(data1, data2, "double round-trip must be idempotent");
        }

        // 4. Serialize(null) returns empty string
        [Test]
        public void Serialize_NullList_ReturnsEmpty()
        {
            Assert.AreEqual("", TranscriptSerializer.Serialize(null));
        }

        // 5. Deserialize(null) returns empty list
        [Test]
        public void Deserialize_Null_ReturnsEmptyList()
        {
            Assert.AreEqual(0, TranscriptSerializer.Deserialize(null).Count);
        }

        // 6. unicode text survives round-trip
        [Test]
        public void SerializeForReload_UnicodeText_Survives()
        {
            const string unicode = "Привет 🌍 日本語";
            var t1 = Make(out _);
            t1.AppendOrExtendAssistant(unicode);
            t1.FinalizeAssistant();
            var data = t1.SerializeForReload();

            var t2 = Make(out _);
            t2.RestoreFromReload(data);
            var entries = TranscriptSerializer.Deserialize(data);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(unicode, entries[0].Text, "unicode must survive round-trip");
        }

        // 7. SerializeChips(null) returns null
        [Test]
        public void SerializeChips_Null_ReturnsNull()
        {
            Assert.IsNull(TranscriptSerializer.SerializeChips(null));
        }

        // 8. DeserializeChips(null) returns null
        [Test]
        public void DeserializeChips_Null_ReturnsNull()
        {
            Assert.IsNull(TranscriptSerializer.DeserializeChips(null));
        }

        // 9. _entries list is capped at MaxMessages (CH4.arch.1 fix verification)
        [Test]
        public void Entries_CappedAtMaxMessages_WhenContainerEvicts()
        {
            var t = Make(out _);
            for (int i = 0; i < 210; i++)
                t.AppendUserBubble($"msg{i}");

            // Verify via reflection that _entries is also capped (not growing unboundedly)
            var entriesField = typeof(ChatTranscript)
                .GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance);
            var entries = (System.Collections.Generic.List<TranscriptEntry>)entriesField.GetValue(t);

            Assert.LessOrEqual(entries.Count, 200, "_entries must be capped at MaxMessages (200)");

            // Also confirm SerializeForReload still caps correctly
            var data    = t.SerializeForReload();
            var serial  = TranscriptSerializer.Deserialize(data);
            Assert.AreEqual(200, serial.Count, "serialized must cap at 200");
        }
    }
}
