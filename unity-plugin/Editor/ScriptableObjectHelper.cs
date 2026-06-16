using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ScriptableObjectHelper
    {
        static readonly string[] ValidActions = { "create", "get", "set", "list_types", "find" };

        internal static string Execute(string action, string argsJson)
        {
            switch (action)
            {
                case "create": return Create(argsJson);
                case "get":    return Get(argsJson);
                case "set":    return Set(argsJson);
                case "list_types": return ListTypes(argsJson);
                case "find":   return Find(argsJson);
                default:       throw new ArgumentException(ErrorHelper.InvalidAction(action, ValidActions));
            }
        }

        // ── create ────────────────────────────────────────────────────────────

        private static string Create(string args)
        {
            var typeName = JsonHelper.ExtractString(args, "type");
            var path = JsonHelper.ExtractString(args, "path");
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException("type is required");
            if (string.IsNullOrEmpty(path))     throw new ArgumentException("path is required");

            var type = FindSOType(typeName);
            if (type == null) throw new ArgumentException($"ScriptableObject type not found: {typeName}");

            AssetHelper.EnsureDirectory(path);
            var asset = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return $"Created: {path}";
        }

        // ── get ───────────────────────────────────────────────────────────────

        private static string Get(string args)
        {
            var asset = LoadAsset(JsonHelper.ExtractString(args, "path"));
            var so = new SerializedObject(asset);
            var prop = so.GetIterator();
            prop.Next(true);
            var sb = new StringBuilder();
            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Script") continue;
                sb.AppendLine($"{prop.name}: {ComponentSerializer.GetPropertyValueString(prop)}");
            }
            return sb.ToString().TrimEnd('\n', '\r');
        }

        // ── set ───────────────────────────────────────────────────────────────

        private static string Set(string args)
        {
            var path  = JsonHelper.ExtractString(args, "path");
            var prop  = JsonHelper.ExtractString(args, "prop");
            var value = JsonHelper.ExtractString(args, "value");
            if (string.IsNullOrEmpty(prop))  throw new ArgumentException("prop is required");
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("value is required");

            var asset = LoadAsset(path);
            var so = new SerializedObject(asset);
            var property = so.FindProperty(prop);
            if (property == null) throw new ArgumentException($"Property not found: {prop}");

            ValueParser.SetPropertyValue(property, value);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return "ok";
        }

        // ── list_types ────────────────────────────────────────────────────────

        private static string ListTypes(string args)
        {
            var filter = JsonHelper.ExtractString(args, "filter");
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .Where(t => string.IsNullOrEmpty(filter) || t.Name.Contains(filter))
                .Take(100)
                .Select(t => t.Name);
            return string.Join("\n", types);
        }

        // ── find ──────────────────────────────────────────────────────────────

        private static string Find(string args)
        {
            var typeName = JsonHelper.ExtractString(args, "type");
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException("type is required");

            var guids = AssetDatabase.FindAssets($"t:{typeName}");
            if (guids.Length == 0) return "(none)";
            return string.Join("\n", guids.Select(AssetDatabase.GUIDToAssetPath));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static ScriptableObject LoadAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required");
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) throw new ArgumentException($"ScriptableObject not found: {path}");
            return asset;
        }

        private static Type FindSOType(string name)
        {
            return TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .FirstOrDefault(t => t.Name == name || t.FullName == name);
        }

    }
}
