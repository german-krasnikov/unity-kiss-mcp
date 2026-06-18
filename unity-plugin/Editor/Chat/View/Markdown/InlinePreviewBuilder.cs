// Simplified compatibility shim: delegates preview construction to PreviewBuilderRegistry.
// Static seams are consumed by the individual IPreviewBuilder implementations for test injection.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class InlinePreviewBuilder
    {
        /// <summary>Seam: replaces file/AssetDatabase texture loading in ImagePreviewBuilder.</summary>
        internal static Func<string, Texture2D> TextureLoader;

        /// <summary>Seam: replaces AssetPreview.GetAssetPreview in AssetPreviewBuilder.</summary>
        internal static Func<UnityEngine.Object, Texture2D> AssetPreviewLoader;

        /// <summary>Seam: replaces AssetDatabase.LoadAssetAtPath&lt;AudioClip&gt; in AudioPreviewBuilder.</summary>
        internal static Func<string, (float length, int frequency, int channels)?> AudioClipLoader;

        /// <summary>Delegates to the preview registry. Returns null if no builder handles the request.</summary>
        internal static VisualElement Build(string kindKey, string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(kindKey)) return null;
            var request = new PreviewRequest(kindKey, path);
            return PreviewBuilderRegistry.Build(request, PreviewLifetimeScope.Current);
        }
    }
}
