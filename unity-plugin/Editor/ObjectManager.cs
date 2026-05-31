using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEditor.Events;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class ObjectManager
    {
        public static string FindObjects(string name, string tag, string layer, string component)
        {
            var results = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                TraverseAndFilter(root.transform, name, tag, layer, component, results);
            }

            return results.ToString().TrimEnd('\n');
        }

        // Resolves path → go and component name → Component in one call.
        // Throws ArgumentException if either is missing.
        internal static (GameObject go, Component comp) ResolveComponent(string path, string component)
        {
            component = ComponentSerializer.StripNamespace(component);
            var go = ComponentSerializer.FindObjectOrThrow(path);
            component = InputNormalizer.NormalizeComponent(component, go);
            var comp = go.GetComponent(component);
            if (comp == null) throw new ArgumentException(ErrorHelper.ComponentNotFound(component, go));
            return (go, comp);
        }

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

        public static string CreateObject(string name, string parent, string components, string primitive = null, string prefabPath = null)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                    throw new ArgumentException($"Prefab not found: {prefabPath} (exists={System.IO.File.Exists(prefabPath)})");
                var prefabType = PrefabUtility.GetPrefabAssetType(prefabAsset);
                if (prefabType == PrefabAssetType.NotAPrefab)
                    throw new ArgumentException($"Not a prefab: {prefabPath} (type={prefabAsset.GetType().Name})");
                go = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                if (go == null)
                    throw new InvalidOperationException($"Failed to instantiate: {prefabPath} (prefabType={prefabType})");
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }
            else if (!string.IsNullOrEmpty(primitive) && Enum.TryParse<PrimitiveType>(primitive, true, out var pt))
            {
                go = GameObject.CreatePrimitive(pt);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }
            else
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = ComponentSerializer.FindObjectOrThrow(parent);
                Undo.SetTransformParent(go.transform, parentGo.transform, false, $"Reparent {name}");
                EditorUtility.SetDirty(go);
                if (!EditorApplication.isPlaying)
                    EditorSceneManager.MarkSceneDirty(go.scene);
            }

            if (!string.IsNullOrEmpty(components))
            {
                var types = components.Split(',');
                foreach (var typeName in types)
                {
                    var type = FindType(typeName.Trim());
                    if (type == null)
                        throw new ArgumentException($"Component type not found: {typeName}");
                    Undo.AddComponent(go, type);
                }
            }

            return ComponentSerializer.GetPath(go);
        }

        public static string SetActive(string path, bool active)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);
            Undo.RecordObject(go, $"SetActive {path}");
            go.SetActive(active);
            EditorUtility.SetDirty(go);
            return $"{ComponentSerializer.GetPath(go)} active={active}";
        }

        public static string SetMaterial(string path, string color, string shader)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                throw new ArgumentException(ErrorHelper.ComponentNotFound("Renderer", go));

            var shaderObj = Shader.Find(shader ?? "Universal Render Pipeline/Lit");
            if (shaderObj == null)
                shaderObj = Shader.Find("Standard");
            if (shaderObj == null)
                throw new ArgumentException($"Shader not found: {shader}");

            var mat = new Material(shaderObj);
            Undo.RegisterCreatedObjectUndo(mat, "Create Material");
            if (!string.IsNullOrEmpty(color))
            {
                if (!ColorUtility.TryParseHtmlString(color, out var c))
                    throw new ArgumentException($"Invalid color: {color}");
                mat.color = c;
            }
            Undo.RecordObject(renderer, "Set Material");
            renderer.sharedMaterial = mat;
            return $"shader={renderer.sharedMaterial.shader.name} color=#{ColorUtility.ToHtmlStringRGBA(renderer.sharedMaterial.color)}";
        }

        public static string WireEvent(string path, string component, string eventField,
            string targetPath, string methodName, string argType, string argValue)
        {
            var (_, comp) = ResolveComponent(path, component);

            // Validate event field via SerializedProperty (more reliable than reflection)
            var soCheck = new SerializedObject(comp);
            var evtCheck = soCheck.FindProperty(eventField);
            if (evtCheck == null)
                throw new ArgumentException($"Field '{eventField}' not found on {component}");
            if (evtCheck.FindPropertyRelative("m_PersistentCalls.m_Calls") == null)
                throw new ArgumentException($"Field '{eventField}' is not a UnityEvent");

            // Find target — scene object or asset
            UnityEngine.Object target;
            var targetGo = ComponentSerializer.FindObject(targetPath);
            if (targetGo != null)
            {
                // Resolve component that has the target method (fixes m_TargetAssemblyTypeName)
                UnityEngine.Object resolved = targetGo; // fallback: GO itself (SetActive etc.)
                foreach (var comp2 in targetGo.GetComponents<Component>())
                {
                    if (comp2 == null) continue;
                    try
                    {
                        if (comp2.GetType().GetMethod(methodName,
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) != null)
                        {
                            resolved = comp2;
                            break;
                        }
                    }
                    catch { }
                }
                target = resolved;
            }
            else
            {
                target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(targetPath);
                if (target == null)
                    throw new ArgumentException($"Target not found: {targetPath}");
            }

            Undo.RecordObject(comp, $"Wire {eventField}");

            if (!string.IsNullOrEmpty(argType) && argType != "void" && string.IsNullOrEmpty(argValue))
                throw new ArgumentException($"arg_value required when arg_type is '{argType}'");

            // Add persistent listener via SerializedObject for reliability
            var so = new SerializedObject(comp);
            var evtProp = so.FindProperty(eventField);
            var calls = evtProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            int idx = calls.arraySize;
            calls.InsertArrayElementAtIndex(idx);
            var call = calls.GetArrayElementAtIndex(idx);

            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                $"{target.GetType().AssemblyQualifiedName}";
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly

            // Determine mode from argType
            if (string.IsNullOrEmpty(argType) || argType == "void")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
            }
            else if (argType == "bool")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 6; // Bool
                call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue =
                    argValue == "true";
            }
            else if (argType == "int")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 3; // Int
                call.FindPropertyRelative("m_Arguments.m_IntArgument").intValue =
                    int.Parse(argValue);
            }
            else if (argType == "float")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 4; // Float
                call.FindPropertyRelative("m_Arguments.m_FloatArgument").floatValue =
                    float.Parse(argValue, CultureInfo.InvariantCulture);
            }
            else if (argType == "string")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 5; // String
                call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = argValue;
            }
            else if (argType == "object")
            {
                call.FindPropertyRelative("m_Mode").enumValueIndex = 2; // Object
                var argObj = ComponentSerializer.FindObject(argValue);
                UnityEngine.Object resolved = argObj;
                if (resolved == null)
                    resolved = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(argValue);
                if (resolved == null)
                    throw new ArgumentException($"Object arg not found: {argValue}");
                call.FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue = resolved;
                call.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue =
                    "UnityEngine.Object, UnityEngine";
            }
            else
            {
                throw new ArgumentException($"Unsupported arg_type: {argType}. Use: void, bool, int, float, string, object");
            }

            so.ApplyModifiedProperties();
            return argType == "void" || string.IsNullOrEmpty(argType)
                ? $"Wired {eventField}[{idx}]: {targetPath}.{methodName}()"
                : $"Wired {eventField}[{idx}]: {targetPath}.{methodName}({argType}={argValue})";
        }

        public static string UnwireEvent(string path, string component, string eventField, string index)
        {
            var (_, comp) = ResolveComponent(path, component);

            var so = new SerializedObject(comp);
            var evtProp = so.FindProperty(eventField);
            if (evtProp == null)
                throw new ArgumentException($"Field '{eventField}' not found on {component}");
            var calls = evtProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null)
                throw new ArgumentException($"Field '{eventField}' is not a UnityEvent");

            Undo.RecordObject(comp, $"Unwire {eventField}");

            if (string.IsNullOrEmpty(index))
            {
                int count = calls.arraySize;
                calls.arraySize = 0;
                so.ApplyModifiedProperties();
                return $"Cleared {eventField} ({count} removed)";
            }

            if (!int.TryParse(index, out int idx))
                throw new ArgumentException($"Index must be an integer, got: '{index}'");
            if (idx < 0 || idx >= calls.arraySize)
                throw new ArgumentException($"Index {idx} out of range (0..{calls.arraySize - 1})");
            calls.DeleteArrayElementAtIndex(idx);
            so.ApplyModifiedProperties();
            return $"Removed {eventField}[{idx}], {calls.arraySize} remaining";
        }

        public static string SetParent(string path, string newParent, bool worldPositionStays = true)
        {
            var go = ComponentSerializer.FindObject(path, strict: true);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(newParent))
            {
                var parentGo = ComponentSerializer.FindObject(newParent, strict: true);
                if (parentGo == null)
                    throw new ArgumentException(ErrorHelper.ObjectNotFound(newParent));
                parentTransform = parentGo.transform;
            }

            Undo.SetTransformParent(go.transform, parentTransform, worldPositionStays, $"Set parent {path}");
            EditorUtility.SetDirty(go);
            if (!EditorApplication.isPlaying)
                EditorSceneManager.MarkSceneDirty(go.scene);
            return ComponentSerializer.GetPath(go);
        }

        public static void DeleteObject(string path, bool force = false)
        {
            var go = ComponentSerializer.FindObject(path, strict: true);
            if (go == null)
                throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
            if (!force && go.transform.childCount > 0)
                throw new ArgumentException(
                    $"'{path}' has {go.transform.childCount} children. Pass force=true to delete with all descendants.");
            Undo.DestroyObjectImmediate(go);
        }

        public static void DeleteObject(int instanceId, bool force = false)
        {
            var go = ComponentSerializer.FindObjectById(instanceId);
            if (go == null)
                throw new ArgumentException($"Object not found: #{instanceId}");
            if (!force && go.transform.childCount > 0)
                throw new ArgumentException(
                    $"'#{instanceId}' has {go.transform.childCount} children. Pass force=true to delete with all descendants.");
            Undo.DestroyObjectImmediate(go);
        }

        public static void ManageComponent(string path, string type, string action)
        {
            var go = ComponentSerializer.FindObjectOrThrow(path);

            if (action == "add")
            {
                var componentType = FindType(type);
                if (componentType == null)
                    throw new ArgumentException($"Component type not found: {type}");
                if (go.GetComponent(componentType) != null)
                    throw new ArgumentException(
                        $"'{type}' already exists on '{go.name}'. " +
                        "Use action=remove first, or set_property to modify.");
                Undo.AddComponent(go, componentType);
            }
            else if (action == "remove")
            {
                var shortType = ComponentSerializer.StripNamespace(type);
                var component = go.GetComponent(shortType);
                if (component == null)
                    throw new ArgumentException(ErrorHelper.ComponentNotFound(type, go));
                Undo.DestroyObjectImmediate(component);
            }
            else
            {
                throw new ArgumentException($"Invalid action: {action}");
            }
        }

        private static void TraverseAndFilter(Transform t, string name, string tag, string layer, string component, StringBuilder results)
        {
            var go = t.gameObject;
            bool match = true;

            if (!string.IsNullOrEmpty(name) && !go.name.Contains(name))
                match = false;
            if (!string.IsNullOrEmpty(tag) && go.tag != tag)
                match = false;
            if (!string.IsNullOrEmpty(layer) && LayerMask.LayerToName(go.layer) != layer)
                match = false;
            if (!string.IsNullOrEmpty(component) && go.GetComponent(ComponentSerializer.StripNamespace(component)) == null)
                match = false;

            if (match)
            {
                results.AppendLine(ComponentSerializer.GetPath(go));
            }

            foreach (Transform child in t)
            {
                TraverseAndFilter(child, name, tag, layer, component, results);
            }
        }

        internal static Type FindType(string typeName)
        {
            // Fast path: common Unity types
            var quick = Type.GetType("UnityEngine." + typeName + ", UnityEngine")
                     ?? Type.GetType(typeName + ", Assembly-CSharp");
            if (quick != null) return quick;

            // Full scan
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName) ?? assembly.GetType("UnityEngine." + typeName);
                    if (type != null) return type;
                }
                catch (ReflectionTypeLoadException) { }
            }
            return null;
        }
    }
}
