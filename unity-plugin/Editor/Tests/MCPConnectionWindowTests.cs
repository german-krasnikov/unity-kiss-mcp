using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPConnectionWindowTests
    {
        [TearDown]
        public void TearDown() => ChatSettingsHook.ResetConnectionEvent();

        [Test]
        public void OnBuildConnection_NullByDefault_NoSubscribers()
        {
            Assert.IsFalse(ChatSettingsHook.HasConnectionSubscribers);
        }

        [Test]
        public void InvokeConnection_WithSubscriber_CallsIt()
        {
            var called = false;
            ChatSettingsHook.OnBuildConnection += _ => called = true;
            ChatSettingsHook.InvokeConnection(new VisualElement());
            Assert.IsTrue(called);
        }

        [Test]
        public void InvokeConnection_NoSubscribers_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ChatSettingsHook.InvokeConnection(new VisualElement()));
        }

        [Test]
        public void InvokeConnection_MultipleSubscribers_AllCalled()
        {
            int callCount = 0;
            ChatSettingsHook.OnBuildConnection += _ => callCount++;
            ChatSettingsHook.OnBuildConnection += _ => callCount++;
            ChatSettingsHook.InvokeConnection(new VisualElement());
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void InvokeConnection_PassesRootElement_ToSubscriber()
        {
            var expected = new VisualElement();
            VisualElement received = null;
            ChatSettingsHook.OnBuildConnection += root => received = root;
            ChatSettingsHook.InvokeConnection(expected);
            Assert.AreSame(expected, received);
        }
    }
}
