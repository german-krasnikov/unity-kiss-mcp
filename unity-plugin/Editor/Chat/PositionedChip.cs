// PositionedChip: a chip pinned to a text position.
// TextOffset = char index in TextField.value at the moment of insertion.
// Fixes Bug 3: two identical-name chips have different TextOffset → distinguishable.
namespace UnityMCP.Editor.Chat
{
    /// <summary>A chip with a known insertion position in the text field.</summary>
    internal readonly struct PositionedChip
    {
        internal readonly ChipData Chip;
        internal readonly int      TextOffset;

        internal PositionedChip(ChipData chip, int textOffset)
        {
            Chip       = chip;
            TextOffset = textOffset;
        }
    }
}
