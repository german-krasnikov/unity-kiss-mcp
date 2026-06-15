// Minimal chip helpers for CLI-layer tests (no View dependency).
// H() and S() build ChipData values for chip sequence assertions.
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal static class ChipTestHelpers
    {
        internal static ChipData H(string path, string name, int id = 0)
            => new ChipData(ChipKindKeys.Hierarchy, path, name, id);

        internal static ChipData S(string path, string name)
            => new ChipData(ChipKindKeys.Script, path, name, 0);
    }
}
