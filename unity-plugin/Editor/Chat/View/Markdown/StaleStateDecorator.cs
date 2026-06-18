// Attaches deferred stale-state styling to a chip pill via IChipExistenceService.Observe().
// The subscription is disposed automatically when the pill is detached from its panel.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class StaleStateDecorator
    {
        /// <summary>
        /// Subscribes the pill to existence updates for the referenced chip.
        /// If the cached value is already false, the pill is faded immediately.
        /// If the value is unknown, an Observe() subscription is created and disposed on detach.
        /// </summary>
        internal static void Attach(VisualElement pill, string kindKey, string path,
            IChipExistenceService existenceService)
        {
            if (pill == null || existenceService == null) return;

            var cached = existenceService.Exists(kindKey, path);
            if (cached == false)
            {
                MarkStale(pill, pill.tooltip);
            }
            else if (cached == null)
            {
                var capturedPill = pill;
                var capturedTooltip = pill.tooltip;
                var token = existenceService.Observe(kindKey, path, exists =>
                {
                    if (!exists) MarkStale(capturedPill, capturedTooltip);
                });

                pill.RegisterCallback<DetachFromPanelEvent>(_ =>
                {
                    try { token?.Dispose(); }
                    catch (Exception e) { Debug.LogException(e); }
                });
            }
        }

        static void MarkStale(VisualElement pill, string originalTooltip)
        {
            pill.style.opacity = 0.4f;
            pill.tooltip = "[NOT FOUND] " + originalTooltip;
        }
    }
}
