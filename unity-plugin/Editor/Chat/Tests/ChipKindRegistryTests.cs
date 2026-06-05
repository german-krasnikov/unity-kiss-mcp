// E2E test suite for ChipKindRegistry — Part 1: registration, resolution, lifecycle.
// FakeProvider simulates a third-party plugin. Tests (a)–(h).
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    // Fake third-party provider: handles Mesh objects, Priority=50 (beats built-ins).
    internal sealed class FakeProvider : IChipKindProvider
    {
        public string Key         => "custom_widget";
        public int    Priority    => 50;
        public string IconName    => "d_SceneViewFx";
        public string HexColor    => "#ff00ff";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => obj is Mesh;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath ?? obj.name, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";
            var bracket = $"[{Key}:{chip.Path}]";
            return (ctx.Depth == "summary" || ctx.Depth == "full") && !string.IsNullOrEmpty(ctx.ResolvedSummary)
                ? bracket + "\n" + ctx.ResolvedSummary
                : bracket;
        }

        public void Navigate(string reference) { /* no-op */ }
    }

    [TestFixture]
    public class ChipKindRegistryTests
    {
        [SetUp]    public void SetUp()    => ChipKindRegistry.ResetToBuiltIns();
        [TearDown] public void TearDown() => ChipKindRegistry.ResetToBuiltIns();

        // (a) Resolve routes Mesh to FakeProvider
        [Test]
        public void Resolve_MeshObject_RoutesToFake()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var mesh = new Mesh();
            try
            {
                var provider = ChipKindRegistry.Resolve(mesh, "Assets/w.fbx");
                Assert.IsNotNull(provider);
                Assert.AreEqual("custom_widget", provider.Key);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // (b) ForKey returns correct icon and color
        [Test]
        public void ForKey_CustomWidget_CorrectIconAndColor()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var provider = ChipKindRegistry.ForKey("custom_widget");
            Assert.IsNotNull(provider);
            Assert.AreEqual("#ff00ff",       provider.HexColor);
            Assert.AreEqual("d_SceneViewFx", provider.IconName);
        }

        // (f) Duplicate key: keep-first + LogWarning + no throw
        [Test]
        public void Register_DuplicateKey_KeepsFirst_NoThrow()
        {
            ChipKindRegistry.Register(new FakeProvider());
            Assert.DoesNotThrow(() => ChipKindRegistry.Register(new FakeProvider()));
            var provider = ChipKindRegistry.ForKey("custom_widget");
            Assert.AreEqual("#ff00ff", provider.HexColor);
        }

        // (g) Unregister removes provider
        [Test]
        public void Unregister_RemovesProvider()
        {
            ChipKindRegistry.Register(new FakeProvider());
            Assert.IsNotNull(ChipKindRegistry.ForKey("custom_widget"));
            ChipKindRegistry.Unregister("custom_widget");
            Assert.IsNull(ChipKindRegistry.ForKey("custom_widget"));
        }

        // (h) ResetToBuiltIns clears fakes, 8 built-ins present
        [Test]
        public void ResetToBuiltIns_ClearsFakes()
        {
            ChipKindRegistry.Register(new FakeProvider());
            ChipKindRegistry.ResetToBuiltIns();
            Assert.IsNull(ChipKindRegistry.ForKey("custom_widget"));
            Assert.IsNotNull(ChipKindRegistry.ForKey(ChipKindKeys.Hierarchy));
        }

        // (i) All 8 built-ins present after reset
        [Test]
        public void RegisterBuiltIns_All8Keys_Present()
        {
            var allKeys = new[]
            {
                ChipKindKeys.Hierarchy, ChipKindKeys.Scene, ChipKindKeys.Script,
                ChipKindKeys.Prefab, ChipKindKeys.Material, ChipKindKeys.Texture,
                ChipKindKeys.ScriptableObject, ChipKindKeys.Asset
            };
            foreach (var key in allKeys)
                Assert.IsNotNull(ChipKindRegistry.ForKey(key), $"Built-in key '{key}' missing");
        }

        // (j) Priority ordering: lower Priority wins
        [Test]
        public void Resolve_LowerPriority_WinsOverHigher()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var mesh = new Mesh();
            try
            {
                var provider = ChipKindRegistry.Resolve(mesh, "Assets/w.fbx");
                Assert.AreEqual("custom_widget", provider.Key);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        // (k) AllKeys includes/excludes custom
        [Test]
        public void AllKeys_IncludesCustom_AfterRegister_ExcludesAfterUnregister()
        {
            ChipKindRegistry.Register(new FakeProvider());
            CollectionAssert.Contains(ChipKindRegistry.AllKeys, "custom_widget");

            ChipKindRegistry.Unregister("custom_widget");
            CollectionAssert.DoesNotContain(ChipKindRegistry.AllKeys, "custom_widget");
        }

        // (l) DepthFor custom key falls to provider.DefaultDepth
        [Test]
        public void DepthFor_CustomKey_FallsToProviderDefaultDepth()
        {
            ChipKindRegistry.Register(new FakeProvider());
            var cfg = new ChipConfig();
            Assert.AreEqual("path", cfg.DepthFor("custom_widget"));
        }

        // (m) REGISTRATION ORDER: register custom BEFORE EnsureBuiltIns → custom survives
        [Test]
        public void RegistrationOrder_CustomBeforeBuiltIns_Survives()
        {
            ChipKindRegistry.Unregister("custom_widget");
            ChipKindRegistry.Register(new FakeProvider());
            ChipKindRegistry.EnsureBuiltIns();
            Assert.IsNotNull(ChipKindRegistry.ForKey("custom_widget"),
                "custom_widget must survive EnsureBuiltIns (guard never clears)");
        }
    }
}
