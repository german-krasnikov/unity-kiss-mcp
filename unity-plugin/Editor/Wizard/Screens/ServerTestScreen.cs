using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Tests the MCP server TCP connection and shows port with counter animation.</summary>
    public sealed class ServerTestScreen : IWizardScreen
    {
        private readonly Action _onNext;
        private readonly Action _onBack;
        private VisualElement _root;
        private Label _portLabel;
        private Label _statusLabel;
        private VisualElement _orbEl;
        private IVisualElementScheduledItem _beatJob;

        public string Title => "Server";

        public ServerTestScreen(Action onNext, Action onBack)
        {
            _onNext = onNext;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("wiz-container");

            var title = new Label("Testing MCP Server connection...");
            title.AddToClassList("wiz-title");

            // Orb / heartbeat indicator
            var orbRow = new VisualElement();
            orbRow.style.flexDirection = FlexDirection.Row;
            orbRow.style.alignItems = Align.Center;
            orbRow.style.marginBottom = 12;

            _orbEl = new VisualElement();
            _orbEl.style.width  = 14;
            _orbEl.style.height = 14;
            _orbEl.style.borderTopLeftRadius     = 7;
            _orbEl.style.borderTopRightRadius    = 7;
            _orbEl.style.borderBottomLeftRadius  = 7;
            _orbEl.style.borderBottomRightRadius = 7;
            _orbEl.style.backgroundColor = new UnityEngine.Color(0.23f, 0.82f, 0.62f);
            _orbEl.style.marginRight = 8;

            _portLabel = new Label("Port: —");
            _portLabel.style.fontSize = 13;

            orbRow.Add(_orbEl);
            orbRow.Add(_portLabel);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("wiz-subtitle");

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;

            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });
            nav.Add(new Button(_onNext) { text = "Next →" });

            _root.Add(title);
            _root.Add(orbRow);
            _root.Add(_statusLabel);
            _root.Add(spacer);
            _root.Add(nav);

            return _root;
        }

        public void OnEnter()
        {
            if (_root == null) return;
            var (ok, detail) = SetupDiagnostics.CheckServer();
            _statusLabel.text = detail;

            if (ok)
            {
                int port = MCPServer.ServerPort;
                AnimateCounter(port);
                _beatJob = _root.schedule.Execute(Beat).Every(900);
                WizardAnimUtils.PulseOnce(_orbEl);
            }
            else
            {
                _orbEl.style.backgroundColor = new UnityEngine.Color(0.75f, 0.22f, 0.17f);
                _portLabel.text = "Port: not connected";
            }
        }

        public void OnExit()
        {
            _beatJob?.Pause();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void AnimateCounter(int targetPort)
        {
            int steps = 12;
            int step  = 0;
            _root.schedule.Execute(() =>
            {
                float t = (float)step / steps;
                int display = (int)(t * targetPort);
                _portLabel.text = $"Port: {display}";
                step++;
                if (step >= steps) _portLabel.text = $"Port: {targetPort}";
            }).Every(40).Until(() => step > steps);
        }

        private void Beat()
        {
            if (MCPServer.IsRunning)
                _orbEl.ToggleInClassList("wiz-pulse");
        }
    }
}
