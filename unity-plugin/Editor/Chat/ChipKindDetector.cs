// Pure static detector: maps a Unity Object + asset path to a ChipKind enum value.
// Detection order follows the architect plan D1. No side effects.
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ChipKindDetector
    {
        /// <summary>
        /// Detect the ChipKind for a dragged/context-added object.
        /// <param name="obj">The Unity Object (may be null).</param>
        /// <param name="assetPath">AssetDatabase path — empty/null for scene objects.</param>
        /// </summary>
        internal static ChipKind Detect(Object obj, string assetPath)
        {
            if (obj == null) return ChipKind.Asset;

            // 1. Scene hierarchy GameObject (not an asset).
            if (obj is GameObject go && !AssetDatabase.Contains(go))
                return ChipKind.Hierarchy;

            // 2. .unity scene asset.
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".unity"))
                return ChipKind.Scene;

            // 3. Script asset.
            if (obj is MonoScript)
                return ChipKind.Script;

            // 4. Prefab asset.
            if (!string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab"))
                return ChipKind.Prefab;

            // 5. Material.
            if (obj is Material)
                return ChipKind.Material;

            // 6. Texture (covers Texture2D, RenderTexture, etc.).
            if (obj is Texture)
                return ChipKind.Texture;

            // 7. ScriptableObject (catches SO sub-classes).
            if (obj is ScriptableObject)
                return ChipKind.ScriptableObject;

            // 8. Fallback.
            return ChipKind.Asset;
        }

        /// <summary>
        /// The short lowercase kind prefix used in the serialized bracket format.
        /// E.g. ChipKind.ScriptableObject → "so".
        /// </summary>
        internal static string ShortPrefix(ChipKind kind)
        {
            switch (kind)
            {
                case ChipKind.Hierarchy:       return "hierarchy";
                case ChipKind.Scene:           return "scene";
                case ChipKind.Script:          return "script";
                case ChipKind.Prefab:          return "prefab";
                case ChipKind.Material:        return "material";
                case ChipKind.Texture:         return "texture";
                case ChipKind.ScriptableObject: return "so";
                default:                       return "asset";
            }
        }
    }
}
