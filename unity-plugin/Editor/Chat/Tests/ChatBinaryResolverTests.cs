// TDD — RED first. Tests drive ChatBinaryResolver negative-cache and seam contract.
// Requires UNITY_INCLUDE_TESTS define (Chat.Tests asmdef enforces it).
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatBinaryResolverTests
    {
        [SetUp]
        public void SetUp()
        {
            ChatBinaryResolver.WhichOverride = null;
            EditorPrefs.DeleteKey(ChatBinaryResolver.PrefKey);
            ChatBinaryResolver.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ChatBinaryResolver.WhichOverride = null;
            EditorPrefs.DeleteKey(ChatBinaryResolver.PrefKey);
        }

        [Test]
        public void Resolve_PrefOverride_SkipsWhich()
        {
            var invoked = false;
            ChatBinaryResolver.WhichOverride = _ => { invoked = true; return "/usr/local/bin/claude"; };

            EditorPrefs.SetString(ChatBinaryResolver.PrefKey, "/custom/claude");
            var result = ChatBinaryResolver.Resolve();

            Assert.AreEqual("/custom/claude", result);
            Assert.IsFalse(invoked, "WhichOverride must not be called when EditorPrefs override is set");
        }

        [Test]
        public void Resolve_NegativeCache_DoesNotReprobe()
        {
            var callCount = 0;
            ChatBinaryResolver.WhichOverride = _ => { callCount++; return null; };

            ChatBinaryResolver.Resolve(); // first probe
            ChatBinaryResolver.Resolve(); // should hit cache

            Assert.AreEqual(1, callCount, "WhichOverride must only be called once after a null result");
        }

        [Test]
        public void Resolve_ForceRefresh_Reprobes()
        {
            var callCount = 0;
            ChatBinaryResolver.WhichOverride = _ => { callCount++; return null; };

            ChatBinaryResolver.Resolve();                    // probe → callCount==1
            ChatBinaryResolver.Resolve(forceRefresh: true);  // bust cache → callCount==2

            Assert.AreEqual(2, callCount, "forceRefresh:true must re-invoke WhichOverride");
        }
    }
}
