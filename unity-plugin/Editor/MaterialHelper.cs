using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class MaterialHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            return action switch
            {
                "create" => Create(argsJson),
                "get" => Get(argsJson),
                "set" => Set(argsJson),
                "copy" => Copy(argsJson),
                "list_properties" => ListProperties(argsJson),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "create", "get", "set", "copy", "list_properties" }))
            };
        }

        private static string Create(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var shaderName = JsonHelper.ExtractString(args, "shader") ?? "Standard";

            AssetHelper.EnsureDirectory(path);
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException($"Shader not found: {shaderName}");

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return $"ok: {path}";
        }

        private static Material ResolveMaterial(string args)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var objectPath = JsonHelper.ExtractString(args, "object_path");

            if (path != null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new InvalidOperationException($"Material not found: {path}");
                return mat;
            }
            if (objectPath != null)
            {
                var go = ComponentSerializer.FindObject(objectPath);
                if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(objectPath));
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new InvalidOperationException($"No Renderer on: {objectPath}");
                if (renderer.sharedMaterial == null) throw new InvalidOperationException($"Renderer on '{objectPath}' has no material assigned");
                return renderer.sharedMaterial;
            }
            throw new ArgumentException("path or object_path is required");
        }

        private static string Get(string args)
        {
            var mat = ResolveMaterial(args);
            var sb = new StringBuilder();
            sb.AppendLine($"Shader: {mat.shader.name}");

            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                var name = ShaderUtil.GetPropertyName(mat.shader, i);
                var type = ShaderUtil.GetPropertyType(mat.shader, i);
                sb.AppendLine(FormatProperty(mat, name, type));
            }
            return sb.ToString().TrimEnd();
        }

        private static string FormatProperty(Material mat, string name, ShaderUtil.ShaderPropertyType type)
        {
            return type switch
            {
                ShaderUtil.ShaderPropertyType.Color =>
                    $"{name}: {mat.GetColor(name)} [Color]",
                ShaderUtil.ShaderPropertyType.Float or ShaderUtil.ShaderPropertyType.Range =>
                    $"{name}: {mat.GetFloat(name).ToString("G4", CultureInfo.InvariantCulture)} [Float]",
                ShaderUtil.ShaderPropertyType.TexEnv =>
                    $"{name}: {AssetDatabase.GetAssetPath(mat.GetTexture(name))} [Texture]",
                ShaderUtil.ShaderPropertyType.Vector =>
                    $"{name}: {mat.GetVector(name)} [Vector]",
                _ => $"{name}: ? [{type}]"
            };
        }

        private static string Set(string args)
        {
            var mat = ResolveMaterial(args);
            var prop = JsonHelper.ExtractString(args, "prop")
                ?? throw new ArgumentException("prop is required");
            var value = JsonHelper.ExtractString(args, "value")
                ?? throw new ArgumentException("value is required");

            int idx = mat.shader.FindPropertyIndex(prop);

            // Keyword (e.g. _EMISSION) — not in shader property block
            if (idx < 0)
            {
                if (value == "true") { Undo.RecordObject(mat, "Enable Keyword"); mat.EnableKeyword(prop); EditorUtility.SetDirty(mat); return "ok"; }
                if (value == "false") { Undo.RecordObject(mat, "Disable Keyword"); mat.DisableKeyword(prop); EditorUtility.SetDirty(mat); return "ok"; }
                throw new InvalidOperationException($"Property not found: {prop}");
            }

            Undo.RecordObject(mat, "Set Material Property");
            var type = mat.shader.GetPropertyType(idx);
            switch (type)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    mat.SetFloat(prop, float.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    mat.SetColor(prop, ValueParser.ParseColor(value));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    mat.SetVector(prop, ValueParser.ParseVector4Lenient(value));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(value);
                    if (tex == null) throw new InvalidOperationException($"Texture not found: {value}");
                    mat.SetTexture(prop, tex);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Int:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var matIntVal))
                        throw new ArgumentException($"Invalid int: '{value}'");
                    mat.SetInt(prop, matIntVal);
                    break;
            }

            EditorUtility.SetDirty(mat);
            return "ok";
        }

        private static string Copy(string args)
        {
            var sourcePath = JsonHelper.ExtractString(args, "source")
                ?? throw new ArgumentException("source is required");
            var targets = JsonHelper.ExtractString(args, "targets")
                ?? throw new ArgumentException("targets is required");

            Material mat;
            if (sourcePath.StartsWith("Assets/", StringComparison.Ordinal) || sourcePath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (mat == null) throw new InvalidOperationException($"Material not found at: {sourcePath}");
            }
            else
            {
                var sourceGo = ComponentSerializer.FindObject(sourcePath);
                if (sourceGo == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(sourcePath));
                var sourceRenderer = sourceGo.GetComponent<Renderer>();
                if (sourceRenderer == null) throw new InvalidOperationException($"No Renderer on: {sourcePath}");
                mat = sourceRenderer.sharedMaterial;
            }

            int count = 0;
            foreach (var t in targets.Split(','))
            {
                var tPath = t.Trim();
                if (tPath.Length == 0) continue;
                var go = ComponentSerializer.FindObject(tPath);
                if (go == null) continue;
                var r = go.GetComponent<Renderer>();
                if (r == null) continue;
                Undo.RecordObject(r, "Copy Material");
                r.sharedMaterial = mat;
                EditorUtility.SetDirty(r);
                count++;
            }
            return $"ok: {count} copied";
        }

        private static string ListProperties(string args)
        {
            var mat = ResolveMaterial(args);
            var sb = new StringBuilder();
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                var name = ShaderUtil.GetPropertyName(mat.shader, i);
                var type = ShaderUtil.GetPropertyType(mat.shader, i);
                sb.AppendLine($"{name}: {type}");
            }
            return sb.ToString().TrimEnd();
        }

    }
}
