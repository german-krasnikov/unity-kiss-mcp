// Thread-safe TCS store for ask_user — one entry per active ask_user TCP call.
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEditor;

namespace UnityMCP.Editor
{
    public static class PendingAskRegistry
    {
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>>
            _pending = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        [InitializeOnLoadMethod]
        private static void OnDomainReload()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelAll;
        }

        /// <summary>Register a new pending ask; returns requestId for convenience.</summary>
        public static string Register(string requestId)
        {
            var tcs = new TaskCompletionSource<string>();
            _pending[requestId] = tcs;  // overwrite on duplicate (domain-reload safety)
            return requestId;
        }

        /// <summary>Called from UI Submit callback (main thread) to complete the TCS.</summary>
        public static void Complete(string requestId, string answersJson)
        {
            if (_pending.TryGetValue(requestId, out var tcs))
                tcs.TrySetResult(answersJson);
        }

        /// <summary>Called on client disconnect or domain reload cleanup.</summary>
        public static void Cancel(string requestId)
        {
            if (_pending.TryRemove(requestId, out var tcs))
                tcs.TrySetCanceled();
        }

        /// <summary>Cancel all pending asks (called before assembly reload).</summary>
        public static void CancelAll()
        {
            foreach (var key in _pending.Keys)
                Cancel(key);
        }

        /// <summary>CommandRouter reads this to ContinueWith. Returns null if not found.</summary>
        public static TaskCompletionSource<string> GetTcs(string requestId)
        {
            _pending.TryGetValue(requestId, out var tcs);
            return tcs;
        }
    }
}
