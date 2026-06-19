using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>Setup wizard EditorWindow — hosts WizardScreenHost and renders each page.</summary>
    public class SetupWizard : EditorWindow
    {
        private WizardScreenHost _host;
        private VisualElement    _pageSlot;

        [MenuItem("MCP/Setup Wizard", priority = 2)]
        [MenuItem("Window/Unity MCP/Setup Wizard", priority = 200)]
        public static void ShowWindow()
        {
            var w = GetWindow<SetupWizard>("MCP Setup");
            w.minSize = new Vector2(360, 440);
        }

        private void CreateGUI()
        {
            var ss = MCPEditorUtils.LoadStyleSheet("Wizard/SetupWizard.uss");
            if (ss != null) rootVisualElement.styleSheets.Add(ss);

            _host = new WizardScreenHost(Close, OnNavigated);

            // Dots bar
            var dotsBar = new VisualElement();
            dotsBar.AddToClassList("wiz-dots");

            var dots = new VisualElement[_host.ScreenCount];
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = new VisualElement();
                dots[i].AddToClassList("wiz-dot");
                dotsBar.Add(dots[i]);
            }
            _host.SetDots(dots);

            _pageSlot = new VisualElement();
            _pageSlot.style.flexGrow = 1;

            rootVisualElement.Add(dotsBar);
            rootVisualElement.Add(_pageSlot);

            _host.Navigate(0);
        }

        private void OnNavigated()
        {
            _pageSlot.Clear();
            var screen = _host.CurrentScreen;
            if (screen == null) return;
            _pageSlot.Add(screen.Build());
            screen.OnEnter();
        }
    }
}
