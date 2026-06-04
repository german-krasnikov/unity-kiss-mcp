using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static class ComponentSerializer
    {
        private static readonly HashSet<string> SkipProperties = new HashSet<string>
        {
            "m_Script", "m_ObjectHideFlags", "m_GameObject",
            "m_Enabled", "m_EditorHideFlags", "m_EditorClassIdentifier"
        };

        private static readonly HashSet<string> SkipTransformProperties = new HashSet<string>
        {
            "m_Father", "m_Children", "m_RootOrder", "m_ConstrainProportionsScale"
        };

        public static string Serialize(string path, string typeName)
        {
            var go = FindObject(path);
            if (go == null) return null;

            var component = FindComponent(go, typeName);
            if (component == null) return null;

            var sb = new StringBuilder();
            SerializeComponent(sb, component);
            return sb.ToString().TrimEnd();
        }

        public static string SerializeAll(int instanceId)
        {
            var go = FindObjectById(instanceId);
            if (go == null) return null;

            var sb = new StringBuilder();
            sb.Append("name: ").AppendLine(go.name);
            sb.Append("active: ").AppendLine(go.activeSelf ? "true" : "false");
            sb.Append("tag: ").AppendLine(go.tag);
            var layerName = LayerMask.LayerToName(go.layer);
            sb.Append("layer: ").AppendLine(string.IsNullOrEmpty(layerName) ? go.layer.ToString() : layerName);
            if (go.isStatic) sb.AppendLine("static: true");

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                sb.AppendLine("---");
                sb.Append('[').Append(comp.GetType().Name).AppendLine("]");
                SerializeComponent(sb, comp);
            }

            return sb.ToString().TrimEnd();
        }

        private static void SerializeComponent(StringBuilder sb, Component component)
        {
            var isTransform = component is Transform;
            var so = new SerializedObject(component);
            var prop = so.GetIterator();
            int written = 0;
            if (prop.NextVisible(true))
            {
                do
                {
                    if (SkipProperties.Contains(prop.name)) continue;
                    if (isTransform && SkipTransformProperties.Contains(prop.name)) continue;
                    sb.Append(prop.name).Append(": ");
                    AppendPropertyValue(sb, prop);
                    sb.AppendLine();
                    written++;
                } while (prop.NextVisible(false));
            }
            if (written == 0) sb.AppendLine("(no serialized fields)");
        }

        public static string ListComponents(string path)
        {
            var go = FindObject(path);
            if (go == null) return null;
            return ListComponentsInternal(go);
        }

        public static string ListComponents(int instanceId)
        {
            var go = FindObjectById(instanceId);
            if (go == null) return null;
            return ListComponentsInternal(go);
        }

        private static string ListComponentsInternal(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var sb = new StringBuilder();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var name = comp.GetType().Name;
                if (name == "Transform") continue;
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(name);
            }
            return sb.ToString();
        }

        internal static GameObject FindObjectById(int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            return obj as GameObject;
        }

        private static void AppendPropertyValue(StringBuilder sb, SerializedProperty prop)
            => sb.Append(GetPropertyValueString(prop));

        internal static string GetPropertyValueString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:   return prop.intValue.ToString();
                case SerializedPropertyType.Float:     return prop.floatValue.ToString("G4", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean:   return prop.boolValue ? "true" : "false";
                case SerializedPropertyType.String:    return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return (prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length)
                        ? prop.enumDisplayNames[prop.enumValueIndex]
                        : prop.intValue.ToString();
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return $"({v2.x.ToString("G4", CultureInfo.InvariantCulture)}, {v2.y.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return $"({v3.x.ToString("G4", CultureInfo.InvariantCulture)}, {v3.y.ToString("G4", CultureInfo.InvariantCulture)}, {v3.z.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return $"({v4.x.ToString("G4", CultureInfo.InvariantCulture)}, {v4.y.ToString("G4", CultureInfo.InvariantCulture)}, {v4.z.ToString("G4", CultureInfo.InvariantCulture)}, {v4.w.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.Vector2Int:
                    var v2i = prop.vector2IntValue;
                    return $"({v2i.x}, {v2i.y})";
                case SerializedPropertyType.Vector3Int:
                    var v3i = prop.vector3IntValue;
                    return $"({v3i.x}, {v3i.y}, {v3i.z})";
                case SerializedPropertyType.Color:
                    return $"#{ColorUtility.ToHtmlStringRGBA(prop.colorValue)}";
                case SerializedPropertyType.Quaternion:
                    var euler = prop.quaternionValue.eulerAngles;
                    return $"({euler.x.ToString("G4", CultureInfo.InvariantCulture)}, {euler.y.ToString("G4", CultureInfo.InvariantCulture)}, {euler.z.ToString("G4", CultureInfo.InvariantCulture)})";
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null) return "null";
                    if (prop.objectReferenceValue is GameObject refGo)
                        return $"{GetPath(refGo)} #{refGo.GetInstanceID()}";
                    if (prop.objectReferenceValue is Component refComp)
                        return $"{GetPath(refComp.gameObject)} #{refComp.gameObject.GetInstanceID()} ({refComp.GetType().Name})";
                    return $"{prop.objectReferenceValue.name} #{prop.objectReferenceValue.GetInstanceID()}";
                case SerializedPropertyType.LayerMask:
                    var lsb = new StringBuilder();
                    AppendLayerMask(lsb, prop.intValue);
                    return lsb.ToString();
                case SerializedPropertyType.ArraySize:
                    return prop.intValue.ToString();
                case SerializedPropertyType.Generic:
                    if (prop.isArray)
                    {
                        int count = System.Math.Min(prop.arraySize, 10);
                        if (count == 0) return "[]";
                        var asb = new StringBuilder("[");
                        for (int j = 0; j < count; j++)
                        {
                            if (j > 0) asb.Append(", ");
                            var elem = prop.GetArrayElementAtIndex(j);
                            if (elem.propertyType == SerializedPropertyType.Generic)
                            {
                                var inlined = TryInlineStruct(elem, 1);
                                asb.Append(inlined ?? "{...}");
                            }
                            else
                                asb.Append(GetPropertyValueString(elem));
                        }
                        if (prop.arraySize > 10) asb.Append($", ...+{prop.arraySize - 10}");
                        return asb.Append("]").ToString();
                    }
                    else
                    {
                        var calls = prop.FindPropertyRelative("m_PersistentCalls.m_Calls");
                        if (calls != null && calls.isArray)
                        {
                            var esb = new StringBuilder();
                            AppendUnityEvent(esb, calls);
                            return esb.ToString();
                        }
                        return TryInlineStruct(prop, 0) ?? $"<{prop.type}>";
                    }
                default:
                    return $"<{prop.propertyType}>";
            }
        }

        private static void AppendUnityEvent(StringBuilder sb, SerializedProperty calls)
        {
            sb.Append($"UnityEvent[{calls.arraySize}]");
            if (calls.arraySize == 0) return;
            sb.Append(" -> ");
            for (int i = 0; i < calls.arraySize; i++)
            {
                if (i > 0) sb.Append(", ");
                var call = calls.GetArrayElementAtIndex(i);
                var target = call.FindPropertyRelative("m_Target")?.objectReferenceValue;
                var method = call.FindPropertyRelative("m_MethodName")?.stringValue ?? "?";
                var mode = call.FindPropertyRelative("m_Mode")?.enumValueIndex ?? 0;
                sb.Append(target != null ? target.name : "null");
                sb.Append('.').Append(method).Append('(');
                var args = call.FindPropertyRelative("m_Arguments");
                switch (mode)
                {
                    case 1: sb.Append("void"); break;
                    case 3: sb.Append("int=").Append(args?.FindPropertyRelative("m_IntArgument")?.intValue ?? 0); break;
                    case 4: sb.Append("float=").Append((args?.FindPropertyRelative("m_FloatArgument")?.floatValue ?? 0f).ToString("G4", CultureInfo.InvariantCulture)); break;
                    case 5: sb.Append("string=").Append(args?.FindPropertyRelative("m_StringArgument")?.stringValue ?? ""); break;
                    case 6: sb.Append("bool=").Append(args?.FindPropertyRelative("m_BoolArgument")?.boolValue == true ? "True" : "False"); break;
                    case 2:
                    {
                        var objArg = args?.FindPropertyRelative("m_ObjectArgument")?.objectReferenceValue;
                        sb.Append("obj=").Append(objArg != null ? objArg.name : "null");
                        break;
                    }
                    default: sb.Append("void"); break;
                }
                sb.Append(')');
            }
        }

        private static void AppendLayerMask(StringBuilder sb, int mask)
        {
            bool first = true;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    var name = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!first) sb.Append(", ");
                    sb.Append(name);
                    first = false;
                }
            }
            if (first) sb.Append("None");
        }

        public static GameObject FindObjectOrThrow(string path)
        {
            var go = FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            return go;
        }

        public static GameObject FindObject(string path, bool strict = false)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (RefManager.IsRef(path))
            {
                var resolved = RefManager.Resolve(path);
                if (resolved != null) return resolved;
                throw new System.ArgumentException($"Stale ref: {path}. Call get_hierarchy to refresh.");
            }

            if (path.StartsWith("/")) path = path.Substring(1);
            if (string.IsNullOrEmpty(path)) return null;

            var parts = path.Split('/');
            var root = FindRoot(parts[0]);
            if (root == null) return strict ? null : TryFuzzyFind(path, parts);

            GameObject current = root;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                if (child == null) return strict ? null : TryFuzzyFind(path, parts);
                current = child.gameObject;
            }
            return current;
        }

        private static GameObject TryFuzzyFind(string path, string[] parts)
        {
            var lastName = parts[parts.Length - 1];
            var candidates = new List<GameObject>();

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    FindByName(root, lastName, candidates, 5);
            }

            if (parts.Length > 1)
            {
                var suffix = "/" + string.Join("/", parts);
                candidates.RemoveAll(c => !GetPath(c).EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase));
            }

            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count > 1)
                throw new System.ArgumentException(
                    $"Ambiguous: '{path}'. Did you mean: " +
                    string.Join(", ", candidates.ConvertAll(c => GetPath(c))));
            return null;
        }

        private static void FindByName(GameObject root, string name, List<GameObject> results, int max)
        {
            var queue = new Queue<GameObject>();
            queue.Enqueue(root);
            while (queue.Count > 0 && results.Count < max)
            {
                var go = queue.Dequeue();
                if (go.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    results.Add(go);
                for (int i = 0; i < go.transform.childCount; i++)
                    queue.Enqueue(go.transform.GetChild(i).gameObject);
            }
        }

        private static GameObject FindRoot(string name)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot.name == name)
                return stage.prefabContentsRoot;

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == name) return root;
            }
            return null;
        }

        /// <summary>Strip namespace prefix: "UnityEngine.UI.Button" → "Button"</summary>
        internal static string StripNamespace(string typeName)
        {
            if (typeName == null) return null;
            var dot = typeName.LastIndexOf('.');
            return dot >= 0 ? typeName.Substring(dot + 1) : typeName;
        }

        internal static Component FindComponent(GameObject go, string typeName)
        {
            var shortName = InputNormalizer.NormalizeComponent(StripNamespace(typeName), go);
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var t = comp.GetType();
                if (t.Name == shortName || t.FullName == typeName)
                    return comp;
                var bt = t.Name.IndexOf('`');
                if (bt > 0 && t.Name.Substring(0, bt).Equals(shortName, System.StringComparison.OrdinalIgnoreCase))
                    return comp;
            }
            return null;
        }

        internal static string GetPath(GameObject go)
        {
            var path = go.name;
            var t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return "/" + path;
        }

        private const int MaxInlineFields = 8;

        private static string TryInlineStruct(SerializedProperty prop, int depth)
        {
            if (depth > 2) return null;

            var copy = prop.Copy();
            var end = prop.GetEndProperty();
            if (!copy.NextVisible(true) || SerializedProperty.EqualContents(copy, end))
                return null;

            var fields = new System.Collections.Generic.List<(string name, string val)>();
            do
            {
                if (SerializedProperty.EqualContents(copy, end)) break;
                fields.Add((copy.name, GetPropertyValueString(copy)));
                if (fields.Count >= MaxInlineFields) break;
            } while (copy.NextVisible(false));

            if (fields.Count == 0) return null;

            // Pretty HashId: exactly 2 fields, one string + one int
            if (fields.Count == 2)
            {
                var f0 = prop.FindPropertyRelative(fields[0].name);
                var f1 = prop.FindPropertyRelative(fields[1].name);
                if (f0 != null && f1 != null)
                {
                    SerializedProperty strProp = null, intProp = null;
                    if (f0.propertyType == SerializedPropertyType.String && f1.propertyType == SerializedPropertyType.Integer)
                    { strProp = f0; intProp = f1; }
                    else if (f1.propertyType == SerializedPropertyType.String && f0.propertyType == SerializedPropertyType.Integer)
                    { strProp = f1; intProp = f0; }
                    if (strProp != null)
                        return $"{strProp.stringValue} ({intProp.intValue})";
                }
            }

            var sb = new StringBuilder("{");
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(fields[i].name).Append('=').Append(fields[i].val);
            }
            return sb.Append("}").ToString();
        }
    }
}
