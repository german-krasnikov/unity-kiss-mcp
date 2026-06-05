// Thin facade: maps a Unity Object + asset path to a KindKey string via the registry.
// ShortPrefix removed — the Key IS the prefix (H6).
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal static class ChipKindDetector
    {
        /// <summary>
        /// Detect the chip kind key for a dragged/context-added object.
        /// Delegates to ChipKindRegistry.Resolve. Returns ChipKindKeys.Asset on no match.
        /// </summary>
        internal static string Detect(Object obj, string assetPath)
            => ChipKindRegistry.Resolve(obj, assetPath)?.Key ?? ChipKindKeys.Asset;
    }
}
