using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public class ChatActivityStateTests
    {
        [Test] public void Initial_Phase_Is_Idle()
            => Assert.AreEqual(ActivityPhase.Idle, new ChatActivityState().Phase);

        [Test] public void Send_From_Idle_Returns_True()
        {
            var s = new ChatActivityState();
            Assert.IsTrue(s.Send());
            Assert.AreEqual(ActivityPhase.Sending, s.Phase);
        }

        [Test] public void Send_While_Sending_Returns_False()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsFalse(s.Send());
        }

        [Test] public void FirstToken_From_Sending_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.FirstToken());
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
        }

        [Test] public void FirstToken_From_Idle_Is_Ignored()
            => Assert.IsFalse(new ChatActivityState().FirstToken());

        [Test] public void FirstToken_From_Receiving_Is_Ignored()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsFalse(s.FirstToken());
        }

        [Test] public void Done_From_Receiving_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Done_From_Idle_Returns_False()
            => Assert.IsFalse(new ChatActivityState().Done());

        [Test] public void Done_From_Sending_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Fail_Aliases_Done()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.Fail());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Fail_From_Idle_Returns_False()
            => Assert.IsFalse(new ChatActivityState().Fail());

        [Test] public void Full_Lifecycle()
        {
            var s = new ChatActivityState();
            Assert.IsTrue(s.Send());
            Assert.IsTrue(s.FirstToken());
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Send_From_Receiving_Is_Ignored()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsFalse(s.Send());
        }
    }
}
