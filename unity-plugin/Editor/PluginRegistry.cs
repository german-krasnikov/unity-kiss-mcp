using System.Collections.Generic;
using System.Linq;

namespace UnityMCP.Editor
{
    public static class PluginRegistry
    {
        private static readonly List<IMCPPlugin> _plugins = new List<IMCPPlugin>();

        public static void Register(IMCPPlugin plugin)
        {
            if (_plugins.Any(p => p.Name == plugin.Name)) return;
            try
            {
                plugin.RegisterCommands();
                _plugins.Add(plugin);
                UnityEngine.Debug.Log($"[MCP] Plugin registered: {plugin.Name}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[MCP] Plugin '{plugin.Name}' failed to register: {e.Message}");
            }
        }

        public static void RegisterAllPlugins()
        {
            foreach (var plugin in _plugins)
            {
                try { plugin.RegisterCommands(); }
                catch (System.Exception e) { UnityEngine.Debug.LogError($"[MCP] Plugin '{plugin.Name}' RegisterCommands failed: {e.Message}"); }
            }
        }

        public static void OnDomainReload()
        {
            foreach (var plugin in _plugins)
            {
                try { plugin.OnDomainReload(); }
                catch (System.Exception e) { UnityEngine.Debug.LogError($"[MCP] Plugin '{plugin.Name}' OnDomainReload failed: {e.Message}"); }
            }
        }

        public static bool IsPluginCommand(string cmd)
        {
            return _plugins.Any(p =>
                (!string.IsNullOrEmpty(p.CommandPrefix)
                    && (cmd == p.CommandPrefix || cmd.StartsWith(p.CommandPrefix + "_")))
                || p.AdditionalCommands.Contains(cmd));
        }

        public static string[] GetAllPluginToolNames()
        {
            return _plugins
                .SelectMany(p => CommandRegistry.GetAllCommands()
                    .Where(c => (!string.IsNullOrEmpty(p.CommandPrefix)
                            && (c == p.CommandPrefix || c.StartsWith(p.CommandPrefix + "_")))
                        || p.AdditionalCommands.Contains(c)))
                .ToArray();
        }

        public static IReadOnlyList<IMCPPlugin> GetAll() => _plugins;

        internal static void Clear() => _plugins.Clear();
    }
}
