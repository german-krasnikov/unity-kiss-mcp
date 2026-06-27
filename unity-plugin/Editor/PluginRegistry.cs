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
            _plugins.Add(plugin);
            UnityEngine.Debug.Log($"[MCP] Plugin registered: {plugin.Name}");
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

        public static bool IsPluginCommand(string cmd) =>
            _plugins.Any(p => BelongsToPlugin(p, cmd));

        /// <summary>Returns all registered commands that belong to this plugin.</summary>
        public static string[] GetCommandsForPlugin(IMCPPlugin plugin) =>
            CommandRegistry.GetAllCommands().Where(c => BelongsToPlugin(plugin, c)).ToArray();

        public static string[] GetAllPluginToolNames() =>
            _plugins.SelectMany(p => GetCommandsForPlugin(p)).ToArray();

        private static bool BelongsToPlugin(IMCPPlugin plugin, string cmd) =>
            (!string.IsNullOrEmpty(plugin.CommandPrefix)
                && (cmd == plugin.CommandPrefix || cmd.StartsWith(plugin.CommandPrefix + "_")))
            || plugin.AdditionalCommands.Contains(cmd);

        public static IReadOnlyList<IMCPPlugin> GetAll() => _plugins;

        public static IReadOnlyList<IMCPPlugin> All => _plugins;

        internal static void Clear() => _plugins.Clear();
    }
}
