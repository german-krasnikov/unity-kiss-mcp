using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Chat
{
    /// <summary>
    /// Chip kind for scene regions drawn by SceneRegionTool.
    /// Key = "region". Path = 8-char UUID stored in SceneRegionState.
    /// CanHandle = false — regions are programmatic, never drag-dropped.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class RegionChipProvider : IChipKindProvider
    {
        static RegionChipProvider() => ChipKindRegistry.Register(new RegionChipProvider());

        public string   Key              => ChipKindKeys.Region;
        public int      Priority         => 120;
        public string   IconName         => "d_RectTool";
        public string   HexColor         => "#22d3ee";
        public string   DefaultDepth     => "summary";
        public string[] BarePathExtensions => System.Array.Empty<string>();

        public bool CanHandle(Object obj, string assetPath) => false;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, "", "empty region", 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";

            var bracket = $"[{Key}:{chip.Path}]";
            if (ctx.Depth == "path") return bracket;

            var snap = SceneRegionState.GetById(chip.Path);
            if (snap == null) return bracket + " (expired)";

            var body = (snap.AnnotationType ?? "region") switch
            {
                "point"       => FormatPoint(snap, ctx.Depth),
                "polyline"    => FormatPolyline(snap, ctx.Depth),
                "measurement" => FormatMeasurement(snap),
                _             => FormatSnapshot(snap, ctx.Depth),
            };
            return bracket + "\n" + body;
        }

        public void Navigate(string reference)
            => SceneRegionState.FrameRegion(reference);

        public void Ping(string reference)
            => SceneRegionState.HighlightRegion(reference);

        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Frame Region", _ => Navigate(reference));
            menu.AppendAction("Copy Object Paths", _ =>
            {
                var snap = SceneRegionState.GetById(reference);
                if (snap == null) return;
                EditorGUIUtility.systemCopyBuffer = string.Join("\n",
                    snap.ObjectPaths ?? System.Array.Empty<string>());
            });
            menu.AppendAction("Remove Region", _ => SceneRegionState.Remove(reference));
        }

        // ── Annotation payload formatting ─────────────────────────────────────

        static string FormatPoint(RegionSnapshot snap, string depth)
        {
            var sb = new StringBuilder();
            sb.Append($"pos=({snap.CenterX:F2},{snap.CenterZ:F2})");
            if (!string.IsNullOrEmpty(snap.Label))
                sb.Append($" label={snap.Label}");
            if (!string.IsNullOrEmpty(snap.SceneName))
                sb.Append($" scene={snap.SceneName}");

            if (depth == "full" && snap.ObjectPaths != null && snap.ObjectPaths.Length > 0)
            {
                sb.Append("\nnearest:");
                foreach (var p in snap.ObjectPaths) sb.Append('\n').Append(p);
            }
            return sb.ToString();
        }

        static string FormatPolyline(RegionSnapshot snap, string depth)
        {
            int pts = snap.VerticesFlat != null ? snap.VerticesFlat.Length / 2 : 0;
            var sb  = new StringBuilder();
            sb.Append($"type=polyline pts={pts} len={snap.LengthOrDistance:F1}m");
            if (!string.IsNullOrEmpty(snap.Label))
                sb.Append($" label={snap.Label}");
            if (!string.IsNullOrEmpty(snap.SceneName))
                sb.Append($" scene={snap.SceneName}");
            if (!string.IsNullOrEmpty(snap.Direction))
                sb.Append($" dir=({snap.Direction})");

            if (depth == "full")
            {
                // Deployment hint: tell AI which tool uses these waypoints
                sb.Append("\n# use these XZ waypoints for am_deploy_line");

                if (snap.VerticesFlat != null && snap.VerticesFlat.Length >= 4)
                {
                    sb.Append("\npoints:");
                    for (int i = 0; i < pts; i++)
                    {
                        sb.Append("\n  - x=");
                        sb.Append(snap.VerticesFlat[i * 2].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                        sb.Append(" z=");
                        sb.Append(snap.VerticesFlat[i * 2 + 1].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }

            if (pts >= 2)
            {
                sb.Append($"\nstart=({snap.VerticesFlat[0]:F2},{snap.VerticesFlat[1]:F2})");
                sb.Append($"\nend=({snap.VerticesFlat[(pts - 1) * 2]:F2},{snap.VerticesFlat[(pts - 1) * 2 + 1]:F2})");
            }

            if (depth == "full" && snap.ObjectPaths != null && snap.ObjectPaths.Length > 0)
            {
                sb.Append("\nnear(r=2m):");
                foreach (var p in snap.ObjectPaths) sb.Append('\n').Append(p);
            }
            return sb.ToString();
        }

        static string FormatMeasurement(RegionSnapshot snap)
        {
            var sb = new StringBuilder();
            sb.Append($"dist={snap.LengthOrDistance:F1}m");

            if (snap.VerticesFlat != null && snap.VerticesFlat.Length >= 4)
            {
                sb.Append($" from=({snap.VerticesFlat[0]:F1},{snap.VerticesFlat[1]:F1})");
                sb.Append($" to=({snap.VerticesFlat[2]:F1},{snap.VerticesFlat[3]:F1})");
            }
            if (!string.IsNullOrEmpty(snap.Label))
                sb.Append($" label={snap.Label}");
            if (!string.IsNullOrEmpty(snap.SceneName))
                sb.Append($" scene={snap.SceneName}");
            return sb.ToString();
        }

        // ── Region payload formatting ─────────────────────────────────────────

        static string FormatSnapshot(RegionSnapshot snap, string depth)
        {
            var sb = new StringBuilder();

            sb.Append($"area={snap.Area:F1}m² center=({snap.CenterX:F1},{snap.CenterZ:F1})");
            sb.Append($" bounds=({snap.MinX:F1},{snap.MinZ:F1})-({snap.MaxX:F1},{snap.MaxZ:F1})");
            sb.Append($" objects={snap.TotalCount}");

            if (!string.IsNullOrEmpty(snap.SceneName))
                sb.Append($" scene={snap.SceneName}");

            if (SceneRegionState.IsStale(snap.Id))
                sb.Append(" STALE");

            if (snap.Truncated)
                sb.Append($" (showing {snap.ObjectPaths?.Length ?? 0}/{snap.TotalCount})");

            var paths = snap.ObjectPaths;
            if (paths == null || paths.Length == 0)
            {
                sb.Append("\n(no objects)");
                return sb.ToString();
            }

            sb.Append('\n');

            if (depth == "full" || paths.Length <= 10)
            {
                foreach (var p in paths) sb.Append(p).Append('\n');
            }
            else
            {
                for (int i = 0; i < 10; i++) sb.Append(paths[i]).Append('\n');
                if (paths.Length > 10) sb.Append($"...+{paths.Length - 10} more");
            }

            if (depth == "full" && snap.VerticesFlat != null && snap.VerticesFlat.Length >= 4)
            {
                sb.Append("\npolygon=");
                int pairCount = snap.VerticesFlat.Length / 2;
                for (int i = 0; i < pairCount; i++)
                {
                    if (i > 0) sb.Append(';');
                    sb.Append(snap.VerticesFlat[i * 2].ToString("F2",
                        System.Globalization.CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(snap.VerticesFlat[i * 2 + 1].ToString("F2",
                        System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
