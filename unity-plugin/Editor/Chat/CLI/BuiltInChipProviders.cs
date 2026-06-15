// 8 built-in IChipKindProvider implementations.
// All internal — registered by ChipKindRegistry.EnsureBuiltIns().
// HierarchyChipProvider.Navigate uses SceneObjectFinder (CLI-internal, no View dep).
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class HierarchyChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Hierarchy;
        public int    Priority => 100;
        public string IconName => "d_UnityEditor.SceneHierarchyWindow";
        public string HexColor => "#4a9eff";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath)
            => obj is GameObject go && !AssetDatabase.Contains(go);

        public ChipData Create(Object obj, string assetPath)
        {
            var go = (GameObject)obj;
            var path = ComponentSerializer.GetPath(go);
            var sep = path.IndexOf(":/", System.StringComparison.Ordinal);
            var display = sep >= 0 ? path.Substring(0, sep) + "/" + go.name : go.name;
            return new ChipData(Key, path, display, go.GetInstanceID());
        }

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            var bracket = chip.InstanceID != 0
                ? $"[{Key}:{chip.Path} #{chip.InstanceID}]"
                : $"[{Key}:{chip.Path}]";
            return ctx.Depth == "none" ? "" :
                   (ctx.Depth == "summary" || ctx.Depth == "full") && !string.IsNullOrEmpty(ctx.ResolvedSummary)
                       ? bracket + "\n" + ctx.ResolvedSummary
                       : bracket;
        }

        public void Navigate(string reference)
        {
            // RefParser strips " #id" so FindGameObject matches by clean path (bug fix P7).
            var go = SceneObjectFinder.FindGameObject(RefParser.Parse(Key, reference).Path);
            if (go == null) { Debug.LogWarning("[MCP Chat] Reference stale: " + reference); return; }
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }
    }

    internal sealed class SceneChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Scene;
        public int    Priority => 200;
        public string IconName => "d_SceneAsset Icon";
        public string HexColor => "#c084fc";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".unity");

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class ScriptChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Script;
        public int    Priority => 300;
        public string IconName => "d_cs Script Icon";
        public string HexColor => "#4ade80";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => obj is MonoScript;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference)
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(reference);
            if (ms == null) { Debug.LogWarning("[MCP Chat] Script not found: " + reference); return; }
            AssetDatabase.OpenAsset(ms);
        }
    }

    internal sealed class PrefabChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Prefab;
        public int    Priority => 400;
        public string IconName => "d_Prefab Icon";
        public string HexColor => "#60a5fa";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class MaterialChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Material;
        public int    Priority => 500;
        public string IconName => "d_Material Icon";
        public string HexColor => "#f97316";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => obj is Material;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class TextureChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Texture;
        public int    Priority => 600;
        public string IconName => "d_Texture Icon";
        public string HexColor => "#facc15";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => obj is Texture;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class SOChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.ScriptableObject;
        public int    Priority => 700;
        public string IconName => "d_ScriptableObject Icon";
        public string HexColor => "#fb7185";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => obj is ScriptableObject;

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class FolderChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Folder;
        public int    Priority => 150;
        public string IconName => "d_Folder Icon";
        public string HexColor => "#a78bfa";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath)
            => obj is DefaultAsset && !string.IsNullOrEmpty(assetPath)
               && AssetDatabase.IsValidFolder(assetPath);

        public ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal sealed class AssetChipProvider : IChipKindProvider
    {
        public string Key      => ChipKindKeys.Asset;
        public int    Priority => int.MaxValue;
        public string IconName => "d_DefaultAsset Icon";
        public string HexColor => "#94a3b8";
        public string DefaultDepth => "path";

        public bool CanHandle(Object obj, string assetPath) => true; // fallback

        public ChipData Create(Object obj, string assetPath)
        {
            var path = string.IsNullOrEmpty(assetPath) ? (obj != null ? obj.name : "") : assetPath;
            return new ChipData(Key, path, obj != null ? obj.name : path, 0);
        }

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public void Navigate(string reference) => BuiltInChipProviderHelper.PingAsset(reference);
    }

    internal static class BuiltInChipProviderHelper
    {
        internal static void PingAsset(string assetPath)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (obj == null) { Debug.LogWarning("[MCP Chat] Asset not found: " + assetPath); return; }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }
    }
}
