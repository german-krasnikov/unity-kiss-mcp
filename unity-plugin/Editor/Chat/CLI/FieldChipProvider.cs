using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Chip kind for a single serialized field of a component.
    /// Key = "field". Path = "goPath|CompType|fieldName".
    /// CanHandle = false — created programmatically from Inspector context menu.
    /// Registered via ChipKindRegistry.EnsureBuiltIns().
    /// </summary>
    internal sealed class FieldChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Field;
        public int      Priority         => 130;
        public string   IconName         => "d_FilterByLabel";
        public string   HexColor         => "#f59e0b";
        public string   DefaultDepth     => "summary";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool     CanHandle(Object obj, string assetPath) => false;
        public ChipData Create(Object obj, string assetPath)    => default;

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";

            var bracket = $"[{Key}:{chip.Path}]";
            if (ctx.Depth == "path") return bracket;

            var parts = chip.Path?.Split('|');
            if (parts == null || parts.Length < 3) return bracket + "\n(invalid field path)";

            var goPath    = parts[0];
            var compType  = parts[1];
            var fieldName = parts[2];

            var go = FindObject(goPath);
            if (go == null) return bracket + $"\n{fieldName}=(object not found)";

            var comp = go.GetComponent(compType);
            if (comp == null) return bracket + $"\n{fieldName}=(component not found)";

            using var so = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return bracket + $"\n{fieldName}=(not found)";

            return bracket + $"\n{fieldName}={ChipPropertyFormatter.Format(prop)}";
        }

        // Test seam: replace with a mock to avoid scene queries in unit tests.
        internal static System.Func<string, GameObject> FindObjectOverride;

        private static GameObject FindObject(string path)
            => FindObjectOverride != null ? FindObjectOverride(path) : ComponentSerializer.FindObject(path);

        public void Navigate(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return;
            var parts = reference.Split('|');
            var go    = FindObject(parts[0]);
            if (go != null) Selection.activeGameObject = go;
        }

        public void Ping(string reference) => Navigate(reference);

        public void AppendContextMenuItems(DropdownMenu menu, string reference) { }
    }
}
