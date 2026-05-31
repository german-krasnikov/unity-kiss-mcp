using System;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class ShaderSerializer
    {
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        /// <summary>
        /// Entry point. target="material" reads material values from scene renderer.
        /// Otherwise serializes shader properties (from scene object or asset path).
        /// </summary>
        public static string Serialize(string path, string target)
        {
            if (target == "material")
                return SerializeMaterial(path);
            return SerializeShader(path);
        }

        // Serialize shader — path is scene object or asset path (Assets/...)
        private static string SerializeShader(string path)
        {
            var shader = LoadShader(path);
            if (shader == null)
                throw new InvalidOperationException($"Shader not found: '{path}'");

            var sb = new StringBuilder();
            sb.Append("Shader: \"").Append(shader.name).AppendLine("\"");
            sb.AppendLine("properties:");
            AppendShaderProperties(sb, shader, null);
            AppendKeywordsLine(sb, shader.keywordSpace.keywords);
            AppendErrorsLine(sb, shader);
            sb.Append("passes: ").Append(shader.passCount);
            return sb.ToString().TrimEnd();
        }

        // Serialize material on scene renderer — path is scene object path
        private static string SerializeMaterial(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                throw new InvalidOperationException(ErrorHelper.ComponentNotFound("Renderer", go));

            var mat = renderer.sharedMaterial;
            if (mat == null)
                throw new InvalidOperationException($"No material on renderer of '{path}'");

            var sb = new StringBuilder();
            sb.Append("Material on '").Append(path).AppendLine("' (renderer index 0)");
            sb.Append("shader: ").AppendLine(mat.shader != null ? mat.shader.name : "none");
            AppendShaderProperties(sb, mat.shader, mat);
            AppendEnabledKeywords(sb, mat);
            return sb.ToString().TrimEnd();
        }

        private static Shader LoadShader(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Asset path (Assets/...)
            if (path.StartsWith("Assets/") || path.StartsWith("Packages/"))
                return AssetDatabase.LoadAssetAtPath<Shader>(path);

            // Scene object path — get shader from renderer
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return null;
            var renderer = go.GetComponent<Renderer>();
            return renderer?.sharedMaterial?.shader;
        }

        private static void AppendShaderProperties(StringBuilder sb, Shader shader, Material mat)
        {
            if (shader == null) return;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                var propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);
                sb.Append("  ").Append(propName).Append(": ");

                if (mat != null)
                    sb.AppendLine(GetMaterialValue(mat, propName, propType));
                else
                    sb.AppendLine(GetDefaultValue(shader, i, propType));
            }
        }

        private static string GetDefaultValue(Shader shader, int i, UnityEngine.Rendering.ShaderPropertyType type)
        {
            return type switch
            {
                UnityEngine.Rendering.ShaderPropertyType.Color =>
                    VecStr(shader.GetPropertyDefaultVectorValue(i)),
                UnityEngine.Rendering.ShaderPropertyType.Vector =>
                    VecStr(shader.GetPropertyDefaultVectorValue(i)),
                UnityEngine.Rendering.ShaderPropertyType.Float =>
                    shader.GetPropertyDefaultFloatValue(i).ToString("G4", IC),
                UnityEngine.Rendering.ShaderPropertyType.Range =>
                    shader.GetPropertyDefaultFloatValue(i).ToString("G4", IC),
                UnityEngine.Rendering.ShaderPropertyType.Texture => "Texture",
                UnityEngine.Rendering.ShaderPropertyType.Int =>
                    ((int)shader.GetPropertyDefaultFloatValue(i)).ToString(),
                _ => type.ToString()
            };
        }

        private static string GetMaterialValue(Material mat, string name, UnityEngine.Rendering.ShaderPropertyType type)
        {
            try
            {
                return type switch
                {
                    UnityEngine.Rendering.ShaderPropertyType.Color =>
                        VecStr(mat.GetColor(name)),
                    UnityEngine.Rendering.ShaderPropertyType.Vector =>
                        VecStr(mat.GetVector(name)),
                    UnityEngine.Rendering.ShaderPropertyType.Float =>
                        mat.GetFloat(name).ToString("G4", IC),
                    UnityEngine.Rendering.ShaderPropertyType.Range =>
                        mat.GetFloat(name).ToString("G4", IC),
                    UnityEngine.Rendering.ShaderPropertyType.Texture =>
                        mat.GetTexture(name) != null ? mat.GetTexture(name).name : "none",
                    UnityEngine.Rendering.ShaderPropertyType.Int =>
                        mat.GetInt(name).ToString(),
                    _ => type.ToString()
                };
            }
            catch (System.Exception ex) { Debug.LogWarning($"GetMaterialValue: {ex.Message}"); return "?"; }
        }

        private static void AppendKeywordsLine(StringBuilder sb, UnityEngine.Rendering.LocalKeyword[] keywords)
        {
            sb.Append("keywords: ");
            sb.AppendLine(keywords.Length > 0
                ? string.Join(" ", System.Array.ConvertAll(keywords, k => k.name))
                : "none");
        }

        private static void AppendEnabledKeywords(StringBuilder sb, Material mat)
        {
            var kw = mat.shaderKeywords;
            sb.Append("keywords: ");
            sb.AppendLine(kw.Length > 0 ? string.Join(" ", kw) : "none");
        }

        private static void AppendErrorsLine(StringBuilder sb, Shader shader)
        {
            sb.Append("errors: ");
            sb.AppendLine(ShaderUtil.ShaderHasError(shader) ? "yes" : "none");
        }

        private static string VecStr(Vector4 v) =>
            $"({v.x.ToString("G4", IC)},{v.y.ToString("G4", IC)},{v.z.ToString("G4", IC)},{v.w.ToString("G4", IC)})";

        private static string VecStr(Color c) =>
            $"({c.r.ToString("G4", IC)},{c.g.ToString("G4", IC)},{c.b.ToString("G4", IC)},{c.a.ToString("G4", IC)})";
    }
}
