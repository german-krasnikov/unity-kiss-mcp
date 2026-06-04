// F28: CommandSchema validation — NUnit EditMode tests.
// Covers: optional params that handlers use (fix #5) + consolidated commands (fix #17).
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MCPCommandSchemaTests
    {
        // ── #5: Optional params that were previously rejected by Validate ────────

        [Test]
        public void Validate_GetHierarchy_Summary_Passes()
        {
            var err = CommandSchema.Validate("get_hierarchy", "{\"summary\":\"true\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_GetHierarchy_Incremental_Passes()
        {
            var err = CommandSchema.Validate("get_hierarchy", "{\"incremental\":\"true\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_SetProperty_DryRun_Passes()
        {
            var err = CommandSchema.Validate("set_property",
                "{\"path\":\"/Obj\",\"component\":\"Transform\",\"prop\":\"m_LocalPosition\",\"value\":\"(0,0,0)\",\"dry_run\":\"true\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_DeleteObject_Force_Passes()
        {
            var err = CommandSchema.Validate("delete_object", "{\"path\":\"/Obj\",\"force\":\"true\"}");
            Assert.IsNull(err, err);
        }

        // ── Batch path: schema validation is called by BatchHelper ───────────────

        [Test]
        public void Batch_GetHierarchy_SummaryParam_NotRejectedBySchema()
        {
            // BatchHelper calls CommandSchema.Validate before execution.
            // If "summary" were still unknown the result would contain "Unknown param".
            string result = BatchHelper.Execute(
                "get_hierarchy summary=true", "continue", 5000, atomic: false);
            Assert.IsFalse(result.Contains("Unknown param"), result);
        }

        [Test]
        public void Batch_GetHierarchy_IncrementalParam_NotRejectedBySchema()
        {
            string result = BatchHelper.Execute(
                "get_hierarchy incremental=true", "continue", 5000, atomic: false);
            Assert.IsFalse(result.Contains("Unknown param"), result);
        }

        [Test]
        public void Batch_DeleteObject_ForceParam_NotRejectedBySchema()
        {
            // No real object to delete — we just need the schema to pass; the handler
            // will return an error about the missing object, NOT an unknown-param error.
            string result = BatchHelper.Execute(
                "delete_object path=/NONEXISTENT force=true", "continue", 5000, atomic: false);
            Assert.IsFalse(result.Contains("Unknown param"), result);
        }

        // ── #17: Consolidated commands exist in schema ───────────────────────────

        [Test]
        public void Validate_Scene_ConsolidatedCommand_Passes()
        {
            var err = CommandSchema.Validate("scene", "{\"action\":\"save\",\"path\":\"test\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_Animation_ConsolidatedCommand_Passes()
        {
            var err = CommandSchema.Validate("animation", "{\"action\":\"get\",\"path\":\"/Obj\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_Timeline_ConsolidatedCommand_Passes()
        {
            var err = CommandSchema.Validate("timeline", "{\"action\":\"get\",\"path\":\"/Obj\"}");
            Assert.IsNull(err, err);
        }

        [Test]
        public void Validate_References_ConsolidatedCommand_Passes()
        {
            var err = CommandSchema.Validate("references", "{\"action\":\"get\",\"path\":\"/Obj\"}");
            Assert.IsNull(err, err);
        }

        // ── #17: Dead legacy aliases removed — no longer accepted ───────────────

        [Test]
        public void Validate_NewScene_LegacyAlias_Rejected()
        {
            var err = CommandSchema.Validate("new_scene", "{}");
            Assert.IsNotNull(err, "new_scene should be rejected (dead alias removed)");
        }

        [Test]
        public void Validate_GetAnimation_LegacyAlias_Rejected()
        {
            var err = CommandSchema.Validate("get_animation", "{\"path\":\"/Obj\"}");
            Assert.IsNotNull(err, "get_animation should be rejected (dead alias removed)");
        }

        [Test]
        public void Validate_GetReferences_LegacyAlias_Rejected()
        {
            var err = CommandSchema.Validate("get_references", "{\"path\":\"/Obj\"}");
            Assert.IsNotNull(err, "get_references should be rejected (dead alias removed)");
        }
    }
}
