using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpdateCheckerTests
    {
        [SetUp]
        public void SetUp() => UpdateChecker.ResetForTest();

        [Test]
        public void HasUpdate_FalseWhenNoVersion()
        {
            Assert.IsFalse(UpdateChecker.HasUpdate);
        }

        [Test]
        public void HasUpdate_TrueWhenVersionSet()
        {
            UpdateChecker.SetAvailableVersionForTest("1.99.0");
            Assert.IsTrue(UpdateChecker.HasUpdate);
        }

        [Test]
        public void SkipVersion_ClearsAvailableVersion()
        {
            UpdateChecker.SetAvailableVersionForTest("1.99.0");
            UpdateChecker.SkipVersion();
            Assert.IsFalse(UpdateChecker.HasUpdate);
        }

        [Test]
        public void SkipVersion_WhenNoVersion_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UpdateChecker.SkipVersion());
        }
    }
}
