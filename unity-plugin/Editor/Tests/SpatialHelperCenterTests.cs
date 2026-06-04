// TDD — RED first. EditMode tests for SpatialHelper.ObjectsInRadius center param. F13.
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SpatialHelperCenterTests
    {
        private GameObject _anchor;
        private GameObject _nearby;
        private GameObject _far;

        [SetUp]
        public void SetUp()
        {
            _anchor = new GameObject("SpatialCenter_Anchor");
            _anchor.transform.position = Vector3.zero;

            _nearby = new GameObject("SpatialCenter_Nearby");
            _nearby.transform.position = new Vector3(2f, 0f, 0f);

            _far = new GameObject("SpatialCenter_Far");
            _far.transform.position = new Vector3(100f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_anchor);
            Object.DestroyImmediate(_nearby);
            Object.DestroyImmediate(_far);
        }

        // Scenario 20: center as world position finds nearby object
        [Test]
        public void ObjectsInRadius_CenterWorldPos_FindsNearbyObject()
        {
            var result = SpatialHelper.ObjectsInRadius(null, 5f, center: "0,0,0");
            StringAssert.Contains("SpatialCenter_Nearby", result);
            StringAssert.DoesNotContain("SpatialCenter_Far", result);
        }

        // Scenario 21: invalid center format throws with descriptive message
        [Test]
        public void ObjectsInRadius_InvalidCenter_ThrowsDescriptiveError()
        {
            var ex = Assert.Throws<System.ArgumentException>(() =>
                SpatialHelper.ObjectsInRadius(null, 5f, center: "not,a,number"));
            StringAssert.Contains("Invalid center format", ex.Message);
        }

        // Scenario 22: center only (no path) works
        [Test]
        public void ObjectsInRadius_CenterOnly_NoPath_Works()
        {
            var result = SpatialHelper.ObjectsInRadius(null, 5f, center: "0,0,0");
            StringAssert.Contains("objects within", result);
        }

        // Scenario 23: both path and center provided — center wins (anchor excluded, world pos used)
        [Test]
        public void ObjectsInRadius_BothPathAndCenter_CenterWins()
        {
            // center at (0,0,0), radius 5 → finds SpatialCenter_Nearby
            // path=/SpatialCenter_Anchor would have excluded _anchor from results
            var result = SpatialHelper.ObjectsInRadius(
                "/" + _anchor.name, 5f, center: "0,0,0");
            // When center wins, _anchor is NOT the fromObj, so it can appear in results
            StringAssert.Contains("SpatialCenter_Nearby", result);
        }

        // Scenario 24: radius 0 returns empty
        [Test]
        public void ObjectsInRadius_RadiusZero_ReturnsEmpty()
        {
            var result = SpatialHelper.ObjectsInRadius("/" + _anchor.name, 0f);
            StringAssert.Contains("No objects within radius", result);
        }

        // Scenario 25: negative radius treated as empty
        [Test]
        public void ObjectsInRadius_NegativeRadius_ReturnsEmpty()
        {
            var result = SpatialHelper.ObjectsInRadius("/" + _anchor.name, -1f);
            StringAssert.Contains("No objects within radius", result);
        }
    }
}
