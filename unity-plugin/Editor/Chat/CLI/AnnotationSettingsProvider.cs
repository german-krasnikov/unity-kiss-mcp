// Contributes "Annotation Tools" foldout to Chat Settings.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class AnnotationSettingsProvider : ISettingsProvider
    {
        static AnnotationSettingsProvider() => SettingsProviderRegistry.Register(new AnnotationSettingsProvider());

        public string Key         => "annotation_tools";
        public string DisplayName => "Annotation Tools";
        public int    Order       => 900;

        public void BuildUI(VisualElement parent)
        {
            var autoAdd = new Toggle("Auto-add to chat")
                { value = EditorPrefs.GetBool(PrefKeys.RegionAutoAdd, true) };
            autoAdd.RegisterValueChangedCallback(e =>
                EditorPrefs.SetBool(PrefKeys.RegionAutoAdd, e.newValue));
            parent.Add(autoAdd);

            var toolOptions = new List<string> { "Point", "Path", "Ruler" };
            var toolDropdown = new DropdownField("Default tool", toolOptions,
                EditorPrefs.GetInt(PrefKeys.DefaultAnnotationMode, 0));
            toolDropdown.RegisterValueChangedCallback(e =>
                EditorPrefs.SetInt(PrefKeys.DefaultAnnotationMode, toolOptions.IndexOf(e.newValue)));
            parent.Add(toolDropdown);

            var maxObj = new IntegerField("Max objects shown")
                { value = EditorPrefs.GetInt(PrefKeys.RegionMaxObjects, 10) };
            maxObj.tooltip = "Max contained object paths in region chip payload (1-100)";
            maxObj.RegisterValueChangedCallback(e =>
                EditorPrefs.SetInt(PrefKeys.RegionMaxObjects, Mathf.Clamp(e.newValue, 1, 100)));
            parent.Add(maxObj);
        }
    }
}
