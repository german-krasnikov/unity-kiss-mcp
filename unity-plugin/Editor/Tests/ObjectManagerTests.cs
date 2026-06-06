// TDD — EditMode tests for ObjectManager mutations (P0-2 audit gap).
// Run in Unity Test Runner → EditMode.
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ObjectManagerTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("OM_TestObj");
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy test object if still alive (some tests destroy it intentionally)
            if (_go != null)
                Object.DestroyImmediate(_go);

            // Clean up any objects created by tests that aren't tracked via _go
            foreach (var name in new[] { "OM_Created", "OM_Parent", "OM_Child", "OM_SetActive" })
            {
                var stray = GameObject.Find(name);
                if (stray != null)
                    Object.DestroyImmediate(stray);
            }
        }

        // ── 1. CreateObject ───────────────────────────────────────────────────

        [Test]
        public void CreateObject_WithName_CreatesInScene()
        {
            var path = ObjectManager.CreateObject("OM_Created", null, null);

            var found = GameObject.Find("OM_Created");
            Assert.IsNotNull(found, "GameObject should exist in scene after CreateObject");
            Assert.AreEqual("/OM_Created", path);

            Object.DestroyImmediate(found);
        }

        [Test]
        public void CreateObject_WithPrimitive_CreatesMeshFilter()
        {
            var path = ObjectManager.CreateObject("OM_Created", null, null, primitive: "Cube");

            var found = GameObject.Find("OM_Created");
            Assert.IsNotNull(found);
            Assert.IsNotNull(found.GetComponent<MeshFilter>(), "Primitive Cube must have MeshFilter");

            Object.DestroyImmediate(found);
        }

        [Test]
        public void CreateObject_UnknownComponent_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.CreateObject("OM_Created", null, "NonExistentComponentXYZ"));

            // cleanup if it partially created
            var stray = GameObject.Find("OM_Created");
            if (stray != null) Object.DestroyImmediate(stray);
        }

        // ── 2. DeleteObject ───────────────────────────────────────────────────

        [Test]
        public void DeleteObject_RemovesFromScene()
        {
            // _go is tracked; after delete we null it so TearDown skips it
            ObjectManager.DeleteObject("/OM_TestObj");
            _go = null;

            Assert.IsNull(GameObject.Find("OM_TestObj"), "Object should be gone after DeleteObject");
        }

        [Test]
        public void DeleteObject_WithChildren_WithoutForce_ThrowsArgumentException()
        {
            var child = new GameObject("OM_Child");
            child.transform.SetParent(_go.transform);

            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.DeleteObject("/OM_TestObj"));

            // _go and child still alive — TearDown cleans up
        }

        [Test]
        public void DeleteObject_WithChildren_WithForce_DeletesAll()
        {
            var child = new GameObject("OM_Child");
            child.transform.SetParent(_go.transform);

            ObjectManager.DeleteObject("/OM_TestObj", force: true);
            _go = null;

            Assert.IsNull(GameObject.Find("OM_TestObj"));
            Assert.IsNull(GameObject.Find("OM_Child"));
        }

        [Test]
        public void DeleteObject_NotFound_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.DeleteObject("/OM_DoesNotExist_XYZ", force: true));
        }

        // ── 3. SetActive ──────────────────────────────────────────────────────

        [Test]
        public void SetActive_False_DeactivatesObject()
        {
            ObjectManager.SetActive("/OM_TestObj", false);

            Assert.IsFalse(_go.activeSelf, "activeSelf should be false after SetActive(false)");
        }

        [Test]
        public void SetActive_True_ActivatesObject()
        {
            _go.SetActive(false);

            ObjectManager.SetActive("/OM_TestObj", true);

            Assert.IsTrue(_go.activeSelf, "activeSelf should be true after SetActive(true)");
        }

        [Test]
        public void SetActive_ReturnsPathAndState()
        {
            var result = ObjectManager.SetActive("/OM_TestObj", false);

            StringAssert.Contains("OM_TestObj", result);
            StringAssert.Contains("active=False", result);
        }

        // ── 4. ManageComponent ────────────────────────────────────────────────

        [Test]
        public void ManageComponent_Add_AddsComponent()
        {
            ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "add");

            Assert.IsNotNull(_go.GetComponent<Rigidbody>(), "Rigidbody should be present after add");
        }

        [Test]
        public void ManageComponent_Remove_RemovesComponent()
        {
            _go.AddComponent<Rigidbody>();

            ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "remove");

            Assert.IsNull(_go.GetComponent<Rigidbody>(), "Rigidbody should be gone after remove");
        }

        [Test]
        public void ManageComponent_AddDuplicate_ThrowsArgumentException()
        {
            _go.AddComponent<Rigidbody>();

            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "add"));
        }

        [Test]
        public void ManageComponent_RemoveMissing_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "remove"));
        }

        [Test]
        public void ManageComponent_InvalidAction_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.ManageComponent("/OM_TestObj", "Rigidbody", "replace"));
        }

        // ── 5. SetParent ──────────────────────────────────────────────────────

        [Test]
        public void SetParent_ValidPath_ReparentsObject()
        {
            var parent = new GameObject("OM_Parent");

            try
            {
                ObjectManager.SetParent("/OM_TestObj", "/OM_Parent");

                Assert.AreEqual(parent.transform, _go.transform.parent,
                    "Parent should be OM_Parent after SetParent");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SetParent_NullParent_UnparentsObject()
        {
            var parent = new GameObject("OM_Parent");
            _go.transform.SetParent(parent.transform);

            try
            {
                ObjectManager.SetParent("/OM_Parent/OM_TestObj", null);

                Assert.IsNull(_go.transform.parent, "Parent should be null after unparenting");
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void SetParent_InvalidChildPath_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetParent("/OM_DoesNotExist_XYZ", null));
        }

        // ── 6. SetProperty ────────────────────────────────────────────────────

        [Test]
        public void SetProperty_FloatField_UpdatesValue()
        {
            // m_LocalPosition.x on Transform
            ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_LocalPosition", "(5,0,0)");

            Assert.AreEqual(5f, _go.transform.localPosition.x, 0.001f,
                "localPosition.x should be 5 after SetProperty");
        }

        [Test]
        public void SetProperty_InvalidComponent_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty("/OM_TestObj", "NonExistentComponent", "m_LocalPosition", "(0,0,0)"));
        }

        [Test]
        public void SetProperty_InvalidProperty_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_NonExistentProp_XYZ", "0"));
        }

        [Test]
        public void SetProperty_DryRun_DoesNotMutate()
        {
            _go.transform.localPosition = Vector3.zero;

            var result = ObjectManager.SetProperty("/OM_TestObj", "Transform", "m_LocalPosition", "(9,0,0)", dryRun: true);

            Assert.AreEqual(0f, _go.transform.localPosition.x, 0.001f,
                "DryRun must not change value");
            StringAssert.Contains("DRY-RUN", result);
        }
    }
}
