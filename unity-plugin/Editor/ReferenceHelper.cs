using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class ReferenceHelper
    {
        private const int MAX_ARRAY = 100;
        private const int MAX_SCAN = 5000;

        private struct RefEntry
        {
            public string ComponentType;
            public string PropertyPath;
            public string ReferencedPath;
            public string Relation; // "child" | "external" | "asset" | "null"
            public int ReferencedId;
            public GameObject ReferencedObject;
        }

        // --- Public API ---

        public static string GetReferences(string path, bool includeChildren, int depth)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var sb = new StringBuilder();
            var visited = new HashSet<int>();
            AppendReferences(sb, go, path, depth, visited);

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                    AppendReferencesRecursive(sb, child.gameObject, path, depth, visited);
            }

            return sb.Length == 0 ? "no references" : sb.ToString().TrimEnd('\n');
        }

        public static string FindReferencesTo(string path)
        {
            var targetGo = ComponentSerializer.FindObject(path);
            if (targetGo == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var targetId = targetGo.GetInstanceID();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            int count = 0;
            int scanned = 0;

            foreach (var root in roots)
            {
                ScanForReferencesTo(root.transform, targetId, targetGo, sb, ref count, ref scanned);
                if (scanned >= MAX_SCAN)
                {
                    sb.AppendLine($"(scan limit {MAX_SCAN} reached)");
                    break;
                }
            }

            sb.Append("found: ").Append(count);
            return sb.ToString();
        }

        // --- Internal helpers ---

        private static void AppendReferences(StringBuilder sb, GameObject go, string rootPath, int depth, HashSet<int> visited)
        {
            if (!visited.Add(go.GetInstanceID())) return;

            var goPath = ComponentSerializer.GetPath(go);
            var refs = CollectRefs(go);

            if (refs.Count == 0) return;

            string lastComp = null;
            foreach (var r in refs)
            {
                if (r.ComponentType != lastComp)
                {
                    sb.Append(goPath).Append(" [").Append(r.ComponentType).AppendLine("]");
                    lastComp = r.ComponentType;
                }
                sb.Append("  ").Append(r.PropertyPath).Append(": ");
                if (r.Relation == "null")
                    sb.AppendLine("null");
                else
                    sb.Append(r.ReferencedPath).Append(" #").Append(r.ReferencedId).Append(' ').AppendLine(r.Relation);
            }

            if (depth > 1)
            {
                foreach (var r in refs)
                {
                    if (r.ReferencedObject != null && r.Relation != "asset" && r.Relation != "null")
                    {
                        sb.Append("-- ").Append(r.ReferencedPath).AppendLine(" --");
                        AppendReferences(sb, r.ReferencedObject, rootPath, depth - 1, visited);
                    }
                }
            }
        }

        private static void AppendReferencesRecursive(StringBuilder sb, GameObject go, string rootPath, int depth, HashSet<int> visited)
        {
            AppendReferences(sb, go, rootPath, depth, visited);
            foreach (Transform child in go.transform)
                AppendReferencesRecursive(sb, child.gameObject, rootPath, depth, visited);
        }

        private static List<RefEntry> CollectRefs(GameObject go)
        {
            var refs = new List<RefEntry>();
            var goPath = ComponentSerializer.GetPath(go);

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null || comp is Transform) continue;
                WalkProperties(new SerializedObject(comp), comp.GetType().Name, goPath, refs);
            }
            return refs;
        }

        private static void WalkProperties(SerializedObject so, string compType, string goPath, List<RefEntry> refs)
        {
            var prop = so.GetIterator();
            if (!prop.NextVisible(true)) return;

            do
            {
                if (prop.name == "m_Script" || prop.name == "m_GameObject") continue;

                if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic)
                {
                    int cap = Math.Min(prop.arraySize, MAX_ARRAY);
                    for (int i = 0; i < cap; i++)
                    {
                        var elem = prop.GetArrayElementAtIndex(i);
                        if (elem.propertyType == SerializedPropertyType.ObjectReference)
                            AddRefEntry(refs, compType, $"{prop.name}[{i}]", elem, goPath);
                    }
                }
                else if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    AddRefEntry(refs, compType, prop.name, prop, goPath);
                }
            } while (prop.NextVisible(false));
        }

        private static void AddRefEntry(List<RefEntry> refs, string compType, string propPath, SerializedProperty prop, string goPath)
        {
            var entry = new RefEntry { ComponentType = compType, PropertyPath = propPath };

            if (prop.objectReferenceValue == null)
            {
                entry.Relation = "null";
                refs.Add(entry);
                return;
            }

            GameObject refGo = null;
            if (prop.objectReferenceValue is GameObject g) refGo = g;
            else if (prop.objectReferenceValue is Component c) refGo = c.gameObject;

            if (refGo != null)
            {
                entry.ReferencedPath = ComponentSerializer.GetPath(refGo);
                entry.ReferencedId = refGo.GetInstanceID();
                entry.ReferencedObject = refGo;
                entry.Relation = ClassifyRef(goPath, refGo);
            }
            else
            {
                entry.ReferencedPath = prop.objectReferenceValue.name;
                entry.ReferencedId = prop.objectReferenceValue.GetInstanceID();
                entry.Relation = "asset";
            }

            refs.Add(entry);
        }

        private static string ClassifyRef(string ownerPath, GameObject referenced)
        {
            var refPath = ComponentSerializer.GetPath(referenced);
            if (refPath == ownerPath) return "self";
            if (refPath.StartsWith(ownerPath + "/")) return "child";
            if (ownerPath.StartsWith(refPath + "/")) return "parent";

            // Same root object = sibling
            var ownerRoot = ownerPath.IndexOf('/', 1);
            var refRoot = refPath.IndexOf('/', 1);
            var ownerRootPart = ownerRoot > 0 ? ownerPath.Substring(0, ownerRoot) : ownerPath;
            var refRootPart = refRoot > 0 ? refPath.Substring(0, refRoot) : refPath;
            if (ownerRootPart == refRootPart) return "sibling";

            return "external";
        }

        private static void ScanForReferencesTo(Transform t, int targetId, GameObject targetGo, StringBuilder sb, ref int count, ref int scanned)
        {
            if (scanned >= MAX_SCAN) return;
            scanned++;

            var go = t.gameObject;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                if (!prop.NextVisible(true)) continue;

                do
                {
                    if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic)
                    {
                        int cap = Math.Min(prop.arraySize, MAX_ARRAY);
                        for (int i = 0; i < cap; i++)
                        {
                            var elem = prop.GetArrayElementAtIndex(i);
                            if (MatchesTarget(elem, targetId))
                            {
                                sb.Append(ComponentSerializer.GetPath(go))
                                  .Append(" [").Append(comp.GetType().Name).Append("].").Append(prop.name).Append('[').Append(i).AppendLine("]");
                                count++;
                            }
                        }
                    }
                    else if (MatchesTarget(prop, targetId))
                    {
                        sb.Append(ComponentSerializer.GetPath(go))
                          .Append(" [").Append(comp.GetType().Name).Append("].").AppendLine(prop.name);
                        count++;
                    }
                } while (prop.NextVisible(false));
            }

            foreach (Transform child in t)
                ScanForReferencesTo(child, targetId, targetGo, sb, ref count, ref scanned);
        }

        private static bool MatchesTarget(SerializedProperty prop, int targetId)
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue == null)
                return false;
            if (prop.objectReferenceValue is GameObject refGo) return refGo.GetInstanceID() == targetId;
            if (prop.objectReferenceValue is Component refComp) return refComp.gameObject.GetInstanceID() == targetId;
            return false;
        }

    }
}
