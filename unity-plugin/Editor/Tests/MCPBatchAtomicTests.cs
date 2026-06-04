// F27: Atomic batch rollback — NUnit EditMode tests.
// Run manually in Unity Test Runner (Window > General > Test Runner > EditMode).
// These tests CANNOT run in CI (disconnected from Unity). See project memory note.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPBatchAtomicTests
    {
        // ── 1. All succeed — objects present, no rollback ─────────────────────
        [Test]
        public void Atomic_AllSucceed_AllObjectsPresent()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicA\ncreate_object name=AtomicB",
                "continue", 25000, atomic: true);

            Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback expected on success");
            Assert.IsTrue(result.Contains("ok:2"), "Both ops should succeed");

            var a = GameObject.Find("AtomicA");
            var b = GameObject.Find("AtomicB");
            Assert.IsNotNull(a, "AtomicA should exist");
            Assert.IsNotNull(b, "AtomicB should exist");

            // Cleanup
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
        }

        // ── 2. Op1 succeeds, Op2 fails — everything reverted ─────────────────
        [Test]
        public void Atomic_Op2Fails_AllReverted_SceneUnchanged()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicC\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: true);

            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Rollback expected");
            Assert.IsTrue(result.Contains("err:1"), "Should report 1 error");

            var c = GameObject.Find("AtomicC");
            Assert.IsNull(c, "AtomicC should be reverted (not present in scene)");
        }

        // ── 3. Non-atomic — partial apply on failure ──────────────────────────
        [Test]
        public void NonAtomic_Op2Fails_PartialApplied()
        {
            string result = BatchHelper.Execute(
                "create_object name=AtomicD\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: false);

            Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback in non-atomic mode");

            var d = GameObject.Find("AtomicD");
            Assert.IsNotNull(d, "AtomicD should remain (partial apply in non-atomic mode)");

            // Cleanup
            Object.DestroyImmediate(d);
        }

        // ── 4. Nested batch: outer atomic reverts inner's work too ────────────
        [Test]
        public void Atomic_NestedBatch_OuterReverts()
        {
            // Outer atomic batch: inner batch creates E (succeeds), then outer fails
            // The inner batch text must be escaped for embedding; use a simple approach:
            // outer op0: create F via inner batch (nested)
            // outer op1: fail via invalid set_property
            // Result: F should be gone
            string innerBatch = "create_object name=AtomicE";
            string result = BatchHelper.Execute(
                $"batch commands=\"{innerBatch}\"\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)",
                "continue", 25000, atomic: true);

            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Outer rollback expected");

            var e = GameObject.Find("AtomicE");
            Assert.IsNull(e, "AtomicE (created by nested batch) should be reverted by outer");
        }

        // ── 5. Read-only atomic batch — no exception, no spurious undo entry ──
        [Test]
        public void Atomic_ReadOnlyBatch_NoOp()
        {
            // ping is a read command — should succeed cleanly with no rollback
            Assert.DoesNotThrow(() =>
            {
                string result = BatchHelper.Execute(
                    "ping\nping",
                    "continue", 25000, atomic: true);
                Assert.IsFalse(result.Contains("ATOMIC_ROLLBACK"), "No rollback for read-only batch");
                Assert.IsTrue(result.Contains("ok:2"), "Both pings should succeed");
            });
        }

        // ── 6. atomic=true overrides on_error=continue (still stops+reverts) ──
        [Test]
        public void Atomic_OverridesOnErrorContinue()
        {
            // With on_error=continue normally we'd keep going, but atomic overrides
            string result = BatchHelper.Execute(
                "create_object name=AtomicF\nset_property path=/NONEXISTENT component=Transform prop=m_LocalPosition value=(0,0,0)\ncreate_object name=AtomicG",
                "continue", 25000, atomic: true);

            // AtomicG (op2) must NOT have been created — atomic stopped at op1 failure
            Assert.IsTrue(result.Contains("ATOMIC_ROLLBACK"), "Rollback expected");
            Assert.IsFalse(result.Contains("[2]"), "Op2 should not have been executed");

            var f = GameObject.Find("AtomicF");
            var g = GameObject.Find("AtomicG");
            Assert.IsNull(f, "AtomicF should be reverted");
            Assert.IsNull(g, "AtomicG should never have been created");
        }
    }
}
