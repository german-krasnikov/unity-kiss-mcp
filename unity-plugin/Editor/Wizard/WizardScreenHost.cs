using System;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Wizard.Screens;

namespace UnityMCP.Editor.Wizard
{
    /// <summary>
    /// Pure logic host for the setup wizard — testable without EditorWindow.
    /// Manages screen navigation, dot updates, and completion.
    /// </summary>
    public sealed class WizardScreenHost
    {
        private readonly IWizardScreen[] _screens;
        private VisualElement[] _dots;
        private Action _closeCallback;
        private Action _onNavigate;

        public int ScreenCount  => _screens.Length;
        public int CurrentIndex { get; private set; } = -1;

        /// <param name="closeCallback">Called on Complete() to close the window.</param>
        /// <param name="onNavigate">Called after each Navigate() so the window can re-render.</param>
        public WizardScreenHost(Action closeCallback = null, Action onNavigate = null)
        {
            _closeCallback = closeCallback;
            _onNavigate    = onNavigate;
            _screens = new IWizardScreen[]
            {
                new WelcomeScreen(Next, Complete),
                new PythonCheckScreen(Next, Back),
                new ServerTestScreen(Next, Back),
                new AiConfigScreen(Complete, Back),
            };
        }

        public IWizardScreen CurrentScreen =>
            CurrentIndex >= 0 && CurrentIndex < _screens.Length
                ? _screens[CurrentIndex]
                : null;

        public void SetDots(VisualElement[] dots) => _dots = dots;

        public void Navigate(int index)
        {
            if (index < 0 || index >= _screens.Length) return;
            CurrentScreen?.OnExit();
            CurrentIndex = index;
            RefreshDots();
            _onNavigate?.Invoke();
        }

        public void Next() => Navigate(CurrentIndex + 1);
        public void Back() => Navigate(CurrentIndex - 1);

        public void Complete()
        {
            EditorPrefs.SetBool("MCPWizard.Done", true);
            _closeCallback?.Invoke();
        }

        // ── Dots ──────────────────────────────────────────────────────────────

        private void RefreshDots()
        {
            if (_dots == null) return;
            for (int i = 0; i < _dots.Length; i++)
            {
                if (i == CurrentIndex) _dots[i].AddToClassList("wiz-dot--active");
                else                   _dots[i].RemoveFromClassList("wiz-dot--active");
            }
        }
    }
}
