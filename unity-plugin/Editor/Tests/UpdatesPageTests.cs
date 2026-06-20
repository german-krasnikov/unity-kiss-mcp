using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class UpdatesPageTests
    {
        [SetUp]    public void SetUp()    => UpdateChecker.ResetForTest();
        [TearDown] public void TearDown() => UpdateChecker.ResetForTest();

        [Test]
        public void BuildUpdatesPage_ReturnsNonNull()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            Assert.IsNotNull(page);
        }

        [Test]
        public void BuildUpdatesPage_HasNavPageClass()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            Assert.IsTrue(page.ClassListContains("nav-page"));
        }

        [Test]
        public void BuildUpdatesPage_ContainsCheckButton()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var btn = page.Q<Button>(className: "updates-check-btn");
            Assert.IsNotNull(btn, "Expected a Button with class 'updates-check-btn'");
        }

        [Test]
        public void BuildUpdatesPage_ContainsChangelogArea()
        {
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var area = page.Q(className: "updates-changelog");
            Assert.IsNotNull(area, "Expected an element with class 'updates-changelog'");
        }

        [Test]
        public void BuildUpdatesPage_NoBanner_WhenNoUpdate()
        {
            // HasUpdate is false after reset, so banner should not appear
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var banner = page.Q(className: "wiz-card");
            Assert.IsNull(banner, "Banner should not be present when no update available");
        }

        [Test]
        public void BuildUpdatesPage_ShowsBanner_WhenUpdateAvailable()
        {
            UpdateChecker.SetAvailableVersionForTest("9.99.0");
            var page = SettingsPageFactory.BuildUpdatesPage(() => { });
            var banner = page.Q(className: "wiz-card");
            Assert.IsNotNull(banner, "Banner should be present when update available");
        }
    }
}
