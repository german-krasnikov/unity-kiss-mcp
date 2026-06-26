// TDD — PropertyContextMenuBridge: right-click property → chip creation.
// Tests BuildChipForProperty seam (no GenericMenu click required headlessly).
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    // Minimal MonoBehaviour for testing the m_Script guard
    internal class BridgeTestBehaviour : MonoBehaviour
    {
        public float testValue;
    }

    [TestFixture]
    public class PropertyContextMenuBridgeTests
    {
        [SetUp]
        public void SetUp()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
        }

        [TearDown]
        public void TearDown()
        {
            ChipKindRegistry.ResetToBuiltIns();
            ChipPillFactory.AddToContextAction = null;
        }

        // Guard: null action → OnPropertyContextMenu no-throw
        [Test]
        public void OnPropertyContextMenu_NullAction_NoThrow()
        {
            ChipPillFactory.AddToContextAction = null;
            var go   = new GameObject("BridgeTestGO");
            var rb   = go.AddComponent<Rigidbody>();
            var so   = new SerializedObject(rb);
            var prop = FirstNonScriptProp(so);
            Assume.That(prop, Is.Not.Null, "Rigidbody has no visible properties — skipping");
            var menu = new GenericMenu();

            Assert.DoesNotThrow(() => PropertyContextMenuBridge.OnPropertyContextMenu(menu, prop));
            Object.DestroyImmediate(go);
        }

        // Guard: non-Component target → BuildChipForProperty returns null
        [Test]
        public void BuildChipForProperty_NonComponentTarget_ReturnsNull()
        {
            var mat  = new Material(Shader.Find("Standard"));
            var so   = new SerializedObject(mat);
            var prop = so.GetIterator();
            prop.NextVisible(true);

            var result = PropertyContextMenuBridge.BuildChipForProperty(prop);

            Assert.IsNull(result, "non-Component target must return null");
            Object.DestroyImmediate(mat);
        }

        // Guard: m_Script property → BuildChipForProperty returns null
        [Test]
        public void BuildChipForProperty_ScriptProperty_ReturnsNull()
        {
            var go   = new GameObject("ScriptGuardGO");
            var beh  = go.AddComponent<BridgeTestBehaviour>();
            var so   = new SerializedObject(beh);
            var prop = so.FindProperty("m_Script");
            Assume.That(prop, Is.Not.Null, "m_Script property not found — skipping");

            var result = PropertyContextMenuBridge.BuildChipForProperty(prop);

            Assert.IsNull(result, "m_Script property must return null");
            Object.DestroyImmediate(go);
        }

        // Valid component property → BuildChipForProperty returns field chip
        [Test]
        public void BuildChipForProperty_ValidRigidbodyProperty_ReturnsFieldChip()
        {
            var go   = new GameObject("BridgeGO");
            var rb   = go.AddComponent<Rigidbody>();
            var so   = new SerializedObject(rb);
            var prop = FirstNonScriptProp(so);
            Assume.That(prop, Is.Not.Null, "Rigidbody has no non-script properties — skipping");

            var result = PropertyContextMenuBridge.BuildChipForProperty(prop);

            Assert.IsTrue(result.HasValue, "valid property must return a chip");
            Assert.AreEqual(ChipKindKeys.Field, result.Value.KindKey);
            StringAssert.Contains("Rigidbody", result.Value.Path);
            StringAssert.Contains(prop.name,    result.Value.Path);

            Object.DestroyImmediate(go);
        }

        private static SerializedProperty FirstNonScriptProp(SerializedObject so)
        {
            var it = so.GetIterator();
            if (!it.NextVisible(true)) return null;
            do
            {
                if (it.name != "m_Script") return it.Copy();
            }
            while (it.NextVisible(false));
            return null;
        }
    }
}
