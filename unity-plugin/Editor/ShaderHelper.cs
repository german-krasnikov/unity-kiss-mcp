using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    public static class ShaderHelper
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // --- Preset templates ---

        const string UNLIT_TEMPLATE =
"Shader \"{name}\" {\n" +
"    Properties {\n" +
"        _MainTex (\"Texture\", 2D) = \"white\" {}\n" +
"        _Color (\"Color\", Color) = (1,1,1,1)\n" +
"    }\n" +
"    SubShader {\n" +
"        Tags { \"RenderType\"=\"Opaque\" }\n" +
"        Pass {\n" +
"            CGPROGRAM\n" +
"            #pragma vertex vert\n" +
"            #pragma fragment frag\n" +
"            #include \"UnityCG.cginc\"\n" +
"            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };\n" +
"            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };\n" +
"            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;\n" +
"            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }\n" +
"            fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * _Color; }\n" +
"            ENDCG\n" +
"        }\n" +
"    }\n" +
"}";

        const string TRANSPARENT_TEMPLATE =
"Shader \"{name}\" {\n" +
"    Properties {\n" +
"        _MainTex (\"Texture\", 2D) = \"white\" {}\n" +
"        _Color (\"Color\", Color) = (1,1,1,0.5)\n" +
"    }\n" +
"    SubShader {\n" +
"        Tags { \"Queue\"=\"Transparent\" \"RenderType\"=\"Transparent\" }\n" +
"        Blend SrcAlpha OneMinusSrcAlpha\n" +
"        ZWrite Off\n" +
"        Pass {\n" +
"            CGPROGRAM\n" +
"            #pragma vertex vert\n" +
"            #pragma fragment frag\n" +
"            #include \"UnityCG.cginc\"\n" +
"            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };\n" +
"            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };\n" +
"            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;\n" +
"            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }\n" +
"            fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv) * _Color; }\n" +
"            ENDCG\n" +
"        }\n" +
"    }\n" +
"}";

        const string LIT_TEMPLATE =
"Shader \"{name}\" {\n" +
"    Properties {\n" +
"        _Color (\"Color\", Color) = (1,1,1,1)\n" +
"        _MainTex (\"Albedo\", 2D) = \"white\" {}\n" +
"        _Metallic (\"Metallic\", Range(0,1)) = 0.5\n" +
"        _Smoothness (\"Smoothness\", Range(0,1)) = 0.5\n" +
"    }\n" +
"    SubShader {\n" +
"        Tags { \"RenderType\"=\"Opaque\" }\n" +
"        CGPROGRAM\n" +
"        #pragma surface surf Standard fullforwardshadows\n" +
"        sampler2D _MainTex; fixed4 _Color; half _Metallic; half _Smoothness;\n" +
"        struct Input { float2 uv_MainTex; };\n" +
"        void surf (Input IN, inout SurfaceOutputStandard o) {\n" +
"            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;\n" +
"            o.Albedo = c.rgb; o.Metallic = _Metallic; o.Smoothness = _Smoothness; o.Alpha = c.a;\n" +
"        }\n" +
"        ENDCG\n" +
"    }\n" +
"    Fallback \"Diffuse\"\n" +
"}";

        // --- Public API ---

        /// <summary>Create .shader file from preset or custom code.</summary>
        public static string Create(string path, string preset, string code, string shaderName)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is required for shader create");
            if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"path must end with .shader: {path}");
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(preset))
                throw new ArgumentException("Either preset (unlit/lit/transparent) or code is required");

            // Ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Derive shader name from filename if not provided
            if (string.IsNullOrEmpty(shaderName))
                shaderName = "Custom/" + Path.GetFileNameWithoutExtension(path);

            var shaderCode = string.IsNullOrEmpty(code)
                ? BuildPreset(preset, shaderName)
                : code;

            File.WriteAllText(path, shaderCode, Encoding.UTF8);
            AssetDatabase.ImportAsset(path);

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null && ShaderUtil.ShaderHasError(shader))
            {
                var msgs = ShaderUtil.GetShaderMessages(shader);
                var errs = msgs != null && msgs.Length > 0 ? msgs[0].message : "unknown error";
                return $"warning: shader created but has errors: {errs}\n" + ShaderSerializer.Serialize(path, null);
            }

            return ShaderSerializer.Serialize(path, null);
        }

        /// <summary>Set material property on scene object's renderer.</summary>
        public static string SetProperty(string path, string prop, string value)
        {
            var mat = GetMaterial(path);
            var shader = mat.shader;
            if (shader == null) throw new InvalidOperationException($"No shader on material of '{path}'");

            int idx = FindPropertyIndex(shader, prop);
            if (idx < 0) throw new ArgumentException($"Property '{prop}' not found on shader '{shader.name}'");

            Undo.RecordObject(mat, "Set shader property");
            ApplyProperty(mat, prop, shader.GetPropertyType(idx), value);
            EditorUtility.SetDirty(mat);

            return $"{prop}={value} on {path}";
        }

        /// <summary>Enable/disable shader keyword on material.</summary>
        public static string SetKeyword(string path, string keyword, string enabled)
        {
            var mat = GetMaterial(path);
            Undo.RecordObject(mat, "Set keyword");

            if (enabled == "true")
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);

            EditorUtility.SetDirty(mat);
            return $"keyword {keyword} {enabled} on {path}";
        }

        // --- Private helpers ---

        static string BuildPreset(string preset, string shaderName)
        {
            var template = preset switch
            {
                "unlit"        => UNLIT_TEMPLATE,
                "transparent"  => TRANSPARENT_TEMPLATE,
                "lit"          => LIT_TEMPLATE,
                _ => throw new ArgumentException($"Unknown preset '{preset}'. Valid: unlit, lit, transparent")
            };
            return template.Replace("{name}", shaderName);
        }

        static Material GetMaterial(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException(ErrorHelper.ComponentNotFound("Renderer", go));
            var mat = renderer.sharedMaterial;
            if (mat == null) throw new InvalidOperationException($"No material on renderer of '{path}'");
            return mat;
        }

        static int FindPropertyIndex(Shader shader, string prop)
        {
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
                if (shader.GetPropertyName(i) == prop) return i;
            return -1;
        }

        static void ApplyProperty(Material mat, string prop, UnityEngine.Rendering.ShaderPropertyType type, string value)
        {
            switch (type)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    if (!ColorUtility.TryParseHtmlString(value, out var col))
                        throw new ArgumentException($"Invalid color '{value}'. Use #RRGGBB or #RRGGBBAA");
                    mat.SetColor(prop, col);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    mat.SetFloat(prop, float.Parse(value, IC));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    mat.SetVector(prop, ValueParser.ParseVector4Lenient(value));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    var tex = AssetDatabase.LoadAssetAtPath<Texture>(value);
                    if (tex == null) throw new ArgumentException($"Texture not found: '{value}'");
                    mat.SetTexture(prop, tex);
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Int:
                    if (!int.TryParse(value, NumberStyles.Integer, IC, out var shaderIntVal))
                        throw new ArgumentException($"Invalid int: '{value}'");
                    mat.SetInt(prop, shaderIntVal);
                    break;
                default:
                    throw new ArgumentException($"Unsupported property type: {type}");
            }
        }

    }
}
