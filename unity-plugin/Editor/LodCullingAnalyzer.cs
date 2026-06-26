using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    /// <summary>
    /// LOD group coverage + occlusion culling analysis.
    /// focus: lod|culling|occlusion|null=all
    /// </summary>
    internal static class LodCullingAnalyzer
    {
        public static string Analyze(string focus)
        {
            bool doLOD     = focus == null || focus == "lod";
            bool doCulling = focus == null || focus == "culling" || focus == "occlusion";

            var sb = new StringBuilder();
            if (doLOD)     AppendLOD(sb);
            if (doCulling) AppendCulling(sb);
            return sb.ToString().TrimEnd();
        }

        // ── LOD Section ────────────────────────────────────────────────────

        private static void AppendLOD(StringBuilder sb)
        {
            var lodGroups = Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None);
            int groups = lodGroups.Length;
            int crossFade = 0;
            var recs = new List<string>();

            sb.AppendLine("LOD GROUPS");
            sb.AppendLine($"  groups: {groups}");

            foreach (var group in lodGroups)
            {
                var path = ComponentSerializer.GetPath(group.gameObject);
                if (group.fadeMode == LODFadeMode.CrossFade)
                {
                    crossFade++;
                    sb.AppendLine($"  WARN: {path} LODFadeMode.CrossFade doubles draw calls during transition");
                }

                var lods = group.GetLODs();
                if (lods.Length >= 2)
                {
                    long poly0 = 0, poly1 = 0;
                    foreach (var r in lods[0].renderers)
                        poly0 += GetRendererPolyCount(r);
                    foreach (var r in lods[1].renderers)
                        poly1 += GetRendererPolyCount(r);
                    if (poly0 > 0 && poly1 > 0)
                    {
                        float ratio = (float)poly1 / poly0;
                        sb.AppendLine($"  {path} levels={lods.Length} LOD0/LOD1 ratio={ratio:F2}");
                    }
                }
            }

            // Flag high-poly MeshRenderers without LODGroup
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int noLOD = 0;
            foreach (var mr in renderers)
            {
                if (mr.GetComponentInParent<LODGroup>() != null) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf?.sharedMesh == null) continue;
                long poly = GetPolyCount(mf.sharedMesh);
                if (poly > 5000)
                {
                    noLOD++;
                    if (noLOD <= 5)
                        recs.Add($"  suggest LOD: {ComponentSerializer.GetPath(mr.gameObject)} ({RenderAnalyzer.FormatNum(poly)} tris)");
                }
            }
            if (noLOD > 0)
            {
                sb.AppendLine($"  WARN: {noLOD} high-poly renderers without LODGroup");
                foreach (var r in recs) sb.AppendLine(r);
                if (noLOD > 5) sb.AppendLine($"  ... and {noLOD - 5} more");
            }

            if (crossFade == 0 && groups > 0)
                sb.AppendLine("  LOD fade mode: ok");
        }

        // ── Culling Section ────────────────────────────────────────────────

        private static void AppendCulling(StringBuilder sb)
        {
            sb.AppendLine("CULLING");
            bool hasOcclusion = StaticOcclusionCulling.umbraDataSize > 0;
            bool isRunning    = StaticOcclusionCulling.isRunning;

            if (hasOcclusion)
            {
                sb.AppendLine($"  occlusion: baked ({StaticOcclusionCulling.umbraDataSize} bytes)");
            }
            else
            {
                sb.AppendLine("  WARN: occlusion culling not baked — run Window→Rendering→Occlusion Culling");
            }

            if (isRunning)
                sb.AppendLine("  occlusion bake: running");

            // Frustum culling is always on in Unity — confirm
            sb.AppendLine("  frustum culling: always active");
        }

        // ── Mesh helpers (zero-alloc via GetIndexCount) ────────────────────

        private static long GetPolyCount(Mesh m)
        {
            if (m == null) return 0;
            long total = 0;
            for (int s = 0; s < m.subMeshCount; s++)
                total += (long)m.GetIndexCount(s) / 3;
            return total;
        }

        private static long GetRendererPolyCount(Renderer r)
        {
            if (r == null) return 0;
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
            else mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            return GetPolyCount(mesh);
        }

    }
}
