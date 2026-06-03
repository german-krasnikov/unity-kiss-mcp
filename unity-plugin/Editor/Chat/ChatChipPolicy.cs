// Pure type-allowlist — no runtime state queries, only typeof comparisons.
// Determines which dragged asset types are accepted as context chips.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatChipPolicy
    {
        /// <summary>
        /// Returns true if the asset type is allowed as a context chip.
        /// Rejects folders (DefaultAsset), null, and unlisted types.
        /// </summary>
        internal static bool IsAllowedAssetType(Type t)
        {
            if (t == null)                         return false;
            if (t == typeof(DefaultAsset))         return false; // folders
            if (t == typeof(GameObject))           return true;  // prefabs
            if (t == typeof(Material))             return true;
            if (typeof(Texture).IsAssignableFrom(t))          return true;  // Texture2D, Cubemap, Texture3D, RenderTexture
            if (t == typeof(AnimationClip))                    return true;
            if (t == typeof(MonoScript))                       return true;
            if (typeof(ScriptableObject).IsAssignableFrom(t)) return true;  // user SO assets
            return false;
        }
    }
}
