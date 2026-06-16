// TDD — Issue #1: Model selector dropdown. GPT-5.x update.
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
            var s = System.Array.Find(MCPChatWindow.ModelPresets, p => p.label == "Sonnet 4.6");
            Assert.AreEqual("claude-sonnet-4-6", s.modelId);
        }

        [Test]
        public void ModelPresets_OpusEntry_HasCorrectId()
        {
            var o = System.Array.Find(MCPChatWindow.ModelPresets, p => p.label == "Opus 4.8");
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
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Sonnet 4.6" && p.modelId == "claude-sonnet-4-6"));
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Opus 4.8"   && p.modelId == "claude-opus-4-8"));
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
        public void PresetsForKind_UnknownKind_ReturnsDefaultAndCustom()
        {
            var method = typeof(MCPChatWindow).GetMethod(
                "PresetsForKind",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "PresetsForKind must exist as private static on MCPChatWindow");
            var result = ((string label, string modelId)[])method.Invoke(null, new object[] { (BackendKind)99 });
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Default",   result[0].label);
            Assert.AreEqual("Custom...", result[1].label);
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
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Fable 5" && p.modelId == "claude-fable-5"));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasHaikuEntry()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Haiku 4.5" && p.modelId == "claude-haiku-4-5"));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasOpus47Entry()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Opus 4.7" && p.modelId == "claude-opus-4-7"));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasOpus46Entry()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "Opus 4.6" && p.modelId == "claude-opus-4-6"));
        }

        [Test]
        public void ModelPresetsPerKind_Claude_FableIsFirstNamed()
        {
            // Default is [0], Fable 5 (most powerful) must be [1]
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.AreEqual("claude-fable-5", presets[1].modelId, "Fable 5 must be first named model");
        }

        [Test]
        public void ModelPresetsPerKind_Claude_HasAtLeast6NamedEntries()
        {
            // Default + 6 named + Custom = 8 minimum
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Claude];
            Assert.GreaterOrEqual(presets.Length, 8);
        }

        [Test]
        public void ApplySelectedModel_CustomSentinel_ReturnsSameStore()
        {
            var store  = new BackendConfigStore();
            var result = MCPChatWindow.ApplySelectedModel(store, BackendKind.Claude, "__custom__");
            Assert.AreSame(store, result, "sentinel must not be passed to CLI");
        }

        // ── Codex extended model list ─────────────────────────────────────────

        [Test]
        public void ModelPresetsPerKind_Codex_HasGpt55()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "GPT-5.5" && p.modelId == "gpt-5.5"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasGpt54()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "GPT-5.4" && p.modelId == "gpt-5.4"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasGpt54Mini()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "GPT-5.4 Mini" && p.modelId == "gpt-5.4-mini"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_Gpt55IsFirstNamed()
        {
            // Default is [0], GPT-5.5 (most powerful) must be [1]
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.AreEqual("gpt-5.5", presets[1].modelId, "GPT-5.5 must be first named Codex model");
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasGpt41Mini()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "GPT-4.1 Mini" && p.modelId == "gpt-4.1-mini"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasGpt4o()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "GPT-4o" && p.modelId == "gpt-4o"));
        }

        [Test]
        public void ModelPresetsPerKind_Codex_HasAtLeast9NamedEntries()
        {
            // Default + 9 models + Custom... = 11 minimum
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            Assert.GreaterOrEqual(presets.Length, 11);
        }

        [Test]
        public void ModelPresetsPerKind_Codex_CustomIsLast()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Codex];
            var last    = presets[presets.Length - 1];
            Assert.AreEqual("Custom...", last.label);
            Assert.AreEqual("__custom__", last.modelId);
        }

        // ── Gemini expanded model list ────────────────────────────────────────

        [Test]
        public void ModelPresetsPerKind_Gemini_Has35Flash()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.IsNotNull(System.Array.Find(presets, p => p.label == "3.5 Flash" && p.modelId == "gemini-3.5-flash"));
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_Has3ProPreview()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.IsNotNull(System.Array.Find(presets, p => p.modelId == "gemini-3-pro-preview"));
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_Has25FlashLite()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.IsNotNull(System.Array.Find(presets, p => p.modelId == "gemini-2.5-flash-lite"));
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_HasAtLeast7Entries()
        {
            // Default + 7 models + Custom = 9 minimum
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.GreaterOrEqual(presets.Length, 9);
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_NoDeadModel_20Flash()
        {
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.IsFalse(System.Array.Exists(presets, p => p.modelId == "gemini-2.0-flash"),
                "gemini-2.0-flash was shutdown June 1, 2026 — must not appear in presets");
        }

        [Test]
        public void ModelPresetsPerKind_Gemini_35FlashIsFirstNamed()
        {
            // Default is [0], 3.5 Flash (most powerful GA) must be [1]
            var presets = MCPChatWindow.ModelPresetsPerKind[BackendKind.Gemini];
            Assert.AreEqual("gemini-3.5-flash", presets[1].modelId, "3.5 Flash must be first named Gemini model");
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
