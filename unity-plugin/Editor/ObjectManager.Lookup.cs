using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static partial class ObjectManager
    {
        public static string FindObjects(string name, string tag, string layer, string component)
        {
            var results = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                TraverseAndFilter(root.transform, name, tag, layer, component, results);
            return results.ToString().TrimEnd('\n');
        }

        private static void TraverseAndFilter(Transform t, string name, string tag, string layer, string component, StringBuilder results)
        {
            var go = t.gameObject;
            bool match = true;

            if (!string.IsNullOrEmpty(name) && !go.name.Contains(name)) match = false;
            if (!string.IsNullOrEmpty(tag) && go.tag != tag) match = false;
            if (!string.IsNullOrEmpty(layer) && LayerMask.LayerToName(go.layer) != layer) match = false;
            if (!string.IsNullOrEmpty(component) && go.GetComponent(ComponentSerializer.StripNamespace(component)) == null) match = false;

            if (match) results.AppendLine(ComponentSerializer.GetPath(go));

            foreach (Transform child in t)
                TraverseAndFilter(child, name, tag, layer, component, results);
        }

        internal static Type FindType(string typeName)
        {
            // Fast path: common Unity types
            var quick = Type.GetType("UnityEngine." + typeName + ", UnityEngine")
                     ?? Type.GetType(typeName + ", Assembly-CSharp");
            if (quick != null) return quick;

            // Full scan by full name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName) ?? assembly.GetType("UnityEngine." + typeName);
                    if (type != null) return type;
                }
                catch (ReflectionTypeLoadException) { }
            }

            // Short-name scan (no dot in typeName = unqualified)
            if (!typeName.Contains('.'))
            {
                Type found = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                            {
                                if (found != null)
                                    throw new ArgumentException(
                                        $"Ambiguous: '{typeName}' = {found.FullName} and {t.FullName}. Use full namespace.");
                                found = t;
                            }
                        }
                    }
                    catch (ReflectionTypeLoadException) { }
                }
                if (found != null) return found;
            }

            return null;
        }
    }
}
