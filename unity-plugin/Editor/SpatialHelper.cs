using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class SpatialHelper
    {
        private static float ExtractFloat(string args, string key, float def)
        {
            var val = JsonHelper.ExtractString(args, key);
            return val != null && float.TryParse(val,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        private static Vector3 ParseVec3(string s)
        {
            // expects "(x,y,z)"
            var clean = s.Trim('(', ')');
            var parts = clean.Split(',');
            return new Vector3(
                float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
        }

        public static string Raycast(string fromArg, string toArg, string layerMask)
        {
            Physics.SyncTransforms();

            Vector3 origin = fromArg != null && fromArg.StartsWith("(")
                ? ParseVec3(fromArg)
                : (ComponentSerializer.FindObject(fromArg) is var fo && fo != null
                    ? fo.transform.position
                    : throw new System.ArgumentException(ErrorHelper.ObjectNotFound(fromArg)));

            Vector3 target = toArg != null && toArg.StartsWith("(")
                ? ParseVec3(toArg)
                : (ComponentSerializer.FindObject(toArg) is var to && to != null
                    ? to.transform.position
                    : throw new System.ArgumentException(ErrorHelper.ObjectNotFound(toArg)));

            var dir = target - origin;
            float dist = dir.magnitude;
            var hits = Physics.RaycastAll(origin, dir.normalized, dist);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            var sb = new StringBuilder();
            sb.AppendLine($"PATH: ({origin.x:F1},{origin.y:F1},{origin.z:F1}) -> ({target.x:F1},{target.y:F1},{target.z:F1}) dist={dist:F2}");

            int count = System.Math.Min(hits.Length, 20);
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var p = h.point;
                var col = h.collider.GetType().Name;
                sb.AppendLine($"HIT {i + 1}: {ComponentSerializer.GetPath(h.collider.gameObject)} at ({p.x:F1},{p.y:F1},{p.z:F1}) dist={h.distance:F2} [{col}]");
            }

            sb.Append(count == 0 ? "CLEAR (no hits)" : $"BLOCKED: {count} hit{(count > 1 ? "s" : "")}");
            return sb.ToString();
        }

        public static string SpatialMap(string root, float cellSize, float radius)
        {
            if (cellSize <= 0f) cellSize = 2f;

            var transforms = new List<Transform>();
            if (string.IsNullOrEmpty(root) || root == "/")
            {
                foreach (var go in Object.FindObjectsOfType<GameObject>())
                    if (go.transform.parent == null) transforms.Add(go.transform);
            }
            else
            {
                var rootGO = ComponentSerializer.FindObject(root);
                if (rootGO == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(root));
                foreach (Transform child in rootGO.GetComponentsInChildren<Transform>())
                    transforms.Add(child);
            }

            if (transforms.Count == 0) return "No objects found";

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var t in transforms)
            {
                minX = System.Math.Min(minX, t.position.x);
                maxX = System.Math.Max(maxX, t.position.x);
                minZ = System.Math.Min(minZ, t.position.z);
                maxZ = System.Math.Max(maxZ, t.position.z);
            }

            int cols = System.Math.Min(40, (int)((maxX - minX) / cellSize) + 1);
            int rows = System.Math.Min(40, (int)((maxZ - minZ) / cellSize) + 1);
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;

            var grid = new char[rows, cols];
            var legend = new Dictionary<char, string>();
            char nextLabel = 'A';

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = '.';

            foreach (var t in transforms)
            {
                int c = System.Math.Min(cols - 1, (int)((t.position.x - minX) / cellSize));
                int r = System.Math.Min(rows - 1, (int)((maxZ - t.position.z) / cellSize));
                if (grid[r, c] == '.')
                {
                    if (nextLabel > 'Z') continue;
                    grid[r, c] = nextLabel;
                    legend[nextLabel] = t.name;
                    nextLabel++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Map: XZ, cell={cellSize}m, {cols}x{rows}");
            var legendParts = new List<string>();
            foreach (var kv in legend) legendParts.Add($"{kv.Key}={kv.Value}");
            sb.AppendLine("# " + string.Join(" ", legendParts.ToArray()));

            for (int r = 0; r < rows; r++)
            {
                float z = maxZ - r * cellSize;
                sb.Append($"{z,4:F0} ");
                for (int c = 0; c < cols; c++) sb.Append($" {grid[r, c]} ");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }


        public static string Nearest(string fromPath, string componentFilter)
        {
            var from = ComponentSerializer.FindObject(fromPath);
            if (from == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(fromPath));
            var fromPos = from.transform.position;

            GameObject best = null;
            float bestDist = float.MaxValue;

            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == from) continue;
                if (!string.IsNullOrEmpty(componentFilter))
                {
                    bool hasComp = false;
                    foreach (var c in go.GetComponents<Component>())
                        if (c != null && c.GetType().Name.Contains(componentFilter)) { hasComp = true; break; }
                    if (!hasComp) continue;
                }
                float dist = Vector3.Distance(fromPos, go.transform.position);
                if (dist < bestDist) { bestDist = dist; best = go; }
            }

            if (best == null) return "No matching object found";
            var pos = best.transform.position;
            return $"{ComponentSerializer.GetPath(best)} dist={bestDist:F2} pos=({pos.x:F2},{pos.y:F2},{pos.z:F2})";
        }

        public static string InFrontOf(string path, float distance)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            var pos = go.transform.position + go.transform.forward * distance;
            return $"({pos.x:F2},{pos.y:F2},{pos.z:F2})";
        }

        public static string ObjectsInRadius(string path, float radius)
        {
            var from = ComponentSerializer.FindObject(path);
            if (from == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            var fromPos = from.transform.position;
            var sb = new StringBuilder();
            int count = 0;
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go == from || go.transform.IsChildOf(from.transform)) continue;
                float dist = Vector3.Distance(fromPos, go.transform.position);
                if (dist <= radius)
                {
                    sb.AppendLine($"  {ComponentSerializer.GetPath(go)} dist={dist:F2}");
                    count++;
                    if (count >= 20) { sb.AppendLine("  ...+more"); break; }
                }
            }
            if (count == 0) return "No objects within radius";
            return $"{count} objects within {radius}m:\n{sb.ToString().TrimEnd()}";
        }

        public static string BoundsInfo(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            var b = MultiViewCapture.ComputeBounds(go);
            return $"center=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) " +
                   $"size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) " +
                   $"min=({b.min.x:F2},{b.min.y:F2},{b.min.z:F2}) " +
                   $"max=({b.max.x:F2},{b.max.y:F2},{b.max.z:F2})";
        }

        public static string Execute(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "nearest" => Nearest(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "component") ?? ""),
                "in_front_of" => InFrontOf(
                    JsonHelper.ExtractString(args, "path"),
                    float.TryParse(JsonHelper.ExtractString(args, "distance") ?? "1",
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 1f),
                "objects_in_radius" => ObjectsInRadius(
                    JsonHelper.ExtractString(args, "path"),
                    float.TryParse(JsonHelper.ExtractString(args, "radius") ?? "5",
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 5f),
                "bounds_info" => BoundsInfo(JsonHelper.ExtractString(args, "path")),
                "raycast" => Raycast(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "target"),
                    JsonHelper.ExtractString(args, "layer_mask")),
                "spatial_map" => SpatialMap(
                    JsonHelper.ExtractString(args, "path"),
                    ExtractFloat(args, "cell_size", 2f),
                    ExtractFloat(args, "radius", 0f)),
                _ => throw new System.ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "nearest", "in_front_of", "objects_in_radius", "bounds_info", "raycast", "spatial_map" }))
            };
        }
    }
}
