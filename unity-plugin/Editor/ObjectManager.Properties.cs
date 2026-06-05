using System;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        public static string SetProperty(string path, string component, string prop, string value, bool dryRun = false)
        {
            var (_, comp) = ResolveComponent(path, component);

            var so = new SerializedObject(comp);
            prop = InputNormalizer.NormalizeProperty(prop, so);
            value = InputNormalizer.NormalizeValue(value);
            var property = so.FindProperty(prop);

            if (property == null)
                throw new ArgumentException(ErrorHelper.PropertyNotFound(prop, component, path));

            if (dryRun)
            {
                var current = ComponentSerializer.GetPropertyValueString(property);
                return $"DRY-RUN: {prop} would change {current} → {value}";
            }

            Undo.RecordObject(comp, $"Set {prop}");
            // Handle arrays: comma-separated paths/values
            if (property.isArray && property.propertyType == SerializedPropertyType.Generic)
            {
                var items = ValueParser.SplitArrayValues(value);
                if (items.Length == 0)
                {
                    property.arraySize = 0;
                }
                else
                {
                    property.arraySize = items.Length;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var elem = property.GetArrayElementAtIndex(i);
                        ValueParser.SetPropertyValue(elem, items[i]);
                    }
                }
            }
            else
            {
                ValueParser.SetPropertyValue(property, value);
            }
            so.ApplyModifiedProperties();
            if (comp is Transform && !EditorApplication.isPlaying && !BatchHelper.InBatch)
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }
            var readProp = so.FindProperty(prop);
            return readProp != null ? ComponentSerializer.GetPropertyValueString(readProp) : value;
        }

        public static string SetPropertyDelta(string path, string component, string prop, string delta)
        {
            var (_, comp) = ResolveComponent(path, component);
            Undo.RecordObject(comp, $"Delta {prop}");
            var so = new SerializedObject(comp);
            prop = InputNormalizer.NormalizeProperty(prop, so);
            var property = so.FindProperty(prop);
            if (property == null)
                throw new ArgumentException(ErrorHelper.PropertyNotFound(prop, component, path));

            var oldStr = ComponentSerializer.GetPropertyValueString(property);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    var d = float.Parse(delta.TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    property.floatValue += d;
                    break;
                }
                case SerializedPropertyType.Integer:
                {
                    var d = int.Parse(delta.TrimStart('+'));
                    property.intValue += d;
                    break;
                }
                case SerializedPropertyType.Vector3:
                {
                    var parts = delta.Trim('(', ')').Split(',');
                    var dx = float.Parse(parts[0].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    var dy = float.Parse(parts[1].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    var dz = float.Parse(parts[2].Trim().TrimStart('+'), System.Globalization.CultureInfo.InvariantCulture);
                    property.vector3Value += new UnityEngine.Vector3(dx, dy, dz);
                    break;
                }
                default:
                    throw new ArgumentException($"set_property_delta: unsupported type {property.propertyType}");
            }

            so.ApplyModifiedProperties();
            if (comp is Transform && !EditorApplication.isPlaying && !BatchHelper.InBatch)
            {
                Physics.SyncTransforms();
                Physics2D.SyncTransforms();
            }
            var newStr = ComponentSerializer.GetPropertyValueString(so.FindProperty(prop));
            return $"{oldStr} → {newStr}";
        }
    }
}
