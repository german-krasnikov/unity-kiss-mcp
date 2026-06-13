using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.3 — MCPSettings.GetCatalogCategories fallback ───────────────

    [TestFixture]
    public class MCPSettingsFallbackTests
    {
        private const string KeyCatalog = "UnityMCP_Catalog";

        [TearDown]
        public void TearDown() => EditorPrefs.DeleteKey(KeyCatalog);

        [Test]
        public void GetCatalogCategories_CorruptStoredCatalog_ReturnsFallbackDefault()
        {
            MCPSettings.SetCatalog("<<<BAD_DATA>>>");

            var cats = MCPSettings.GetCatalogCategories();

            Assert.IsNotNull(cats);
            Assert.IsTrue(cats.ContainsKey("CORE"),
                "Fallback catalog must contain the CORE key");
        }

        [Test]
        public void GetCatalogCategories_EmptyCatalog_ReturnsFallbackDefault()
        {
            MCPSettings.SetCatalog("");

            var cats = MCPSettings.GetCatalogCategories();

            Assert.IsNotNull(cats);
            Assert.IsTrue(cats.ContainsKey("CORE"));
        }
    }
}
