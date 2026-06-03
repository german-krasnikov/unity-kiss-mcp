using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    // Injects a MCP status pill into Unity's AppStatusBar via reflection. Fully defensive.
    [InitializeOnLoad]
    internal static class MCPStatusBarWidget
    {
        private static Label _pill;
        private static VisualElement _pillContainer;
        private static bool _injected;
        private static bool _pulseHigh;
        private static IVisualElementScheduledItem _pulseItem;

        static MCPStatusBarWidget()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.delayCall += TryInject; // AppStatusBar may not exist yet
        }

        private static void Cleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            try { _pulseItem?.Pause(); } catch { /* ignore — item may be disposed */ }
            try { _pillContainer?.RemoveFromHierarchy(); } catch { /* ignore — tree may be torn down */ }
            _pill          = null;
            _pillContainer = null;
            _pulseItem     = null;
            _injected      = false;
        }

        private static void TryInject()
        {
            if (_injected) return;
            try
            {
                var root = GetStatusBarRoot();
                if (root == null) { EditorApplication.delayCall += TryInject; return; }

                root.Q("mcp-status-pill")?.RemoveFromHierarchy();
                _pillContainer = BuildPill();
                root.Add(_pillContainer);
                _injected = true;
                PulseTick(); // initial label + colour; the 600ms schedule drives the rest
                _pulseItem = _pillContainer.schedule.Execute(PulseTick).Every(600);
            }
            catch (Exception e) { Debug.LogWarning($"[MCP] StatusBar injection failed: {e.Message}"); }
        }

        // Breathing pulse + label refresh, every 600ms (no per-frame update churn).
        // Up = bright/dim (1.0↔0.35), Listen = subdued (0.85↔0.55), Down = steady dim.
        private static void PulseTick()
        {
            if (_pill == null) return;
            var state = MCPStatusModel.GetState(MCPServer.IsRunning, MCPServer.IsClientConnected);
            RefreshLabel(state);
            if (state == MCPStatusModel.State.Down) { _pill.style.opacity = 0.55f; _pulseHigh = false; return; }
            _pulseHigh = !_pulseHigh;
            float hi = state == MCPStatusModel.State.Up ? 1.0f : 0.85f;
            float lo = state == MCPStatusModel.State.Up ? 0.35f : 0.55f;
            _pill.style.opacity = _pulseHigh ? hi : lo;
        }

        private static void RefreshLabel(MCPStatusModel.State state)
        {
            var text = MCPStatusModel.GetPill(state, MCPServer.ServerPort);
            if (_pill.text == text) return; // nothing changed — skip repaint
            _pill.text = text;
            ApplyPillColor(_pill, state);
        }

        // ── Build the pill VisualElement ──────────────────────────────────

        private static VisualElement BuildPill()
        {
            var container = new VisualElement();
            container.name                 = "mcp-status-pill";
            container.style.flexDirection  = FlexDirection.Row;
            container.style.alignItems     = Align.Center;
            container.style.position       = Position.Absolute;
            container.style.right          = 4;
            container.style.top            = 0;
            container.style.bottom         = 0;

            _pill = new Label("MCP");
            _pill.style.fontSize      = 10;
            _pill.style.paddingLeft   = 4;
            _pill.style.paddingRight  = 4;
            _pill.style.paddingTop    = 1;
            _pill.style.paddingBottom = 1;
            _pill.style.borderTopLeftRadius     = 3;
            _pill.style.borderTopRightRadius    = 3;
            _pill.style.borderBottomLeftRadius  = 3;
            _pill.style.borderBottomRightRadius = 3;
            _pill.style.unityFontStyleAndWeight = FontStyle.Bold;

            // Clicking pill opens an inline action menu (DRY via MCPActions).
            _pill.RegisterCallback<ClickEvent>(OnPillClick);

            // Inline colours (no USS file dependency — status bar has no sheet loader).
            ApplyPillColor(_pill, MCPStatusModel.State.Down);

            container.Add(_pill);
            return container;
        }

        private static void ApplyPillColor(Label label, MCPStatusModel.State state)
        {
            // Background + foreground per state (matches MCPStatus.uss palette).
            switch (state)
            {
                case MCPStatusModel.State.Up:
                    label.style.backgroundColor = new Color(0.12f, 0.48f, 0.36f, 0.85f);
                    label.style.color           = new Color(0.23f, 0.82f, 0.62f);
                    break;
                case MCPStatusModel.State.Listen:
                    label.style.backgroundColor = new Color(0.54f, 0.39f, 0.07f, 0.85f);
                    label.style.color           = new Color(0.91f, 0.64f, 0.23f);
                    break;
                default:
                    label.style.backgroundColor = new Color(0.43f, 0.17f, 0.23f, 0.85f);
                    label.style.color           = new Color(0.91f, 0.27f, 0.38f);
                    break;
            }
        }

        private static void OnPillClick(ClickEvent _)
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Restart"),  false, MCPActions.Restart);
            m.AddItem(new GUIContent("Reimport"), false, MCPActions.Reimport);
            m.AddItem(new GUIContent("Kill"),     false, MCPActions.Kill);
            m.AddSeparator("");
            m.AddItem(new GUIContent("Open Status"), false, MCPStatusWindow.ShowWindow);
            // Chat omitted: separate UNITY_MCP_CHAT assembly — hard dep avoided.
            m.DropDown(_pill.worldBound);
        }

        // ── Reflection helpers ────────────────────────────────────────────

        private static VisualElement GetStatusBarRoot()
        {
            var asm = typeof(UnityEditor.Editor).Assembly;

            var appStatusBarType = asm.GetType("UnityEditor.AppStatusBar");
            if (appStatusBarType == null)
            {
                Debug.LogWarning("[MCP] UnityEditor.AppStatusBar type not found");
                return null;
            }

            var guiViewType  = asm.GetType("UnityEditor.GUIView");
            var backendType  = asm.GetType("UnityEditor.IWindowBackend");
            if (guiViewType == null || backendType == null)
            {
                Debug.LogWarning("[MCP] UnityEditor.GUIView or IWindowBackend type not found");
                return null;
            }

            var windowBackendProp = guiViewType.GetProperty(
                "windowBackend",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var visualTreeProp = backendType.GetProperty(
                "visualTree",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (windowBackendProp == null || visualTreeProp == null)
            {
                Debug.LogWarning("[MCP] windowBackend or visualTree property not found on GUIView/IWindowBackend");
                return null;
            }

            var instances = Resources.FindObjectsOfTypeAll(appStatusBarType);
            if (instances == null || instances.Length == 0)
            {
                // AppStatusBar not visible yet — will retry on next delayCall.
                return null;
            }

            var statusBar = instances[0];
            var backend   = windowBackendProp.GetValue(statusBar);
            if (backend == null) return null;

            var root = visualTreeProp.GetValue(backend) as VisualElement;
            return root;
        }
    }
}
