using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class RemapReferencesHelper
    {
        private const int MAX_ARRAY = 100;

        public static string RemapReferences(string sourcePath, string targetPath, string mappingsText)
        {
            var sourceGo = ComponentSerializer.FindObject(sourcePath);
            if (sourceGo == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(sourcePath));
            var targetGo = ComponentSerializer.FindObject(targetPath);
            if (targetGo == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(targetPath));

            var mappings = ParseMappings(mappingsText);

            var sb = new StringBuilder();
            int remapped = 0, kept = 0, errors = 0;

            var targetComps = targetGo.GetComponents<Component>();
            foreach (var comp in targetComps)
            {
                if (comp == null) continue;

                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                if (!prop.NextVisible(true)) continue;

                Undo.RecordObject(comp, "Remap References");
                bool modified = false;

                do
                {
                    if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic)
                    {
                        int cap = Math.Min(prop.arraySize, MAX_ARRAY);
                        for (int i = 0; i < cap; i++)
                        {
                            var elem = prop.GetArrayElementAtIndex(i);
                            if (elem.propertyType == SerializedPropertyType.ObjectReference)
                                RemapProperty(elem, $"{prop.name}[{i}]", sourcePath, targetPath, mappings, sb, ref remapped, ref kept, ref errors, ref modified);
                        }
                    }
                    else if (prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        RemapProperty(prop, prop.name, sourcePath, targetPath, mappings, sb, ref remapped, ref kept, ref errors, ref modified);
                    }
                } while (prop.NextVisible(false));

                if (modified) so.ApplyModifiedProperties();
            }

            sb.Append($"remapped: {remapped}, kept: {kept}, errors: {errors}");
            return sb.ToString();
        }

        private static void RemapProperty(SerializedProperty prop, string label, string sourcePath, string targetPath,
            Dictionary<string, string> mappings, StringBuilder sb, ref int remapped, ref int kept, ref int errors, ref bool modified)
        {
            if (prop.objectReferenceValue == null) return;

            string currentRefPath = null;
            if (prop.objectReferenceValue is GameObject refGo)
                currentRefPath = ComponentSerializer.GetPath(refGo);
            else if (prop.objectReferenceValue is Component refComp)
                currentRefPath = ComponentSerializer.GetPath(refComp.gameObject);
            if (currentRefPath == null) return;

            string newPath = null;
            if (mappings.TryGetValue(currentRefPath, out var mapped))
                newPath = mapped;
            else if (currentRefPath.StartsWith(sourcePath + "/") || currentRefPath == sourcePath)
                newPath = AutoRemapPath(currentRefPath, sourcePath, targetPath);

            if (newPath != null)
            {
                var newGo = ComponentSerializer.FindObject(newPath);
                if (newGo != null)
                {
                    var fieldType = ValueParser.GetSerializedFieldType(prop);
                    if (fieldType != null && typeof(Component).IsAssignableFrom(fieldType) && fieldType != typeof(GameObject))
                        prop.objectReferenceValue = newGo.GetComponent(fieldType);
                    else
                        prop.objectReferenceValue = newGo;
                    modified = true;
                    sb.Append(label).Append(": ").Append(currentRefPath).Append(" -> ").AppendLine(newPath);
                    remapped++;
                }
                else
                {
                    sb.Append(label).Append(": ").Append(currentRefPath).Append(" -> ").Append(newPath).AppendLine(" miss");
                    errors++;
                }
            }
            else
            {
                kept++;
            }
        }

        private static string AutoRemapPath(string refPath, string sourcePath, string targetPath)
        {
            if (refPath == sourcePath) return targetPath;
            return targetPath + refPath.Substring(sourcePath.Length);
        }

        private static Dictionary<string, string> ParseMappings(string text)
        {
            var dict = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(text)) return dict;

            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;
                dict[trimmed.Substring(0, eq).Trim()] = trimmed.Substring(eq + 1).Trim();
            }
            return dict;
        }
    }
}
