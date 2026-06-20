// TDD: MCPStatusWindow — scheduler fields stored + OnDisable exists (CP-3).
using System.Reflection;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPStatusWindowSchedulerTests
    {
        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        [Test]
        public void MCPStatusWindow_HasSchedulerFields()
        {
            var t = typeof(MCPStatusWindow);
            Assert.IsNotNull(t.GetField("_refreshJob",  NonPublicInstance), "_refreshJob field must exist");
            Assert.IsNotNull(t.GetField("_beatFastJob", NonPublicInstance), "_beatFastJob field must exist");
            Assert.IsNotNull(t.GetField("_beatSoftJob", NonPublicInstance), "_beatSoftJob field must exist");
        }

        [Test]
        public void MCPStatusWindow_HasOnDisableMethod()
        {
            var m = typeof(MCPStatusWindow).GetMethod("OnDisable", NonPublicInstance);
            Assert.IsNotNull(m, "OnDisable method must exist on MCPStatusWindow");
        }

        [Test]
        public void MCPStatusWindow_OnDisable_IsPrivate()
        {
            var m = typeof(MCPStatusWindow).GetMethod("OnDisable", NonPublicInstance);
            Assert.IsNotNull(m);
            Assert.IsTrue(m.IsPrivate, "OnDisable must be private — Unity callback convention");
        }
    }
}
