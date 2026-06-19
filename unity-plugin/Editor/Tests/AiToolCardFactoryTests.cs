using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AiToolCardFactoryTests
    {
        [Test]
        public void Build_Returns8Cards()
        {
            var cards = AiToolCardFactory.Build(9500);
            Assert.AreEqual(8, cards.Length);
        }

        [Test]
        public void Build_ExternalHosts_First4_HaveCopyOrWriteConfig()
        {
            var cards = AiToolCardFactory.Build(9500);
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(
                    cards[i].Action == CardAction.CopyText || cards[i].Action == CardAction.WriteConfig,
                    $"Card[{i}] '{cards[i].Name}' should be CopyText or WriteConfig, was {cards[i].Action}");
        }

        [Test]
        public void Build_ChatBackends_Last4_HaveCopyPort()
        {
            var cards = AiToolCardFactory.Build(9500);
            for (int i = 4; i < 8; i++)
                Assert.AreEqual(CardAction.CopyPort, cards[i].Action,
                    $"Card[{i}] '{cards[i].Name}' should be CopyPort");
        }

        [Test]
        public void Build_PortIsInPayload_ClaudeCode()
        {
            var cards = AiToolCardFactory.Build(12345);
            Assert.IsTrue(cards[0].Payload.Contains("12345"),
                $"ClaudeCode payload should contain port 12345, was: {cards[0].Payload}");
        }

        [Test]
        public void Build_PortIsInPayload_ChatBackends()
        {
            var cards = AiToolCardFactory.Build(9999);
            for (int i = 4; i < 8; i++)
                Assert.AreEqual("9999", cards[i].Payload,
                    $"Card[{i}] '{cards[i].Name}' payload should be '9999'");
        }

        [Test]
        public void Build_ConfigPaths_NotNullOrEmpty()
        {
            var cards = AiToolCardFactory.Build(9500);
            // Cards 1,2,3 are WriteConfig (Desktop, Cursor, Windsurf)
            for (int i = 1; i <= 3; i++)
                Assert.IsFalse(string.IsNullOrEmpty(cards[i].Payload),
                    $"Card[{i}] '{cards[i].Name}' config path should not be empty");
        }

        [Test]
        public void Build_Names_AreKnownBackends()
        {
            var cards = AiToolCardFactory.Build(9500);
            var names = System.Array.ConvertAll(cards, c => c.Name);
            CollectionAssert.Contains(names, "Claude Code");
            CollectionAssert.Contains(names, "Claude Desktop");
            CollectionAssert.Contains(names, "Cursor");
            CollectionAssert.Contains(names, "Windsurf");
            CollectionAssert.Contains(names, "Gemini");
            CollectionAssert.Contains(names, "Kimi K2");
            CollectionAssert.Contains(names, "Codex");
            CollectionAssert.Contains(names, "OpenCode");
        }

        [Test]
        public void Build_AllCards_HaveNonEmptyLabels()
        {
            var cards = AiToolCardFactory.Build(9500);
            foreach (var card in cards)
            {
                Assert.IsFalse(string.IsNullOrEmpty(card.Name),     $"'{card.Name}' Name empty");
                Assert.IsFalse(string.IsNullOrEmpty(card.Body),     $"'{card.Name}' Body empty");
                Assert.IsFalse(string.IsNullOrEmpty(card.BtnLabel), $"'{card.Name}' BtnLabel empty");
            }
        }
    }
}
