using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Data-driven AI tool config cards for all 8 backends.</summary>
    public sealed class AiConfigScreen : IWizardScreen
    {
        private readonly Action _onDone;
        private readonly Action _onBack;
        private VisualElement[] _cards;

        public string Title => "AI Tools";

        public AiConfigScreen(Action onDone, Action onBack)
        {
            _onDone = onDone;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var title = new Label("Configure AI Tools");
            title.AddToClassList("wiz-title");
            root.Add(title);

            int port     = MCPServer.IsRunning ? MCPServer.ServerPort : 9500;
            var allCards = AiToolCardFactory.Build(port);
            var external = allCards.Where(c => c.Action != CardAction.CopyPort).ToArray();
            var chat     = allCards.Where(c => c.Action == CardAction.CopyPort).ToArray();

            var cardElements = new System.Collections.Generic.List<VisualElement>();

            root.Add(MakeGroupLabel("External MCP Hosts"));
            foreach (var card in external) { var el = BuildCard(card, port); cardElements.Add(el); root.Add(el); }

            root.Add(MakeGroupLabel("In-Unity Chat Backends"));
            foreach (var card in chat) { var el = BuildCard(card, port); cardElements.Add(el); root.Add(el); }

            _cards = cardElements.ToArray();

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });
            nav.Add(new Button(_onDone) { text = "Done ✓" });
            root.Add(spacer);
            root.Add(nav);
            return root;
        }

        public void OnEnter()
        {
            if (_cards == null) return;
            for (int i = 0; i < _cards.Length; i++)
                WizardAnimUtils.SlideInRight(_cards[i], i * 100);
        }

        public void OnExit() { }

        // ── Private ───────────────────────────────────────────────────────────

        private static Label MakeGroupLabel(string text)
        {
            var lbl = new Label(text);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.fontSize   = 12;
            lbl.style.marginTop  = 10;
            lbl.style.marginBottom = 4;
            return lbl;
        }

        private static VisualElement BuildCard(BackendCard data, int port)
        {
            var card = new VisualElement();
            card.AddToClassList("wiz-card");

            var heading = new Label(data.Name);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 4;

            var body = new Label(data.Body);
            body.style.fontSize    = 11;
            body.style.whiteSpace  = WhiteSpace.Normal;
            body.style.marginBottom = 8;

            Button btn = null;
            btn = new Button(() =>
            {
                Dispatch(data, port);
                WizardAnimUtils.FlashClass(btn, "wiz-btn-copied", 800);
            }) { text = data.BtnLabel };

            card.Add(heading);
            card.Add(body);
            card.Add(btn);
            return card;
        }

        private static void Dispatch(BackendCard card, int port)
        {
            switch (card.Action)
            {
                case CardAction.CopyText:
                case CardAction.CopyPort:
                    GUIUtility.systemCopyBuffer = card.Payload;
                    break;
                case CardAction.WriteConfig:
                    WizardConfigWriter.Write(card.Name, card.Payload, port);
                    break;
            }
        }
    }
}
