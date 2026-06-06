using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class HubCardButton
    {
        public static VisualElement Build(string icon, string title, string subtitle, Action onClick)
        {
            var card = new VisualElement();
            card.AddToClassList("hub-card");
            if (onClick != null)
                card.AddManipulator(new Clickable(onClick));

            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("hub-card-icon");

            var col = new VisualElement();
            col.AddToClassList("hub-card-col");

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("hub-card-title");

            var subLabel = new Label(subtitle);
            subLabel.AddToClassList("hub-card-subtitle");

            col.Add(titleLabel);
            col.Add(subLabel);
            card.Add(iconLabel);
            card.Add(col);
            return card;
        }
    }
}
