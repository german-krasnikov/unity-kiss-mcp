using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Profiling
{
    /// <summary>
    /// Label that lerps its displayed value to the target at ~60fps.
    /// Scheduler is initialized lazily on AttachToPanel to allow test instantiation without a panel.
    /// </summary>
    internal sealed class AnimatedCounter : Label
    {
        internal float _current;
        internal float _target;
        private readonly string _format;
        private IVisualElementScheduledItem _anim;

        internal AnimatedCounter(string format = "F1")
        {
            _format = format;
            // Lazy init: schedule requires a panel context.
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _anim = schedule.Execute(Tick).Every(16);
                _anim.Pause();
                // Resume if a target was set before attach.
                if (Mathf.Abs(_current - _target) > 0.01f) _anim.Resume();
            });
        }

        internal void SetTarget(float value)
        {
            _target = value;
            if (Mathf.Abs(_current - _target) > 0.01f)
                _anim?.Resume();
        }

        // internal so tests can drive it directly without a scheduler.
        internal void Tick()
        {
            _current = Mathf.Lerp(_current, _target, 0.15f);
            if (Mathf.Abs(_current - _target) < 0.01f)
            {
                _current = _target;
                _anim?.Pause();
            }
            var s = _current.ToString(_format);
            if (text != s) text = s;
        }
    }
}
