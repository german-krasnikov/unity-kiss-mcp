// Extension → IAssetViewer registry. Dispatches asset chip navigation to the right viewer.
// [InitializeOnLoad] registers built-in viewers and wires the ViewerLauncher seam.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class AssetViewerFactory
    {
        private static readonly Dictionary<string, IAssetViewer> _registry =
            new Dictionary<string, IAssetViewer>(StringComparer.OrdinalIgnoreCase);

        static AssetViewerFactory()
        {
            RegisterBuiltIns();
            AssetChipProviderBase.ViewerLauncher = TryShow;
        }

        private static void RegisterBuiltIns()
        {
            var model = new ModelViewerWindow.ViewerAdapter();
            Register(".fbx",   model);
            Register(".obj",   model);
            Register(".blend", model);
            Register(".dae",   model);

            var sprite = new SpriteViewerWindow.ViewerAdapter();
            Register(".png",  sprite);
            Register(".jpg",  sprite);
            Register(".jpeg", sprite);
            Register(".tga",  sprite);
            Register(".exr",  sprite);

            var audio = new AudioViewerWindow.ViewerAdapter();
            Register(".wav",  audio);
            Register(".mp3",  audio);
            Register(".ogg",  audio);
            Register(".aiff", audio);

            // C9: register prefab viewer via factory (fixes C9 + C10)
            Register(".prefab", new PrefabViewerWindow.ViewerAdapter());
        }

        internal static void Register(string ext, IAssetViewer viewer)
        {
            if (string.IsNullOrEmpty(ext)) return;
            _registry[ext.ToLowerInvariant()] = viewer;
        }

        internal static void Unregister(string ext)
        {
            if (!string.IsNullOrEmpty(ext))
                _registry.Remove(ext.ToLowerInvariant());
        }

        internal static IAssetViewer ForPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var ext = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(ext)) return null;
            _registry.TryGetValue(ext.ToLowerInvariant(), out var viewer);
            return viewer;
        }

        internal static bool TryShow(string assetPath)
        {
            var viewer = ForPath(assetPath);
            if (viewer == null) return false;
            viewer.Show(assetPath);
            return true;
        }

        // Test seam: clear all registrations.
        internal static void Reset()
        {
            _registry.Clear();
            AssetChipProviderBase.ViewerLauncher = null;
        }
    }
}
