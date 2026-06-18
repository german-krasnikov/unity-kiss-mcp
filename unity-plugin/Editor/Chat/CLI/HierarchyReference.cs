// Value object for hierarchy references: path + instanceID + GlobalObjectId.
// Supports both legacy "path #id" and new "path#id@goid" formats.
using System;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public readonly struct HierarchyReference
    {
        public string Path { get; }
        public int InstanceId { get; }
        public GlobalObjectId GlobalObjectId { get; }

        public HierarchyReference(string path, int instanceId, GlobalObjectId globalObjectId)
        {
            Path = path;
            InstanceId = instanceId;
            GlobalObjectId = globalObjectId;
        }

        public static HierarchyReference Parse(string rawRef)
        {
            if (string.IsNullOrEmpty(rawRef))
                return new HierarchyReference("", 0, default);

            var working = rawRef;
            GlobalObjectId globalObjectId = default;

            int atIndex = working.IndexOf('@');
            if (atIndex >= 0)
            {
                var goidString = working.Substring(atIndex + 1);
                working = working.Substring(0, atIndex);
                GlobalObjectId.TryParse(goidString, out globalObjectId);
            }

            int instanceId = 0;
            int hashIndex = working.LastIndexOf(" #");
            if (hashIndex >= 0)
            {
                if (int.TryParse(working.Substring(hashIndex + 2), out var id))
                {
                    instanceId = id;
                    working = working.Substring(0, hashIndex).TrimEnd();
                }
            }
            else
            {
                hashIndex = working.LastIndexOf('#');
                if (hashIndex >= 0 && int.TryParse(working.Substring(hashIndex + 1), out var id2))
                {
                    instanceId = id2;
                    working = working.Substring(0, hashIndex).TrimEnd();
                }
            }

            return new HierarchyReference(working, instanceId, globalObjectId);
        }
    }

    public interface IHierarchyResolver
    {
        GameObject Resolve(HierarchyReference reference);
    }

    internal sealed class HierarchyResolver : IHierarchyResolver
    {
        public GameObject Resolve(HierarchyReference reference)
        {
            // 1. GlobalObjectId (survives reparent/rename).
            if (reference.GlobalObjectId.targetObjectId != 0)
            {
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(reference.GlobalObjectId);
                if (obj is GameObject go) return go;
            }

            // 2. InstanceID.
            if (reference.InstanceId != 0)
            {
                var obj = EditorUtility.InstanceIDToObject(reference.InstanceId);
                if (obj is GameObject go) return go;
            }

            // 3. Exact path.
            if (!string.IsNullOrEmpty(reference.Path))
            {
                var go = SceneObjectFinder.FindGameObject(reference.Path);
                if (go != null) return go;

                // 4. Fuzzy name match on the leaf.
                var leaf = reference.Path;
                int slash = leaf.LastIndexOf('/');
                if (slash >= 0 && slash < leaf.Length - 1)
                    leaf = leaf.Substring(slash + 1);

                if (!string.IsNullOrEmpty(leaf))
                {
                    var fuzzy = GameObject.Find(leaf);
                    if (fuzzy != null) return fuzzy;
                }
            }

            return null;
        }
    }
}
