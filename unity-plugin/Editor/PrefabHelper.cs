using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class PrefabHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            return action switch
            {
                "save"           => Save(argsJson),
                "create_variant" => CreateVariant(argsJson),
                "apply"          => Apply(argsJson),
                "revert"         => Revert(argsJson),
                "get_overrides"  => GetOverrides(argsJson),
                "unpack"         => Unpack(argsJson),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "save", "create_variant", "apply", "revert", "get_overrides", "unpack" }))
            };
        }

        private static string Save(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var assetPath = JsonHelper.ExtractString(args, "asset_path")
                ?? throw new ArgumentException("asset_path is required");

            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            AssetHelper.EnsureDirectory(assetPath);
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, assetPath, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            return $"ok: {assetPath}";
        }

        private static string CreateVariant(string args)
        {
            var basePath = JsonHelper.ExtractString(args, "base_path")
                ?? throw new ArgumentException("base_path is required");
            var variantPath = JsonHelper.ExtractString(args, "variant_path")
                ?? throw new ArgumentException("variant_path is required");

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath)
                ?? throw new InvalidOperationException($"Prefab not found: {basePath}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                AssetHelper.EnsureDirectory(variantPath);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                AssetDatabase.SaveAssets();
                return $"ok: {variantPath}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static string Apply(string args)
        {
            var go = RequirePrefabInstance(args);
            Undo.RegisterFullObjectHierarchyUndo(go, "Apply Prefab");
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            return $"Applied: {go.name}";
        }

        private static string Revert(string args)
        {
            var go = RequirePrefabInstance(args);
            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
            return $"Reverted: {go.name}";
        }

        private static string GetOverrides(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return "no overrides (not a prefab instance)";

            var sb = new StringBuilder();
            var mods = PrefabUtility.GetPropertyModifications(go);
            if (mods != null)
            {
                foreach (var m in mods)
                    sb.AppendLine($"  {m.propertyPath}: {m.value}");
            }

            var added = PrefabUtility.GetAddedComponents(go);
            foreach (var a in added)
                sb.AppendLine($"  +component: {a.instanceComponent.GetType().Name}");

            var removed = PrefabUtility.GetRemovedComponents(go);
            foreach (var r in removed)
                sb.AppendLine($"  -component: {r.assetComponent.GetType().Name}");

            return sb.Length > 0 ? sb.ToString().TrimEnd() : "no overrides";
        }

        private static string Unpack(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var recursive = JsonHelper.ExtractString(args, "recursive") == "true";
            var mode = recursive ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot;
            Undo.RegisterFullObjectHierarchyUndo(go, "Unpack Prefab");
            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);
            return $"Unpacked: {go.name}";
        }

        private static GameObject RequirePrefabInstance(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var go = ComponentSerializer.FindObject(path)
                ?? throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                throw new InvalidOperationException($"{go.name} is not a prefab instance");
            return go;
        }
    }
}
