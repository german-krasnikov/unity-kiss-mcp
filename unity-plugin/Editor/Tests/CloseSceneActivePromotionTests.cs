using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Tests
{
    // ── CS5.test.6 — SceneHelper.CloseScene active-scene promotion ───────────

    [TestFixture]
    public class CloseSceneActivePromotionTests : MultiSceneTestBase
    {
        [Test]
        public void CloseScene_ActiveScene_PromotesRemaining()
        {
            // Make the additive scene the active one
            SceneManager.SetActiveScene(_additiveScene);
            var additiveName = _additiveScene.name;

            SceneHelper.CloseScene(additiveName);
            _additiveScene = default; // already closed

            // Active scene must now be something other than the closed scene
            Assert.AreNotEqual(additiveName, SceneManager.GetActiveScene().name,
                "After closing active scene, a different scene must become active");
            Assert.IsTrue(SceneManager.GetActiveScene().IsValid(),
                "Active scene must still be valid after promotion");
        }
    }
}
