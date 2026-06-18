// AssetPostprocessor proxy for ChipExistenceService.
// Forwards OnPostprocessAllAssets events so the service can invalidate its cache.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChipExistenceAssetPostprocessor : AssetPostprocessor
    {
        internal static event Action<string[], string[], string[]> OnAssetsChanged;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            try { OnAssetsChanged?.Invoke(importedAssets, deletedAssets, movedAssets); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
