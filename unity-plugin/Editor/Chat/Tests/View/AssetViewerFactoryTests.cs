// TDD — AssetViewerFactory + ViewerLauncher seam tests (headless, EditMode).
// All tests headless-safe: no AssetDatabase, no EditorWindow calls.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Chat.Tests
{
    // Fake viewer that records calls.
    internal sealed class FakeViewer : IAssetViewer
    {
        public readonly List<string> Shown = new List<string>();
        public void Show(string assetPath) => Shown.Add(assetPath);
    }

    [TestFixture]
    public class AssetViewerFactoryTests
    {
        [SetUp]
        public void SetUp() => AssetViewerFactory.Reset();

        [TearDown]
        public void TearDown() => AssetViewerFactory.Reset();

        [Test]
        public void Register_ThenForPath_ReturnsViewer()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".fbx", fake);

            var viewer = AssetViewerFactory.ForPath("Assets/Char.fbx");

            Assert.AreSame(fake, viewer);
        }

        [Test]
        public void ForPath_CaseInsensitive()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".fbx", fake);

            Assert.AreSame(fake, AssetViewerFactory.ForPath("Assets/Char.FBX"));
            Assert.AreSame(fake, AssetViewerFactory.ForPath("Assets/Char.Fbx"));
        }

        [Test]
        public void ForPath_UnknownExtension_ReturnsNull()
        {
            var result = AssetViewerFactory.ForPath("Assets/Char.unknown_ext_xyz");
            Assert.IsNull(result);
        }

        [Test]
        public void TryShow_RegisteredExt_ReturnsTrue()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".mp3", fake);

            bool result = AssetViewerFactory.TryShow("Assets/sound.mp3");

            Assert.IsTrue(result);
            Assert.AreEqual(1, fake.Shown.Count);
        }

        [Test]
        public void TryShow_UnregisteredExt_ReturnsFalse()
        {
            bool result = AssetViewerFactory.TryShow("Assets/file.unknownxyz");
            Assert.IsFalse(result);
        }

        [Test]
        public void TryShow_CallsShowWithCorrectPath()
        {
            var fake = new FakeViewer();
            AssetViewerFactory.Register(".wav", fake);
            const string path = "Assets/Audio/beep.wav";

            AssetViewerFactory.TryShow(path);

            Assert.AreEqual(path, fake.Shown[0]);
        }

        [Test]
        public void Register_OverridesExisting_KeepsLast()
        {
            var first  = new FakeViewer();
            var second = new FakeViewer();
            AssetViewerFactory.Register(".obj", first);
            AssetViewerFactory.Register(".obj", second);

            var viewer = AssetViewerFactory.ForPath("mesh.obj");

            Assert.AreSame(second, viewer);
        }
    }

    [TestFixture]
    public class ViewerLauncherSeamTests
    {
        [SetUp]
        public void SetUp()
        {
            AssetViewerFactory.Reset();
            AssetChipProviderBase.ViewerLauncher = null;
        }

        [TearDown]
        public void TearDown()
        {
            AssetViewerFactory.Reset();
            AssetChipProviderBase.ViewerLauncher = null;
        }

        [Test]
        public void ViewerLauncher_Set_CalledOnNavigate()
        {
            int callCount = 0;
            string capturedPath = null;
            AssetChipProviderBase.ViewerLauncher = path =>
            {
                callCount++;
                capturedPath = path;
                return true;
            };

            var provider = new ModelChipProvider();
            provider.Navigate("Assets/Char.fbx");

            Assert.AreEqual(1, callCount);
            Assert.AreEqual("Assets/Char.fbx", capturedPath);
        }

        [Test]
        public void ModelChipProvider_CanHandle_Fbx()
        {
            var provider = new ModelChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.fbx"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.FBX"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.obj"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.blend"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/Char.dae"));
        }

        [Test]
        public void ModelChipProvider_CanHandle_RejectsPng()
        {
            var provider = new ModelChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "Assets/tex.png"));
            Assert.IsFalse(provider.CanHandle(null, "Assets/mat.mat"));
        }

        [Test]
        public void AudioChipProvider_CanHandle_AudioExts()
        {
            var provider = new AudioChipProvider();
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.wav"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.mp3"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.ogg"));
            Assert.IsTrue(provider.CanHandle(null, "Assets/music.aiff"));
        }

        [Test]
        public void AudioChipProvider_CanHandle_RejectsFbx()
        {
            var provider = new AudioChipProvider();
            Assert.IsFalse(provider.CanHandle(null, "Assets/model.fbx"));
        }

        [Test]
        public void AudioChipProvider_Priority_BetweenMaterialAndTexture()
        {
            // Material=500, Texture=600 → Audio should be between
            var audio = new AudioChipProvider();
            Assert.Greater(audio.Priority, 500);
            Assert.Less(audio.Priority, 600);
        }

        [Test]
        public void ModelChipProvider_Priority_BetweenPrefabAndMaterial()
        {
            // Prefab=400, Material=500 → Model should be between
            var model = new ModelChipProvider();
            Assert.Greater(model.Priority, 400);
            Assert.Less(model.Priority, 500);
        }
    }

    [TestFixture]
    public class AudioUtilProxyTests
    {
        [Test]
        public void TypeLookup_DoesNotThrow()
        {
            // Verifies reflection init doesn't crash in Editor environment.
            Assert.DoesNotThrow(() => AudioUtilProxy.IsAvailable.ToString());
        }

        [Test]
        public void Stop_WhenClipNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioUtilProxy.Stop());
        }

        [Test]
        public void Pause_WhenClipNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => AudioUtilProxy.Pause());
        }

        [Test]
        public void GetPosition_WhenClipNull_ReturnsZero()
        {
            float result = AudioUtilProxy.GetPosition(null);
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void GetWaveform_WhenClipNull_ReturnsNull()
        {
            var result = AudioUtilProxy.GetWaveform(null, 128, 32);
            Assert.IsNull(result);
        }
    }
}
