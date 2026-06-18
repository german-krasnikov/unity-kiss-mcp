// Test-only fake for IChipExistenceService.
using System;
using System.Collections.Generic;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class FakeChipExistenceService : IChipExistenceService
    {
        /// <summary>Return value for Exists() and synchronous Observe() callbacks.</summary>
        public Func<string, string, bool?> ExistsImpl = (_, _) => null;

        /// <summary>When true, Observe() invokes the callback synchronously if ExistsImpl returns a value.</summary>
        public bool InvokeSyncIfKnown = true;

        readonly Dictionary<(string kind, string path), List<Action<bool>>> _subscribers = new();
        int _disposedCount;

        public int DisposedCount => _disposedCount;

        public bool? Exists(string kindKey, string path)
            => ExistsImpl?.Invoke(kindKey, path);

        public IDisposable Observe(string kindKey, string path, Action<bool> onChanged)
        {
            var key = (kindKey, path);
            if (!_subscribers.TryGetValue(key, out var list))
            {
                list = new List<Action<bool>>();
                _subscribers[key] = list;
            }
            list.Add(onChanged);

            if (InvokeSyncIfKnown)
            {
                var value = ExistsImpl?.Invoke(kindKey, path);
                if (value.HasValue)
                    onChanged?.Invoke(value.Value);
            }

            return new Token(this, key, onChanged);
        }

        public void Invalidate(string kindKey, string path) { }

        public void Clear()
        {
            _subscribers.Clear();
        }

        public void Dispose()
        {
            Clear();
        }

        /// <summary>Simulate a background resolution by invoking all subscribers for the key.</summary>
        public void Resolve(string kindKey, string path, bool value)
        {
            if (!_subscribers.TryGetValue((kindKey, path), out var list)) return;
            foreach (var cb in list)
                cb?.Invoke(value);
        }

        void OnDisposed((string kind, string path) key, Action<bool> callback)
        {
            _disposedCount++;
            if (_subscribers.TryGetValue(key, out var list))
            {
                list.Remove(callback);
                if (list.Count == 0)
                    _subscribers.Remove(key);
            }
        }

        sealed class Token : IDisposable
        {
            readonly FakeChipExistenceService _service;
            readonly (string kind, string path) _key;
            readonly Action<bool> _callback;
            bool _disposed;

            internal Token(FakeChipExistenceService service, (string, string) key, Action<bool> callback)
            {
                _service = service;
                _key = key;
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _service?.OnDisposed(_key, _callback);
            }
        }
    }
}
