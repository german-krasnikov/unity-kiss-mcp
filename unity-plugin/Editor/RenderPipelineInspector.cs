// Pipeline detection + reflection utilities (no hard URP/HDRP assembly references).
using System;
using System.Reflection;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    internal static class RenderPipelineInspector
    {
        private static string _cachedPipeline;

        public static string DetectPipeline()
        {
            if (_cachedPipeline != null) return _cachedPipeline;
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) return _cachedPipeline = "builtin";
            var n = asset.GetType().Name;
            if (n.Contains("Universal") || n.Contains("URP")) return _cachedPipeline = "urp";
            if (n.Contains("HDRP") || n.Contains("HDRenderPipeline")) return _cachedPipeline = "hdrp";
            return _cachedPipeline = "custom";
        }

        // Try public property first, then field. Returns null on miss.
        public static object GetPropOrField(Type t, object obj, string prop, string field)
        {
            if (prop != null)
            {
                var pi = t.GetProperty(prop,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (pi != null)
                    try { return pi.GetValue(obj); } catch { }
            }
            if (field != null)
            {
                var fi = t.GetField(field,
                    BindingFlags.NonPublic | BindingFlags.Public |
                    BindingFlags.Instance | BindingFlags.Static);
                if (fi != null)
                    try { return fi.GetValue(obj); } catch { }
            }
            return null;
        }

        // URP: additionalLightsRenderingMode (NOT supportsAdditionalLights — that property doesn't exist)
        public static bool URPAdditionalLightsEnabled()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) return false;
            var t = asset.GetType();
            var val = GetPropOrField(t, asset,
                "additionalLightsRenderingMode", "m_AdditionalLightsRenderingMode");
            if (val == null) return false;
            // LightRenderingMode enum: Disabled = 0
            return Convert.ToInt32(val) != 0;
        }

        // HDRP: shadow init params
        public static string GetHDRPShadowInfo()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) return null;
            var t = asset.GetType();
            var cascades = GetPropOrField(t, asset, "shadowCascadeCount", "m_ShadowCascadeCount");
            var dist     = GetPropOrField(t, asset, "shadowMaxDistance",   "m_ShadowMaxDistance");
            if (cascades == null && dist == null) return null;
            return $"cascades:{cascades ?? "?"} maxDist:{dist ?? "?"}";
        }
    }
}
