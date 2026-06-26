// Lights, shadow audit, probe audit, light optimization.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor
{
    internal static partial class RenderAnalyzer
    {
        private static string Lights(string path, string detail)
        {
            var lights = GetLights(path);
            int dir = 0, point = 0, spot = 0, area = 0;
            int baked = 0, mixed = 0, realtime = 0, shadows = 0;

            foreach (var l in lights)
            {
                if      (l.type == LightType.Directional) dir++;
                else if (l.type == LightType.Point)       point++;
                else if (l.type == LightType.Spot)        spot++;
                else                                      area++;

                if      (l.lightmapBakeType == LightmapBakeType.Baked)    baked++;
                else if (l.lightmapBakeType == LightmapBakeType.Mixed)    mixed++;
                else if (l.lightmapBakeType == LightmapBakeType.Realtime) realtime++;

                if (l.shadows != LightShadows.None) shadows++;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"LIGHTS: {lights.Length} total");
            sb.AppendLine($"dir={dir} point={point} spot={spot} area={area}");
            sb.Append($"baked={baked} mixed={mixed} realtime={realtime} shadows={shadows}");

            var pipeline = RenderPipelineInspector.DetectPipeline();
            if (pipeline != "builtin")
                sb.Append($"\nPIPELINE: {pipeline.ToUpper()}");

            return sb.ToString();
        }

        private static string ShadowAudit(string path, string detail)
        {
            var lights = GetLights(path);
            var sb = new StringBuilder();
            sb.AppendLine("SHADOW AUDIT:");

            int issues = 0;
            float shadowDist = UnityEngine.QualitySettings.shadowDistance;

            foreach (var l in lights)
            {
                if (l.shadows == LightShadows.None) continue;
                if (l.type == LightType.Directional && shadowDist < 10f)
                {
                    sb.AppendLine($"  WARN: shadow distance very low ({shadowDist:F1}m)");
                    issues++;
                    break; // one warning per scene
                }
            }

            if (issues == 0) sb.AppendLine("  ok: no shadow issues detected");
            return sb.ToString().TrimEnd();
        }

        private static string ProbeAudit(string path, string detail)
        {
            var refProbes = path != null
                ? (ComponentSerializer.FindObject(path)?.GetComponentsInChildren<ReflectionProbe>()
                   ?? Array.Empty<ReflectionProbe>())
                : Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);

            var lpGroups = path != null
                ? (ComponentSerializer.FindObject(path)?.GetComponentsInChildren<LightProbeGroup>()
                   ?? Array.Empty<LightProbeGroup>())
                : Object.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None);

            int lpCount = 0;
            foreach (var g in lpGroups) lpCount += g.probePositions.Length;

            int baked = 0, realtime = 0;
            long vramEstimate = 0;
            foreach (var p in refProbes)
            {
                if      (p.mode == ReflectionProbeMode.Baked)    baked++;
                else if (p.mode == ReflectionProbeMode.Realtime) realtime++;

                if (p.mode == ReflectionProbeMode.Realtime &&
                    p.refreshMode == ReflectionProbeRefreshMode.EveryFrame)
                    vramEstimate += (long)p.resolution * p.resolution * 6 * 4;
            }

            var sb = new StringBuilder();
            sb.AppendLine("PROBE AUDIT:");
            sb.AppendLine($"  reflection probes: {refProbes.Length} (baked:{baked} realtime:{realtime})");
            sb.AppendLine($"  light probes: {lpCount}");
            if (vramEstimate > 0)
            {
                sb.AppendLine($"  realtime probe VRAM est: {FormatNum(vramEstimate)} bytes");
                sb.AppendLine("  WARN: EveryFrame realtime probes are expensive");
            }
            return sb.ToString().TrimEnd();
        }

        private static string LightOptimize(string path)
        {
            var lights = GetLights(path);
            var recs = new List<string>();

            int realtimeWithShadow = 0;
            foreach (var l in lights)
            {
                if (l.lightmapBakeType == LightmapBakeType.Realtime &&
                    l.shadows != LightShadows.None)
                    realtimeWithShadow++;
            }
            if (realtimeWithShadow > 3)
                recs.Add($"CRITICAL: {realtimeWithShadow} realtime shadow lights — bake or use mixed");

            var refProbes = path != null
                ? (ComponentSerializer.FindObject(path)?.GetComponentsInChildren<ReflectionProbe>()
                   ?? Array.Empty<ReflectionProbe>())
                : Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            int highRes = 0;
            foreach (var p in refProbes)
                if (p.resolution > 256) highRes++;
            if (highRes > 0)
                recs.Add($"reflection probes >256 res ({highRes}) — reduce to 128 or bake");

            var sb = new StringBuilder();
            sb.AppendLine("LIGHT OPTIMIZE:");
            if (recs.Count == 0)
                sb.AppendLine("  ok: no issues");
            else
                for (int i = 0; i < recs.Count; i++)
                    sb.AppendLine($"  [{i + 1}] {recs[i]}");

            return sb.ToString().TrimEnd();
        }

        private static Light[] GetLights(string path)
        {
            if (path != null)
            {
                var root = ComponentSerializer.FindObject(path);
                return root?.GetComponentsInChildren<Light>() ?? Array.Empty<Light>();
            }
            return Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        }
    }
}
