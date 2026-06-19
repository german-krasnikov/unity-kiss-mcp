using System;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard.Screens
{
    /// <summary>Runs Python environment check and shows result with animation.</summary>
    public sealed class PythonCheckScreen : IWizardScreen
    {
        private readonly Action _onNext;
        private readonly Action _onBack;
        private VisualElement _root;
        private VisualElement _scanRow;
        private VisualElement[] _scanDots;
        private Label _resultLabel;
        private VisualElement _resultRow;
        private VisualElement _flowRow;
        private int _dotTick;
        private IVisualElementScheduledItem _scanJob;

        public string Title => "Python";

        public PythonCheckScreen(Action onNext, Action onBack)
        {
            _onNext = onNext;
            _onBack = onBack;
        }

        public VisualElement Build()
        {
            _root = new VisualElement();
            _root.AddToClassList("wiz-container");

            var title = new Label("Checking Python environment...");
            title.AddToClassList("wiz-title");

            // Scan dots
            _scanRow = new VisualElement();
            _scanRow.style.flexDirection = FlexDirection.Row;
            _scanRow.style.marginBottom = 12;
            _scanDots = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                _scanDots[i] = new VisualElement();
                _scanDots[i].AddToClassList("wiz-scan-dot");
                _scanRow.Add(_scanDots[i]);
            }

            // Result row (hidden initially)
            _resultRow = new VisualElement();
            _resultRow.AddToClassList("wiz-status-row");
            _resultRow.style.display = DisplayStyle.None;
            var icon = new Label();
            icon.AddToClassList("wiz-status-icon");
            _resultLabel = new Label();
            _resultRow.Add(icon);
            _resultRow.Add(_resultLabel);

            // Flow diagram
            _flowRow = BuildFlowRow();
            _flowRow.style.marginTop = 12;

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;

            var nav = new VisualElement();
            nav.AddToClassList("wiz-nav");
            nav.Add(new Button(_onBack) { text = "← Back" });
            nav.Add(new Button(_onNext) { text = "Next →" });

            _root.Add(title);
            _root.Add(_scanRow);
            _root.Add(_resultRow);
            _root.Add(_flowRow);
            _root.Add(spacer);
            _root.Add(nav);

            return _root;
        }

        public void OnEnter()
        {
            if (_root == null) return;
            _dotTick = 0;
            _scanJob = _root.schedule.Execute(TickDots).Every(250);
            _root.schedule.Execute(RunCheck).StartingIn(400);
        }

        public void OnExit()
        {
            _scanJob?.Pause();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void TickDots()
        {
            int lit = _dotTick % 3;
            for (int i = 0; i < 3; i++)
                _scanDots[i].EnableInClassList("wiz-scan-dot--lit", i == lit);
            _dotTick++;
        }

        private void RunCheck()
        {
            var serverDir = ResolveServerDir();
            var (ok, detail) = SetupDiagnostics.CheckPython(serverDir);
            ShowResult(ok, detail);
        }

        private void ShowResult(bool ok, string detail)
        {
            _scanJob?.Pause();
            _scanRow.style.display = DisplayStyle.None;
            _resultRow.style.display = DisplayStyle.Flex;

            var icon = _resultRow[0] as Label;
            if (icon != null) icon.text = ok ? "✓" : "✗";
            if (icon != null) icon.AddToClassList(ok ? "wiz-status-ok" : "wiz-status-fail");
            _resultLabel.text = detail;

            if (ok)
            {
                WizardAnimUtils.FadeIn(_resultRow);
                FlashFlowSequential();
            }
            else
            {
                WizardAnimUtils.ShakeX(_resultRow);
            }
        }

        private void FlashFlowSequential()
        {
            int i = 0;
            _flowRow.schedule.Execute(() =>
            {
                if (i < _flowRow.childCount)
                    WizardAnimUtils.PulseOnce(_flowRow[i++]);
            }).Every(200).Until(() => i >= _flowRow.childCount);
        }

        private static VisualElement BuildFlowRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.Center;
            row.style.alignItems = Align.Center;

            row.Add(MakeBox("Python"));
            row.Add(MakeArrow());
            row.Add(MakeBox("MCP Server"));
            row.Add(MakeArrow());
            row.Add(MakeBox("Unity"));

            return row;
        }

        private static Label MakeBox(string text)
        {
            var l = new Label(text);
            l.AddToClassList("wiz-card");
            l.style.paddingLeft = l.style.paddingRight = 8;
            l.style.paddingTop  = l.style.paddingBottom = 4;
            l.style.marginLeft  = l.style.marginRight = 4;
            return l;
        }

        private static Label MakeArrow() => new Label("→");

        private static string ResolveServerDir()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PythonCheckScreen).Assembly);
            if (pkg == null) return null;
            return Path.GetFullPath(Path.Combine(pkg.resolvedPath, "..", "server"));
        }
    }
}
