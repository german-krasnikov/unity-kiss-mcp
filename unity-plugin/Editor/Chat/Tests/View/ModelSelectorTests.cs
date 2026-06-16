// TDD — Issue #1: Model selector dropdown.
// Tests MCPChatWindow.ModelPresets, CloneWithModel, ModelPresetsPerKind, ApplySelectedModel.
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ModelSelectorTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Claude");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Codex");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Gemini");
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Claude");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Codex");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Gemini");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Claude.custom");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Codex.custom");
            EditorPrefs.DeleteKey("MCPChat.SelectedModel.Gemini.custom");
        }

        // ── ModelPresets backward-compat alias ────────────────────────────────

        [Test]
        public void ModelPresets_DefaultEntry_HasEmptyModelId()
        {
            var def = MCPChatWindow.ModelPresets[0];
            Assert.AreEqual("Default", def.label);
            Assert.AreEqual("",        def.modelId);
        }

        [Test]
        public void ModelPresets_SonnetEntry_HasCorrectId()
        {
            var s = System.Array.Find(MCPChatWindow.ModelPresets, p => p.label == "Sonnet");
            Assert.AreEqual("claude-sonnet-4-6", s.modelId);
        }

        [Test]
        public void ModelPresets_OpusEntry_HasCorrectId()
        {
            var o = System.Array.Find(MCPChatWindow.ModelPresets, p => p.label == "Opus");
            Assert.AreEqual("claude-opus-4-8", o.modelId);
        }

        [Test]
        public void ModelPresets_AllEntriesHaveLabels()
        {
            foreach (var p in MCPChatWindow.ModelPresets)
                Assert.IsFalse(string.IsNullOrEmpty(p.label));
        }

        [Test]
        public void ModelPref_CanRoundTrip()
        {
            EditorPrefs.SetString("MCPChat.SelectedModel.Claude", "Opus");
            Assert.AreEqual("Opus", EditorPrefs.GetString("MCPChat.SelectedModel.Claude", ""));
        }

        // ── ModelPresetsPerKind ───────────────────────────────────────────────

        [Test]
        public void ModelPresetsPerKind_HasAllBackendKinds()
        {
            Assert.IsTrue(MCPChatWindow.ModelPresetsPerKind.ContainsKey(BackendKind.Claude));
            Assert.IsTrue(MCPChatWindow.ModelPresetsPerKind.ContainsKey(BackendKind.Codex));
            Assert.IsTrue(MCPChatWindow.ModelPresetsPerKind.ContainsKey(BackendKind.Gemini));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasSonnetAndOpus()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Sonnet" && p.modelId == "claude-sonnet-4-6"));
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Opus"   && p.modelId == "claude-opus-4-8"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasO4MiniAndO3()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "o4-mini" && p.modelId == "o4-mini"));
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "o3"      && p.modelId == "o3"));
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_HasFlashAndPro()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "2.5 Flash" && p.modelId == "gemini-2.5-flash"));
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "2.5 Pro"   && p.modelId == "gemini-2.5-pro"));
        }

        // ── PresetsForKind (private static via reflection) ────────────────────

        [Test]
        public void PresetsForKind_UnknownKind_ReturnsDefault()
        {
            var method = typeof(MCPChatWindow).GetMethod(
                "PresetsForKind",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "PresetsForKind must exist as private static on MCPChatWindow");
            var result = ((string label, string modelId)[])method.Invoke(null, new object[] { (BackendKind)99 });
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("Default", result[0].label);
        }

        // ── ApplySelectedModel ────────────────────────────────────────────────

        [Test]
        public void ApplySelectedModel_EmptyModel_ReturnsSameStore()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Claude, "");
            Assert.AreSame(store, result);
        }

        [Test]
        public void ApplySelectedModel_Claude_PatchesClaude()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Claude, "claude-opus-4-8");
            Assert.AreEqual("claude-opus-4-8", result.Claude.Model);
            // other backends unchanged
            Assert.AreEqual(store.Codex.Model,  result.Codex.Model);
            Assert.AreEqual(store.Gemini.Model, result.Gemini.Model);
        }

        [Test]
        public void ApplySelectedModel_Codex_PatchesCodex()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Codex, "o3");
            Assert.AreEqual("o3", result.Codex.Model);
            Assert.AreEqual(store.Claude.Model, result.Claude.Model);
        }

        [Test]
        public void ApplySelectedModel_Gemini_PatchesGemini()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Gemini, "gemini-2.5-pro");
            Assert.AreEqual("gemini-2.5-pro", result.Gemini.Model);
            Assert.AreEqual(store.Claude.Model, result.Claude.Model);
        }

        [Test]
        public void ApplySelectedModel_Claude_SameModel_ReturnsSameInstance()
        {
            var store = new BackendConfigStore();
            store.Claude.Model = "claude-opus-4-8";
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Claude, "claude-opus-4-8");
            Assert.AreSame(store, result);
        }

        // ── Custom sentinel ───────────────────────────────────────────────────

        [Test]
        public void ModelPresetsPerKind_AllKinds_HaveCustomEntry()
        {
            foreach (var kind in new[] { BackendKind.Claude, BackendKind.Codex, BackendKind.Gemini })
            {
                var presets = MCPChatWindow.ModelPresetsPerKind[kind];
                var last    = presets[presets.Length - 1];
                Assert.AreEqual("Custom...", last.label,   $"{kind}: last entry must be Custom...");
                Assert.AreEqual("__custom__", last.modelId, $"{kind}: Custom... must have __custom__ id");
            }
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasFableEntry()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Fable" && p.modelId == "claude-fable-5"));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasHaikuEntry()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Haiku" && p.modelId == "claude-haiku-4-5"));
        }

        [Test]
        public void ApplySelectedModel_CustomSentinel_ReturnsSameStore()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Claude, "__custom__");
            Assert.AreSame(store, result, "sentinel must not be passed to CLI");
        }

        // ── CloneWithModel backward-compat ────────────────────────────────────

        [Test]
        public void CloneWithModel_DifferentModel_ReturnsNewStore()
        {
            var src = new BackendConfigStore();
            src.Claude.Model = "old";
            var cloned = MCPChatWindow.CloneWithModel(src, "new-model");
            Assert.AreEqual("new-model", cloned.Claude.Model);
            Assert.AreEqual(src.Claude.ExtraArgs, cloned.Claude.ExtraArgs);
        }

        [Test]
        public void CloneWithModel_SameModel_ReturnsSameInstance()
        {
            var src = new BackendConfigStore();
            src.Claude.Model = "same";
            var result = MCPChatWindow.CloneWithModel(src, "same");
            Assert.AreSame(src, result);
        }

        [Test]
        public void CloneWithModel_PreservesPermissionMode()
        {
            var src = new BackendConfigStore();
            src.Claude.PermissionMode = "acceptEdits";
            src.Claude.Model = "old";
            var cloned = MCPChatWindow.CloneWithModel(src, "new-model");
            Assert.AreEqual("acceptEdits", cloned.Claude.PermissionMode);
        }
    }
}
