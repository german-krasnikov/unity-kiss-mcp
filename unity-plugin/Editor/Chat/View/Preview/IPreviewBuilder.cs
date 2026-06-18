// One-file-per-kind preview builder contract.
// Implement IPreviewBuilder, register via PreviewBuilderRegistry.Register().
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    // Value object describing a preview request.
    public readonly struct PreviewRequest
    {
        public string KindKey { get; }
        public string Path { get; }
        public int MaxHeight { get; }
        public object UserData { get; }

        public PreviewRequest(string kindKey, string path, int maxHeight = 120, object userData = null)
        {
            KindKey   = kindKey   ?? "";
            Path      = path      ?? "";
            MaxHeight = maxHeight > 0 ? maxHeight : 120;
            UserData  = userData;
        }
    }

    public interface IPreviewBuilder
    {
        /// <summary>True if this builder can produce a preview for the given kind/path.</summary>
        bool CanBuild(string kindKey, string path);

        /// <summary>Build a preview VisualElement. May be incomplete until async data arrives.</summary>
        VisualElement Build(PreviewRequest request, IPreviewContext context);
    }
}
