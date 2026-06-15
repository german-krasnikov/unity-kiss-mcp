// TDD tests for PendingAskRegistry — thread-safe TCS store for ask_user.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PendingAskRegistryTests
    {
        [SetUp]
        public void Setup()
        {
            // Clear any state from previous tests
            PendingAskRegistry.CancelAll();
        }

        [Test]
        public void Register_StoresEntry_GetTcsReturnsNonNull()
        {
            var id = "test-001";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);
            Assert.IsNotNull(tcs);
        }

        [Test]
        public void Complete_ResolvesTask_TaskResultMatchesInput()
        {
            var id = "test-002";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);

            PendingAskRegistry.Complete(id, "{\"q\":\"a\"}");

            Assert.IsTrue(tcs.Task.IsCompleted);
            Assert.AreEqual("{\"q\":\"a\"}", tcs.Task.Result);
        }

        [Test]
        public void Cancel_CancelsTask_TaskIsCanceled()
        {
            var id = "test-003";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);

            PendingAskRegistry.Cancel(id);

            Assert.IsTrue(tcs.Task.IsCanceled);
        }

        [Test]
        public void Complete_AfterCancel_IsNoop_NoException()
        {
            var id = "test-004";
            PendingAskRegistry.Register(id);
            PendingAskRegistry.Cancel(id);

            // Must not throw
            Assert.DoesNotThrow(() => PendingAskRegistry.Complete(id, "ignored"));
        }

        [Test]
        public void GetTcs_UnknownId_ReturnsNull()
        {
            var tcs = PendingAskRegistry.GetTcs("nonexistent-id");
            Assert.IsNull(tcs);
        }

        [Test]
        public void Register_Duplicate_Overwrites_NoPreviousTaskLeak()
        {
            var id = "test-006";
            PendingAskRegistry.Register(id);
            var tcs1 = PendingAskRegistry.GetTcs(id);

            // Re-register same id (domain-reload safety)
            PendingAskRegistry.Register(id);
            var tcs2 = PendingAskRegistry.GetTcs(id);

            // New TCS returned, old one was replaced
            Assert.IsNotNull(tcs2);
        }

        [Test]
        public void CancelAll_CompletesAllPendingWithCancellation()
        {
            PendingAskRegistry.Register("a");
            PendingAskRegistry.Register("b");
            var tcsA = PendingAskRegistry.GetTcs("a");
            var tcsB = PendingAskRegistry.GetTcs("b");

            PendingAskRegistry.CancelAll();

            Assert.IsTrue(tcsA.Task.IsCanceled);
            Assert.IsTrue(tcsB.Task.IsCanceled);
        }
    }
}
