using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    internal static class SimulatorRegistry
    {
        static Dictionary<string, Type> _types;

        static void EnsureLoaded()
        {
            if (_types != null) return;
            _types = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            var iface = typeof(IPlaytestSimulator);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.IsAbstract || t.IsInterface || !iface.IsAssignableFrom(t)) continue;
                    try
                    {
                        var inst = (IPlaytestSimulator)Activator.CreateInstance(t);
                        _types[inst.Name] = t;
                    }
                    catch { /* skip bad types */ }
                }
            }
        }

        public static IPlaytestSimulator Create(string name, SimulatorArgs args)
        {
            EnsureLoaded();
            if (!_types.TryGetValue(name, out var type))
                throw new ArgumentException($"Simulator not found: '{name}'. Available: {string.Join(", ", _types.Keys)}");
            var sim = (IPlaytestSimulator)Activator.CreateInstance(type);
            sim.Start(args);
            return sim;
        }

        // For testing: reset cache so new types are picked up
        internal static void Reset() => _types = null;
    }
}
