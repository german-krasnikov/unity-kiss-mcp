using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace UnityMCP.Editor
{
    internal static class ProjectSettingsHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            var target = JsonHelper.ExtractString(argsJson, "target");
            var prop = JsonHelper.ExtractString(argsJson, "prop");
            var value = JsonHelper.ExtractString(argsJson, "value");
            var indexStr = JsonHelper.ExtractString(argsJson, "index");

            return target switch
            {
                "tags"           => action == "get" ? GetTags()           : AddTag(value ?? prop),
                "layers"         => action == "get" ? GetLayers()         : SetLayer(
                    indexStr != null ? int.Parse(indexStr) : throw new System.Exception("'index' is required for layers set"), value),
                "sorting_layers" => action == "get" ? GetSortingLayers() : throw new System.Exception("sorting_layers is read-only"),
                "quality"        => action == "get" ? GetQuality()        : SetViaReflection(typeof(QualitySettings), prop, value),
                "physics"        => action == "get" ? GetPhysics()        : SetPhysics(prop, value),
                "time"           => action == "get" ? GetTime()           : SetViaReflection(typeof(Time), prop, value),
                "player"         => action == "get" ? GetPlayer()         : SetViaReflection(typeof(PlayerSettings), prop, value),
                _ => throw new System.Exception($"Unknown target '{target}'. Valid: tags, layers, sorting_layers, quality, physics, time, player")
            };
        }

        // ── tags ─────────────────────────────────────────────────────────────

        static string GetTags()
        {
            var sb = new StringBuilder();
            foreach (var t in InternalEditorUtility.tags)
                sb.AppendLine(t);
            return sb.ToString().TrimEnd();
        }

        static string AddTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) throw new System.Exception("value or prop is required for tags set");
            var tm = LoadTagManager();
            var tagsProp = tm.FindProperty("tags");
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tm.ApplyModifiedProperties();
            return "ok";
        }

        // ── layers ────────────────────────────────────────────────────────────

        static string GetLayers()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    sb.AppendLine($"{i}: {name}");
            }
            return sb.ToString().TrimEnd();
        }

        static string SetLayer(int index, string name)
        {
            if (index < 6) throw new System.Exception("Layers 0-5 are reserved by Unity. Use index >= 6.");
            var tm = LoadTagManager();
            var layersProp = tm.FindProperty("layers");
            layersProp.GetArrayElementAtIndex(index).stringValue = name;
            tm.ApplyModifiedProperties();
            return "ok";
        }

        // ── sorting layers ────────────────────────────────────────────────────

        static string GetSortingLayers()
        {
            var sb = new StringBuilder();
            foreach (var sl in SortingLayer.layers)
                sb.AppendLine($"{sl.name} (id={sl.id})");
            return sb.ToString().TrimEnd();
        }

        // ── quality ───────────────────────────────────────────────────────────

        static string GetQuality()
        {
            int level = QualitySettings.GetQualityLevel();
            return $"shadowDistance: {QualitySettings.shadowDistance}\n" +
                   $"vSyncCount: {QualitySettings.vSyncCount}\n" +
                   $"lodBias: {QualitySettings.lodBias}\n" +
                   $"pixelLightCount: {QualitySettings.pixelLightCount}\n" +
                   $"antiAliasing: {QualitySettings.antiAliasing}\n" +
                   $"currentLevel: {level} ({QualitySettings.names[level]})";
        }

        // ── physics ───────────────────────────────────────────────────────────

        static string GetPhysics()
        {
            var sb = new StringBuilder();
            sb.Append($"gravity: {Physics.gravity}\n");
            sb.Append($"defaultSolverIterations: {Physics.defaultSolverIterations}\n");
            sb.Append($"defaultContactOffset: {Physics.defaultContactOffset}\n");
            sb.Append($"bounceThreshold: {Physics.bounceThreshold}\n");
            AppendCollisionMatrix(sb);
            return sb.ToString().TrimEnd();
        }

        static void AppendCollisionMatrix(StringBuilder sb)
        {
            var disabled = new StringBuilder();
            for (int i = 0; i < 32; i++)
            {
                var nameI = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(nameI)) continue;
                for (int j = i; j < 32; j++)
                {
                    var nameJ = LayerMask.LayerToName(j);
                    if (string.IsNullOrEmpty(nameJ)) continue;
                    if (Physics.GetIgnoreLayerCollision(i, j))
                        disabled.Append(nameI).Append(" x ").Append(nameJ).AppendLine(": off");
                }
            }
            if (disabled.Length == 0)
                sb.AppendLine("--- Collision Matrix: all enabled ---");
            else
                sb.Append("--- Collision Matrix ---\n").Append(disabled);
        }

        static string SetPhysics(string prop, string value)
        {
            if (prop == "gravity")
            {
                Physics.gravity = ValueParser.ParseVector3(value);
                return "ok";
            }
            return SetViaReflection(typeof(Physics), prop, value);
        }

        // ── time ──────────────────────────────────────────────────────────────

        static string GetTime() =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "fixedDeltaTime: {0}\nmaximumDeltaTime: {1}\ntimeScale: {2}",
                Time.fixedDeltaTime, Time.maximumDeltaTime, Time.timeScale);

        // ── player ────────────────────────────────────────────────────────────

        static string GetPlayer() =>
            $"companyName: {PlayerSettings.companyName}\n" +
            $"productName: {PlayerSettings.productName}\n" +
            $"bundleVersion: {PlayerSettings.bundleVersion}";

        // ── helpers ───────────────────────────────────────────────────────────

        static SerializedObject LoadTagManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) throw new System.Exception("TagManager.asset not found");
            return new SerializedObject(assets[0]);
        }

        static string SetViaReflection(System.Type type, string prop, string value)
        {
            if (string.IsNullOrEmpty(prop)) throw new System.Exception("prop is required");
            if (value == null) throw new System.Exception("value is required");
            var pi = type.GetProperty(prop, BindingFlags.Static | BindingFlags.Public);
            if (pi == null) throw new System.Exception($"Property '{prop}' not found on {type.Name}");
            if (!pi.CanWrite) throw new System.Exception($"Property '{prop}' on {type.Name} is read-only");

            object parsed;
            if      (pi.PropertyType == typeof(float))   parsed = float.Parse(value, CultureInfo.InvariantCulture);
            else if (pi.PropertyType == typeof(int))     parsed = int.Parse(value);
            else if (pi.PropertyType == typeof(bool))    parsed = value == "true";
            else if (pi.PropertyType == typeof(Vector3)) parsed = ValueParser.ParseVector3(value);
            else                                         parsed = value;

            pi.SetValue(null, parsed);
            return "ok";
        }

    }
}
