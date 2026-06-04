using System.Collections.Generic;
using System.Text;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Single source of truth for command parameter schemas.
    /// Used by BatchHelper to validate before execution — catches hallucinated params early.
    /// </summary>
    internal static class CommandSchema
    {
        private struct ParamDef
        {
            public string[] Required;
            public string[] Optional;
        }

        private static readonly Dictionary<string, ParamDef> Schema = new Dictionary<string, ParamDef>
        {
            // --- Read ---
            { "get_hierarchy", P(null, "depth", "root", "filter", "components") },
            { "get_component", P("path,type") },
            { "get_components_list", P("id") },
            { "get_object_detail", P("id") },
            { "find_objects", P(null, "name", "tag", "layer", "component") },
            { "inspect", P("paths", "components") },
            { "get_console", P(null, "count", "level", "first") },
            { "screenshot", P(null, "width", "height", "camera", "path", "supersample", "angles", "zoom", "offset", "fixed_size", "highlight", "show_colliders", "angle") },
            { "search_scene", P("query", "root", "limit") },
            { "validate_references", P("path", "depth", "ignore_optional", "verbose") },
            { "run_tests", P(null, "mode") },
            { "get_test_results", P(null) },
            { "get_compile_errors", P(null) },
            { "get_spatial_context", P("path", "radius") },
            { "fingerprint", P("path", "depth") },
            { "scan_scene", P(null, "bands") },
            { "check_colliders", P("path") },
            { "get_changes", P(null, "clear") },
            { "validate_layout", P(null, "root", "min_distance") },
            { "scene_diff", P(null) },
            { "get_schema", P(null, "type") },

            // --- Write ---
            { "set_property", P("path,component,prop,value") },
            { "set_property_delta", P("path,component,prop,delta") },
            { "create_object", P("name", "parent", "components", "primitive", "prefab_path") },
            { "delete_object", P(null, "id", "path") },
            { "set_active", P("path,active") },
            { "wire_event", P("path,component,event,target,method", "arg_type", "arg_value") },
            { "unwire_event", P("path,component,event", "index") },
            { "manage_component", P("path,type,action") },
            { "set_parent", P("path,parent", "world_position_stays") },
            { "set_material", P("path,color", "shader") },
            { "batch", P("commands", "on_error", "timeout_ms", "atomic") },
            { "recompile", P(null) },
            { "checkpoint", P(null, "label") },
            { "execute_code", P("code", "undo_label") },

            // --- Runtime ---
            { "invoke_method", P("path,component,method", "args") },
            { "set_runtime_property", P("path,component,field,value") },
            { "query_state", P("queries") },
            { "wait_until", P("path,component,field,value", "timeout", "negate") },
            { "move_to", P("path,position", "timeout") },
            { "test_step", P("path,position", "checks_before", "checks_after", "wait_after", "timeout") },
            { "run_playtest", P("script", "timeout") },
            { "fuzz_playtest", P(null, "steps", "seed", "timeout") },

            // --- Consolidated ---
            { "scene", P("action", "path") },
            { "animation", P("action,path", "clip", "clip_name", "property", "keys", "time") },
            { "timeline", P("action,path", "track", "track_type", "clip", "binding", "start", "duration", "blend_in", "blend_out", "asset_path", "director_path", "tracks", "time") },
            { "references", P("action,path", "children", "depth", "source", "target", "mappings") },
            { "editor", P(null, "action", "path") },
            { "animator", P("action,path", "state", "states", "params", "source", "target", "conditions", "duration", "exit_time", "has_exit_time", "type", "name") },
            { "particle", P("action,path", "name", "module", "prop", "value", "preset") },
            { "shader", P("action,path", "target", "preset", "code", "shader_name", "prop", "value", "keyword", "enabled", "node_type", "node_id", "node_action", "output_node", "output_slot", "input_node", "input_slot", "edge_action") },
            { "create_ui", P("type", "name", "parent", "anchor", "pos", "size", "pivot", "color", "text", "fontSize") },
            { "set_rect", P("path", "anchor", "pos", "size", "pivot", "offsetMin", "offsetMax") },
            { "menu", P("action", "path") },
            { "asset", P("action", "path", "type", "name", "folder", "source", "dest", "prop", "value", "recursive", "labels") },
            { "project_settings", P("action,target", "prop", "value", "index") },
            { "material", P("action", "path", "object_path", "shader", "prop", "value", "source", "targets") },
            { "prefab", P("action", "path", "asset_path", "base_path", "variant_path", "recursive") },
            { "scriptable_object", P("action", "path", "type", "prop", "value", "filter") },
            { "spatial_query", P("action", "path", "target", "distance", "radius", "component", "cell_size", "layer_mask", "center") },

            // --- Legacy aliases ---
            { "new_scene", P(null) },
            { "open_scene", P("path") },
            { "save_scene", P(null, "path") },
            { "discard_changes", P(null) },
            { "get_animation", P("path", "clip", "time") },
            { "create_animation", P("path", "clip_name", "property", "keys") },
            { "edit_animation", P("path,clip,action", "property", "keys") },
            { "preview_animation", P("path,clip", "action", "time") },
            { "get_timeline", P("path", "track") },
            { "create_timeline", P("asset_path", "director_path", "tracks") },
            { "edit_timeline", P("path,action", "track", "track_type", "clip", "binding", "start", "duration", "blend_in", "blend_out") },
            { "preview_timeline", P("path", "action", "time") },
            { "get_references", P("path", "children", "depth") },
            { "find_references_to", P("path") },
            { "remap_references", P("source,target", "mappings") },

            // --- Meta ---
            { "ping", P(null) },
            { "get_version", P(null) },
            { "get_enabled_tools", P(null) },
            { "compile_status", P(null) },
        };

        /// <summary>Validate command + args. Returns null if valid, error message if not.</summary>
        public static string Validate(string cmd, string argsJson)
        {
            if (cmd != null && !Schema.ContainsKey(cmd) && CommandRegistry.IsRegistered(cmd)) return null;

            if (!Schema.TryGetValue(cmd, out var def))
            {
                var best = StringDistance.ClosestMatch(cmd, Schema.Keys);
                return best != null
                    ? $"Unknown command '{cmd}'. Did you mean '{best}'?"
                    : $"Unknown command '{cmd}'.";
            }

            // Check required params
            var missing = new List<string>(2);
            foreach (var req in def.Required)
                if (JsonHelper.ExtractString(argsJson, req) == null)
                    missing.Add(req);

            if (missing.Count > 0)
                return $"'{cmd}' missing required: {string.Join(", ", missing.ToArray())}. Valid: {Join(def)}.";

            // Check for unknown (hallucinated) params
            var keys = ExtractKeys(argsJson);
            var valid = AllParams(def);
            var sb = new StringBuilder();
            foreach (var key in keys)
            {
                if (!valid.Contains(key))
                {
                    var closest = StringDistance.ClosestMatch(key, valid);
                    sb.Append(closest != null
                        ? $"Unknown param '{key}' for '{cmd}'. Did you mean '{closest}'? "
                        : $"Unknown param '{key}' for '{cmd}'. ");
                }
            }
            if (sb.Length > 0)
            {
                sb.Append($"Valid: {Join(def)}.");
                return sb.ToString();
            }

            return null;
        }

        // --- Helpers ---

        /// <summary>Compact builder: P("req1,req2", "opt1", "opt2")</summary>
        private static ParamDef P(string required, params string[] optional)
        {
            return new ParamDef
            {
                Required = string.IsNullOrEmpty(required) ? System.Array.Empty<string>() : required.Split(','),
                Optional = optional ?? System.Array.Empty<string>()
            };
        }

        private static HashSet<string> AllParams(ParamDef def)
        {
            var set = new HashSet<string>();
            foreach (var r in def.Required) set.Add(r);
            foreach (var o in def.Optional) set.Add(o);
            return set;
        }

        private static string Join(ParamDef def)
        {
            var all = new List<string>(def.Required.Length + def.Optional.Length);
            all.AddRange(def.Required);
            all.AddRange(def.Optional);
            return string.Join(", ", all.ToArray());
        }

        /// <summary>Extract top-level keys from flat JSON object.</summary>
        internal static List<string> ExtractKeys(string json)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(json) || json == "{}") return keys;
            int i = 0;
            while (i < json.Length)
            {
                var q1 = json.IndexOf('"', i);
                if (q1 == -1) break;
                var q2 = json.IndexOf('"', q1 + 1);
                if (q2 == -1) break;
                keys.Add(json.Substring(q1 + 1, q2 - q1 - 1));
                var colon = json.IndexOf(':', q2);
                if (colon == -1) break;
                i = colon + 1;
                while (i < json.Length && json[i] == ' ') i++;
                if (i < json.Length && json[i] == '"')
                {
                    i++;
                    while (i < json.Length)
                    {
                        if (json[i] == '"')
                        {
                            int backslashes = 0;
                            int b = i - 1;
                            while (b >= 0 && json[b] == '\\') { backslashes++; b--; }
                            if (backslashes % 2 == 0) break;
                        }
                        i++;
                    }
                    if (i < json.Length) i++;
                }
                else
                {
                    while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                }
                if (i < json.Length && json[i] == ',') i++;
            }
            return keys;
        }
    }
}
