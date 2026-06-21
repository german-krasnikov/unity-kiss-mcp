using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class AnnotatedScreenshotChipProvider : IChipKindProvider
    {
        public string   Key              => ChipKindKeys.AnnotatedScreenshot;
        public int      Priority         => 40; // before Image (50)
        public string   IconName         => "d_RawImage Icon";
        public string   HexColor         => "#e74c3c"; // red annotation color
        public string   DefaultDepth     => "summary";
        public string[] BarePathExtensions => new string[0];

        static AnnotatedScreenshotChipProvider()
            => ChipKindRegistry.Register(new AnnotatedScreenshotChipProvider());

        public bool CanHandle(Object obj, string assetPath) => false;
        public ChipData Create(Object obj, string assetPath) => default;

        public string FormatPayload(ChipData chip, ChipPayloadContext ctx)
        {
            if (ctx.Depth == "none") return "";
            var bracket = $"[{Key}:{chip.DisplayName}]";
            if (ctx.Depth == "path") return bracket;

            var meta = AnnotationMetaWriter.Read(chip.Path);
            if (string.IsNullOrEmpty(meta))
                return bracket;
            return bracket + "\n" + meta.TrimEnd();
        }

        public void Navigate(string reference)
        {
            if (!string.IsNullOrEmpty(reference) && File.Exists(reference))
                EditorUtility.RevealInFinder(reference);
        }

        public void Ping(string reference) { }

        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Open in Finder", _ =>
            {
                if (!string.IsNullOrEmpty(reference) && File.Exists(reference))
                    EditorUtility.RevealInFinder(reference);
            });
        }
    }
}
