using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    internal static class ChipPropertyFormatter
    {
        internal static string Format(SerializedProperty prop)
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
                SerializedPropertyType.Enum            =>
                    prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : $"(enum:{prop.enumValueIndex})",
                SerializedPropertyType.Vector2    => prop.vector2Value.ToString(),
                SerializedPropertyType.Vector3    => prop.vector3Value.ToString(),
                SerializedPropertyType.Vector4    => prop.vector4Value.ToString(),
                SerializedPropertyType.Rect       => prop.rectValue.ToString(),
                SerializedPropertyType.Quaternion => prop.quaternionValue.eulerAngles.ToString(),
                _                                 => $"({prop.propertyType})"
            };
        }
    }
}
