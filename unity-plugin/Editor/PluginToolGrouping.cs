using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure, stateless grouping of plugin commands into ordered subcategories.
    /// No UnityEditor / UIElements deps — EditMode-testable without UI.
    /// </summary>
    internal static class PluginToolGrouping
    {
        /// <summary>
        /// Groups <paramref name="commands"/> by subcategory from
        /// <paramref name="plugin"/>.GetToolSubcategory().
        /// Null or empty subcategory → falls back to plugin.Name.
        /// Order: stable first-seen insertion order of subcategory.
        /// Within a subcategory, commands appear in their original list order.
        /// </summary>
        public static List<(string label, string[] tools)> GroupBySubcategory(
            IMCPPlugin plugin, string[] commands)
        {
            var order = new List<string>();
            var buckets = new Dictionary<string, List<string>>();

            foreach (var cmd in commands)
            {
                var sub = plugin.GetToolSubcategory(cmd);
                var key = string.IsNullOrEmpty(sub) ? plugin.Name : sub;
                if (!buckets.TryGetValue(key, out var list))
                {
                    order.Add(key);
                    buckets[key] = list = new List<string>();
                }
                list.Add(cmd);
            }

            var result = new List<(string, string[])>(order.Count);
            foreach (var key in order)
                result.Add((key, buckets[key].ToArray()));
            return result;
        }
    }
}
