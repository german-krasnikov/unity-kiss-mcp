// 9 built-in IChipKindProvider implementations.
// All internal — registered by ChipKindRegistry.EnsureBuiltIns().
// AssetChipProviderBase: shared FormatPayload + Navigate(PingAsset) + DefaultDepth + Create.
// HierarchyChipProvider: fully custom (instance ID, depth/summary, SceneObjectFinder).
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal abstract class AssetChipProviderBase : IChipKindProvider
    {
        public abstract string Key      { get; }
        public abstract int    Priority { get; }
        public abstract string IconName { get; }
        public abstract string HexColor { get; }
        public virtual  string DefaultDepth => "path";

        public abstract bool CanHandle(Object obj, string assetPath);

        public virtual ChipData Create(Object obj, string assetPath)
            => new ChipData(Key, assetPath, obj.name, 0);

        public virtual string FormatPayload(ChipData chip, ChipPayloadContext ctx)
            => ctx.Depth == "none" ? "" : $"[{Key}:{chip.Path}]";

        public virtual void Navigate(string reference)
        {
            var obj = AssetDatabase.LoadAssetAtPath<Object>(reference);
            if (obj == null) { Debug.LogWarning("[MCP Chat] Asset not found: " + reference); return; }
            EditorGUIUtility.PingObject(obj);
            Selection.activeObject = obj;
        }
    }

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
            return new ChipData(Key, path, FormatHierarchyDisplay(path, go.name), go.GetInstanceID());
        }

        internal static string FormatHierarchyDisplay(string path, string leafName)
        {
            var sep = path.IndexOf(":/", System.StringComparison.Ordinal);
            return sep >= 0 ? "[" + path.Substring(0, sep) + "] " + leafName : leafName;
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

    internal sealed class SceneChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Scene;
        public override int    Priority => 200;
        public override string IconName => "d_SceneAsset Icon";
        public override string HexColor => "#c084fc";

        public override bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".unity");
    }

    internal sealed class ScriptChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Script;
        public override int    Priority => 300;
        public override string IconName => "d_cs Script Icon";
        public override string HexColor => "#4ade80";

        public override bool CanHandle(Object obj, string assetPath) => obj is MonoScript;

        public override void Navigate(string reference)
        {
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(reference);
            if (ms == null) { Debug.LogWarning("[MCP Chat] Script not found: " + reference); return; }
            AssetDatabase.OpenAsset(ms);
        }
    }

    internal sealed class PrefabChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Prefab;
        public override int    Priority => 400;
        public override string IconName => "d_Prefab Icon";
        public override string HexColor => "#60a5fa";

        public override bool CanHandle(Object obj, string assetPath)
            => obj != null && !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".prefab");
    }

    internal sealed class MaterialChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Material;
        public override int    Priority => 500;
        public override string IconName => "d_Material Icon";
        public override string HexColor => "#f97316";

        public override bool CanHandle(Object obj, string assetPath) => obj is Material;
    }

    internal sealed class TextureChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Texture;
        public override int    Priority => 600;
        public override string IconName => "d_Texture Icon";
        public override string HexColor => "#facc15";

        public override bool CanHandle(Object obj, string assetPath) => obj is Texture;
    }

    internal sealed class SOChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.ScriptableObject;
        public override int    Priority => 700;
        public override string IconName => "d_ScriptableObject Icon";
        public override string HexColor => "#fb7185";

        public override bool CanHandle(Object obj, string assetPath) => obj is ScriptableObject;
    }

    internal sealed class FolderChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Folder;
        public override int    Priority => 150;
        public override string IconName => "d_Folder Icon";
        public override string HexColor => "#a78bfa";

        public override bool CanHandle(Object obj, string assetPath)
            => obj is DefaultAsset && !string.IsNullOrEmpty(assetPath)
               && AssetDatabase.IsValidFolder(assetPath);
    }

    internal sealed class AssetChipProvider : AssetChipProviderBase
    {
        public override string Key      => ChipKindKeys.Asset;
        public override int    Priority => int.MaxValue;
        public override string IconName => "d_DefaultAsset Icon";
        public override string HexColor => "#94a3b8";

        public override bool CanHandle(Object obj, string assetPath) => true; // fallback

        public override ChipData Create(Object obj, string assetPath)
        {
            var path = string.IsNullOrEmpty(assetPath) ? (obj != null ? obj.name : "") : assetPath;
            return new ChipData(Key, path, obj != null ? obj.name : path, 0);
        }
    }
}
