using System;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class SchemaHelper
    {
        public static string GetSchema(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "Type not found: (empty)";

            var type = FindType(typeName);
            if (type == null) return $"Type not found: {typeName}";
            if (!typeof(Component).IsAssignableFrom(type)) return $"Cannot instantiate: {typeName} (not a Component)";

            var go = new GameObject("_schema_probe");
            go.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var comp = go.AddComponent(type);
                if (comp == null) return $"Cannot instantiate: {typeName}";

                var sb = new StringBuilder();
                sb.AppendLine($"Schema: {type.Name}");
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        if (prop.name == "m_Script") continue;
                        sb.AppendLine($"  {prop.name}: {prop.propertyType}");
                    } while (prop.NextVisible(false));
                }
                return sb.ToString().TrimEnd();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName)
                    ?? asm.GetTypes().FirstOrDefault(x => x.Name == typeName);
                if (t != null) return t;
            }
            return null;
        }
    }
}
