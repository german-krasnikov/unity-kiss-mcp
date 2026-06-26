using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Reads component field values via reflection, with MemberInfo caching.
    [InitializeOnLoad]
    internal static class WatchEvaluator
    {
        private static readonly Dictionary<(Type, string), MemberInfo> _cache = new();
        private static readonly Dictionary<string, WeakReference<GameObject>> _goCache = new();
        private static readonly Dictionary<(string, string), WeakReference<Component>> _compCache = new();

        static WatchEvaluator()
        {
            EditorApplication.hierarchyChanged += () => { _goCache.Clear(); _compCache.Clear(); };
        }

        public static object ReadValue(string path, string componentType, string field)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var go = GetCachedGameObject(path);
            if (go == null) return null;
            var comp = GetCachedComponent(path, componentType, go);
            if (comp == null) return null;
            return ReadObjectField(comp, field);
        }

        private static GameObject GetCachedGameObject(string path)
        {
            if (_goCache.TryGetValue(path, out var goRef) && goRef.TryGetTarget(out var cached) && cached != null)
                return cached;
            var go = ComponentSerializer.FindObject(path);
            if (go != null) _goCache[path] = new WeakReference<GameObject>(go);
            return go;
        }

        private static Component GetCachedComponent(string path, string componentType, GameObject go)
        {
            var key = (path, componentType);
            if (_compCache.TryGetValue(key, out var compRef) && compRef.TryGetTarget(out var cached) && cached != null)
                return cached;
            var comp = RuntimeHelper.FindComponentInternal(go, componentType);
            if (comp != null) _compCache[key] = new WeakReference<Component>(comp);
            return comp;
        }

        // Internal for testing
        internal static object ReadObjectField(object obj, string fieldPath)
        {
            object current = obj;
            foreach (var part in fieldPath.Split('.'))
            {
                if (current == null) return null;
                var type = current.GetType();
                var key = (type, part);
                if (!_cache.TryGetValue(key, out var member))
                {
                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    member = (MemberInfo)type.GetField(part, flags)
                          ?? type.GetProperty(part, flags);
                    _cache[key] = member;
                    // Log on first miss only — cache hit with null skips this block
                    if (member == null)
                        WatchRegistry.AddLogEntry($"[ERR] member '{part}' not found on {type.Name}");
                }
                if (member == null) return null;
                current = member is FieldInfo fi
                    ? fi.GetValue(current)
                    : ((PropertyInfo)member).GetValue(current);
            }
            return current;
        }

        internal static void ClearCache() { _cache.Clear(); _goCache.Clear(); _compCache.Clear(); }
    }
}
