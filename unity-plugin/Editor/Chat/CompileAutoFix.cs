// Automatically re-submits compile errors as a new chat turn (up to MaxRetries times).
// Test seam: SimulateCompilation(bool) drives the same logic as compilationFinished.
using System;
using UnityEditor.Compilation;

namespace UnityMCP.Editor.Chat
{
    internal sealed class CompileAutoFix
    {
        private const int MaxRetries = 3;

        public event Action<string> OnErrorsDetected;
        public bool IsArmed          { get; private set; }
        internal int RetriesLeft    => _retriesLeft;
        private int  _retriesLeft   = MaxRetries;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Arm()
        {
            IsArmed = true;
        }

        public void Disarm()
        {
            IsArmed      = false;
            _retriesLeft = MaxRetries;
        }

        // ── Editor event wiring ───────────────────────────────────────────────

        public void Subscribe()   => CompilationPipeline.compilationFinished += OnCompilationFinished;
        public void Unsubscribe() => CompilationPipeline.compilationFinished -= OnCompilationFinished;

        private void OnCompilationFinished(object obj)
        {
            var hasErrors = CompileErrorCapture.GetErrors() != "No compilation errors";
            SimulateCompilation(hasErrors);
        }

        // ── Test seam (also called by real compilationFinished indirectly) ────

        /// <summary>
        /// Drive the auto-fix state machine. Called directly in tests; via OnCompilationFinished in production.
        /// </summary>
        internal void SimulateCompilation(bool hasErrors)
        {
            if (!IsArmed) return;

            if (!hasErrors)
            {
                Disarm(); // success — reset everything
                return;
            }

            // Cap exhausted: absorb silently — cap chip is shown at TurnDone, not here.
            if (_retriesLeft <= 0)
            {
                IsArmed = false;
                return;
            }

            _retriesLeft--;
            IsArmed = false; // must re-arm explicitly for next retry
            OnErrorsDetected?.Invoke(CompileErrorCapture.GetErrors());
        }
    }
}
