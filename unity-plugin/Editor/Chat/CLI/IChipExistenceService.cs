// Existence service contract for chip references.
// Observe() returns a disposable subscription; Exists() returns cached value or null (unknown).
namespace UnityMCP.Editor.Chat
{
    public interface IChipExistenceService : System.IDisposable
    {
        /// <summary>Returns cached existence, or null if not yet resolved (queues a background check).</summary>
        bool? Exists(string kindKey, string path);

        /// <summary>
        /// Subscribe to existence changes for a specific chip. If a cached value already exists,
        /// the callback is invoked synchronously. Returns a token that unsubscribes on dispose.
        /// </summary>
        System.IDisposable Observe(string kindKey, string path, System.Action<bool> onChanged);

        /// <summary>Invalidate a single cached entry.</summary>
        void Invalidate(string kindKey, string path);

        /// <summary>Clear all cached entries and pending checks.</summary>
        void Clear();
    }
}
