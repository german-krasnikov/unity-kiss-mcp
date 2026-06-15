// Extension point for rendering a parsed MdBlock into a UIToolkit VisualElement.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public interface IChatBlockRenderer
    {
        bool CanRender(in MdBlock block);
        VisualElement Render(in MdBlock block);
    }
}
