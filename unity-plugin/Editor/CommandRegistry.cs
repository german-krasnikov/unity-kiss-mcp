using System;
using System.Collections.Generic;

namespace UnityMCP.Editor
{
    public static class CommandRegistry
    {
        private struct Entry
        {
            public Func<string, string> Handler;
            public bool Mutating;
            public bool Runtime;
        }

        // All mutations happen on Unity main thread (dispatched by MCPServer).
        private static readonly Dictionary<string, Entry> _commands = new Dictionary<string, Entry>();

        static CommandRegistry() { InitDefaults(); }

        internal static void InitDefaults()
        {
            CommandRouter.RegisterAll();
        }

        public static void Register(string cmd, Func<string, string> handler, bool mutating = false, bool runtime = false)
        {
            _commands[cmd] = new Entry { Handler = handler, Mutating = mutating, Runtime = runtime };
        }

        public static void RegisterAction(string cmd, Func<string, string, string> handler, bool mutating = false, bool runtime = false)
        {
            _commands[cmd] = new Entry
            {
                Handler = args =>
                {
                    var action = JsonHelper.ExtractString(args, "action");
                    if (action == null)
                        throw new ArgumentException($"'action' is required for command '{cmd}'");
                    return handler(action, args);
                },
                Mutating = mutating,
                Runtime = runtime
            };
        }

        public static bool IsRegistered(string cmd) => _commands.ContainsKey(cmd);
        internal static bool IsMutating(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.Mutating;
        internal static bool IsRuntime(string cmd) => _commands.TryGetValue(cmd, out var entry) && entry.Runtime;

        internal static string Execute(string cmd, string args)
        {
            if (!_commands.TryGetValue(cmd, out var entry))
                throw new InvalidOperationException($"Command not registered: {cmd}");
            return entry.Handler(args);
        }

        internal static IEnumerable<string> GetAllCommands() => _commands.Keys;
        internal static void Clear() => _commands.Clear();
    }
}
