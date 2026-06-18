// Ownership-aware texture wrapper. Dispose Owned textures on panel detach;
// never destroy Unity-shared textures (e.g. AssetPreview results).
using System;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public enum TextureOwnership
    {
        /// <summary>Texture is owned by Unity (e.g. AssetPreview.GetAssetPreview). Do not destroy.</summary>
        UnityShared,

        /// <summary>Texture was created by us; we must DestroyImmediate it when done.</summary>
        Owned
    }

    public sealed class TextureHandle : IDisposable
    {
        public Texture2D Texture { get; }
        public TextureOwnership Ownership { get; }

        public TextureHandle(Texture2D texture, TextureOwnership ownership)
        {
            Texture   = texture;
            Ownership = ownership;
        }

        public void Dispose()
        {
            if (Ownership == TextureOwnership.Owned && Texture != null)
                UnityEngine.Object.DestroyImmediate(Texture);
        }
    }
}
