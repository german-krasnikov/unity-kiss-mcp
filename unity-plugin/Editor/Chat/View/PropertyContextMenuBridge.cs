using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Adds "Add to MCP Chat" to every serialized property's right-click menu in the Inspector.
    /// </summary>
    [InitializeOnLoad]
    internal static class PropertyContextMenuBridge
    {
        static PropertyContextMenuBridge()
            => EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;

        internal static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
        {
            if (ChipPillFactory.AddToContextAction == null) return;
            var chip = BuildChipForProperty(property);
            if (!chip.HasValue) return;
            var captured = chip.Value;
            menu.AddItem(new GUIContent("Add to MCP Chat"), false,
                () => ChipPillFactory.AddToContextAction?.Invoke(captured));
        }

        /// <summary>Test seam: builds ChipData for a property without touching GenericMenu.</summary>
        internal static ChipData? BuildChipForProperty(SerializedProperty property)
        {
            if (property.serializedObject.targetObject is not Component comp) return null;
            if (property.name == "m_Script") return null;
            return FieldContextMenu.BuildChipData(comp, property.propertyPath);
        }
    }
}
