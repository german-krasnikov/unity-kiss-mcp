using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Chip kind for a single serialized field of a component.
    /// Key = "field". Path = "goPath|CompType|fieldName".
    /// CanHandle = false — created programmatically from Inspector context menu.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class FieldChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.Field;
        public int      Priority         => 130;
        public string   IconName         => "d_FilterByLabel";
        public string   HexColor         => "#f59e0b";
        public string   DefaultDepth     => "summary";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        static FieldChipProvider() => ChipKindRegistry.Register(new FieldChipProvider());

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

            var so   = new SerializedObject(comp);
            var prop = so.FindProperty(fieldName);
            if (prop == null) return bracket + $"\n{fieldName}=(not found)";

            return bracket + $"\n{fieldName}={FormatProperty(prop)}";
        }

        // Test seam: replace with a mock to avoid scene queries in unit tests.
        internal static System.Func<string, GameObject> FindObjectOverride;

        private static GameObject FindObject(string path)
        {
            if (FindObjectOverride != null) return FindObjectOverride(path);
            return ComponentSerializer.FindObject(path);
        }

        private static string FormatProperty(SerializedProperty prop)
        {
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer         => prop.intValue.ToString(),
                SerializedPropertyType.Boolean         => prop.boolValue.ToString(),
                SerializedPropertyType.Float           => prop.floatValue.ToString("G"),
                SerializedPropertyType.String          => prop.stringValue,
                SerializedPropertyType.Color           => prop.colorValue.ToString(),
                SerializedPropertyType.ObjectReference => prop.objectReferenceValue != null
                    ? prop.objectReferenceValue.name : "(null)",
                SerializedPropertyType.Enum             =>
                    prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : $"(enum:{prop.enumValueIndex})",
                SerializedPropertyType.Vector2         => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3         => prop.vector3Value.ToString(),
                SerializedPropertyType.Vector4         => prop.vector4Value.ToString(),
                SerializedPropertyType.Rect            => prop.rectValue.ToString(),
                SerializedPropertyType.Quaternion      => prop.quaternionValue.eulerAngles.ToString(),
                _                                      => $"({prop.propertyType})"
            };
        }

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
