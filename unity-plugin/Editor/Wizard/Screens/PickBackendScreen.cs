using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Wizard page 2 — scroll list of backends, pick one to configure.</summary>
    public sealed class PickBackendScreen : IWizardScreen
    {
        private readonly Action<BackendDescriptor> _onSelect;
        private readonly Action _onBack;
        private VisualElement[] _cards;

        public string Title => "Pick Backend";

        public PickBackendScreen(Action<BackendDescriptor> onSelect, Action onBack)
        {
            _onSelect = onSelect;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("wiz-container");

            var title = new Label("Choose your AI tool");
            title.AddToClassList("wiz-title");
            root.Add(title);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var backends = BackendDescriptor.All;
            _cards = new VisualElement[backends.Length];

            for (int i = 0; i < backends.Length; i++)
            {
                var backend = backends[i]; // capture for lambda
                var card = BuildCard(backend, IsDetected(backend));
                int idx = i;
                card.RegisterCallback<ClickEvent>(_ => _onSelect?.Invoke(backends[idx]));
                _cards[i] = card;
                scroll.Add(card);
            }

            root.Add(scroll);

            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });
            root.Add(nav);

            return root;
        }

        public void OnEnter()
        {
            if (_cards == null) return;
            for (int i = 0; i < _cards.Length; i++)
                WizardAnimUtils.SlideInRight(_cards[i], i * 60);
        }

        public void OnExit() { }

        /// <summary>Test hook — simulates clicking card at index.</summary>
        public void SimulateSelect(int index)
        {
            var backends = BackendDescriptor.All;
            if (index >= 0 && index < backends.Length)
                _onSelect?.Invoke(backends[index]);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static VisualElement BuildCard(BackendDescriptor b, bool detected)
        {
            var card = new VisualElement();
            card.AddToClassList("wiz-card");

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            var icon = new Label(b.Icon);
            icon.style.fontSize = 18;
            icon.style.marginRight = 8;

            var name = new Label(b.DisplayName);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1;

            header.Add(icon);
            header.Add(name);

            if (detected)
            {
                var badge = new Label("detected");
                badge.AddToClassList("wiz-badge-detected");
                badge.style.fontSize = 10;
                header.Add(badge);
            }

            var desc = new Label(b.Description);
            desc.style.fontSize = 11;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.opacity = 0.75f;

            card.Add(header);
            card.Add(desc);
            return card;
        }

        private static bool IsDetected(BackendDescriptor d)
        {
            if (d.Mechanism == InstallMechanism.ChatAuto) return true;
            if (!string.IsNullOrEmpty(d.BinaryName) && WhichExists(d.BinaryName)) return true;
            if (!string.IsNullOrEmpty(d.ConfigDir) && ConfigDirExists(d.ConfigDir)) return true;
            return false;
        }

        private static bool WhichExists(string tool)
        {
            try
            {
#if UNITY_EDITOR_WIN
                const string cmd = "where";
#else
                const string cmd = "which";
#endif
                var psi = new ProcessStartInfo(cmd, tool)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                proc?.WaitForExit(1000);
                return proc?.ExitCode == 0;
            }
            catch { return false; }
        }

        private static bool ConfigDirExists(string path)
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Directory.Exists(path.Replace("~", home));
            }
            catch { return false; }
        }
    }
}
