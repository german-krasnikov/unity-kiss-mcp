// Public interface for extensible chip kind providers.
// Third-party assemblies implement this and call ChipKindRegistry.Register().
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Defines a chip kind: detection, display, payload formatting, and navigation.
    /// Implement this and call ChipKindRegistry.Register() from [InitializeOnLoad].
    /// </summary>
    public interface IChipKindProvider
    {
        /// <summary>Unique lowercase key, e.g. "hierarchy". Must match ^[a-z0-9_]+$.</summary>
        string Key { get; }

        /// <summary>Lower = checked first. Built-ins use 100–800. Asset = int.MaxValue.</summary>
        int Priority { get; }

        /// <summary>True if this provider handles the given object/path pair.</summary>
        bool CanHandle(Object obj, string assetPath);

        /// <summary>Create a ChipData for the given object.</summary>
        ChipData Create(Object obj, string assetPath);

        /// <summary>EditorGUIUtility.IconContent key for the pill icon.</summary>
        string IconName { get; }

        /// <summary>Hex color string for the pill and response tag, e.g. "#4a9eff".</summary>
        string HexColor { get; }

        /// <summary>
        /// Format the AI-facing bracket text for this chip.
        /// Core pre-resolves summary into ctx before calling this.
        /// </summary>
        string FormatPayload(ChipData chip, ChipPayloadContext ctx);

        /// <summary>Fallback depth when ChipConfig has no explicit entry for this key.</summary>
        string DefaultDepth { get; }

        /// <summary>Handle a click on a chip link. Called with the reference string from the linkId.</summary>
        void Navigate(string reference);
    }
}
