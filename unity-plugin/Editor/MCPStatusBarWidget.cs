using System;
using System.Collections.Generic;
using System.Linq;
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
        private static double _lastHealCheck;
        private static bool _loggedLayout;

        static MCPStatusBarWidget()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.delayCall += TryInject; // AppStatusBar may not exist yet
            EditorApplication.update    += SelfHeal;
        }

        private static void Cleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Cleanup;
            EditorApplication.update -= SelfHeal;
            try { _pulseItem?.Pause(); } catch { /* ignore — item may be disposed */ }
            try { _pillContainer?.RemoveFromHierarchy(); } catch { /* ignore — tree may be torn down */ }
            _pill          = null;
            _pillContainer = null;
            _pulseItem     = null;
            _injected      = false;
        }

        // Self-heal: tree rebuilds on dock/maximize/play-mode detach the pill.
        // The container's schedule stops when detached, so we use a global ticker (~1/sec).
        private static void SelfHeal()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastHealCheck < 1.0) return;
            _lastHealCheck = now;
            if (_pillContainer == null || _pillContainer.panel == null)
                TryInject();
        }

        private static void TryInject()
        {
            // Allow re-inject if container has been detached from the panel
            if (_injected && _pillContainer?.panel != null) return;
            _injected = false;
            try { _pulseItem?.Pause(); } catch { /* detached — already dead */ }
            _pulseItem = null;
            try
            {
                var root = GetStatusBarRoot();
                if (root == null) { EditorApplication.delayCall += TryInject; return; }

                root.Q("mcp-status-pill")?.RemoveFromHierarchy();
                _pillContainer = BuildPill();
                root.Insert(0, _pillContainer); // leftmost — pushes Unity widgets right, no overlap
                _injected = true;
                if (!_loggedLayout) { _loggedLayout = true; Debug.Log($"[MCP] StatusBar children ({root.childCount}): {string.Join(", ", root.Children().Select(c => c.name ?? c.GetType().Name))}"); }
                PulseTick(); // initial label + colour; the schedule drives the rest
                _pulseItem = _pillContainer.schedule.Execute(PulseTick).Every(900);
            }
            catch (Exception e) { Debug.LogWarning($"[MCP] StatusBar injection failed: {e.Message}"); }
        }

        // Pulse semantics: Up=steady 1.0 / Listen=calm breathe 0.85↔0.6 / Down=steady dim 0.5
        private static void PulseTick()
        {
            if (_pill == null) return;
            var state = MCPStatusModel.GetState(MCPServer.IsRunning, MCPServer.IsClientConnected);
            RefreshLabel(state);
            if (state == MCPStatusModel.State.Up)
            {
                _pill.style.opacity = 1.0f;
                return;
            }
            if (state == MCPStatusModel.State.Down)
            {
                _pill.style.opacity = 0.5f;
                return;
            }
            // Listen: gentle breathe between 0.85 and 0.60
            _pulseHigh = !_pulseHigh;
            _pill.style.opacity = _pulseHigh ? 0.85f : 0.60f;
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
            container.name                  = "mcp-status-pill";
            container.style.flexDirection   = FlexDirection.Row;
            container.style.alignItems      = Align.Center;
            container.style.alignSelf       = Align.Center;
            container.style.flexShrink      = 0;
            container.style.marginLeft      = 4;
            container.style.marginRight     = 6;

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

            // Eased opacity transition for the Listen breathe
            _pill.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
            _pill.style.transitionDuration  = new List<TimeValue> { new TimeValue(0.6f, TimeUnit.Second) };

            _pill.RegisterCallback<ClickEvent>(OnPillClick);
            ApplyPillColor(_pill, MCPStatusModel.State.Down);

            container.Add(_pill);
            return container;
        }

        private static void ApplyPillColor(Label label, MCPStatusModel.State state)
        {
            bool pro = EditorGUIUtility.isProSkin;
            switch (state)
            {
                case MCPStatusModel.State.Up:
                    label.style.color           = pro ? new Color(0.27f, 0.85f, 0.62f) : new Color(0.10f, 0.50f, 0.34f);
                    label.style.backgroundColor = pro ? new Color(0.27f, 0.85f, 0.62f, 0.16f) : new Color(0.10f, 0.50f, 0.34f, 0.14f);
                    break;
                case MCPStatusModel.State.Listen:
                    label.style.color           = pro ? new Color(0.93f, 0.66f, 0.24f) : new Color(0.60f, 0.42f, 0.05f);
                    label.style.backgroundColor = pro ? new Color(0.93f, 0.66f, 0.24f, 0.16f) : new Color(0.60f, 0.42f, 0.05f, 0.14f);
                    break;
                default:
                    label.style.color           = pro ? new Color(0.93f, 0.30f, 0.40f) : new Color(0.70f, 0.12f, 0.20f);
                    label.style.backgroundColor = pro ? new Color(0.93f, 0.30f, 0.40f, 0.16f) : new Color(0.70f, 0.12f, 0.20f, 0.14f);
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
            m.DropDown(_pill.worldBound);
        }

        // ── Reflection helpers ────────────────────────────────────────────

        private static VisualElement GetStatusBarRoot()
        {
            var asm            = typeof(UnityEditor.Editor).Assembly;
            var barType        = asm.GetType("UnityEditor.AppStatusBar");
            var guiViewType    = asm.GetType("UnityEditor.GUIView");
            var backendType    = asm.GetType("UnityEditor.IWindowBackend");
            if (barType == null || guiViewType == null || backendType == null) return null;

            var backendProp = guiViewType.GetProperty("windowBackend",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var treeProp = backendType.GetProperty("visualTree",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (backendProp == null || treeProp == null) return null;

            var instances = Resources.FindObjectsOfTypeAll(barType);
            if (instances == null || instances.Length == 0) return null;

            var backend = backendProp.GetValue(instances[0]);
            return backend == null ? null : treeProp.GetValue(backend) as VisualElement;
        }
    }
}
