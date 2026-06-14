using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace UnityMCP.Editor
{
    internal static class PortResolver
    {
        internal static int ResolvePort(string envValue, string jsonContent, int defaultStart)
        {
            if (envValue != null && int.TryParse(envValue, out var p) && IsValidPort(p)) return p;
            var saved = ParsePortFromJson(jsonContent, "port");
            if (saved.HasValue && IsValidPort(saved.Value)) return saved.Value;
            return FindFreePort(defaultStart);
        }

        internal static int ResolveChatPort(string envValue, string jsonContent, int mainPort, int defaultStart)
        {
            if (envValue != null && int.TryParse(envValue, out var p) && IsValidPort(p)) return p;
            var saved = ParsePortFromJson(jsonContent, "chatPort");
            if (saved.HasValue && IsValidPort(saved.Value)) return saved.Value;
            return FindFreePort(defaultStart, skipPort: mainPort);
        }

        internal static int? ParsePortFromJson(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(\\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var val)) return val;
            return null;
        }

        internal static bool IsValidPort(int port) => port >= 1024 && port <= 65535;

        internal static int FindFreePort(int startFrom, int skipPort = -1)
        {
            for (var port = startFrom; port <= 9599; port++)
            {
                if (port == skipPort) continue;
                try
                {
                    var listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch (SocketException) { }
            }
            var fb = new TcpListener(IPAddress.Loopback, 0);
            fb.Start();
            var assigned = ((IPEndPoint)fb.LocalEndpoint).Port;
            fb.Stop();
            return assigned;
        }

        internal static void SavePorts(string filePath, int port, int chatPort)
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                // Merge-write: preserve reloadPort written by reload-package (if present).
                string existing = null;
                try { if (System.IO.File.Exists(filePath)) existing = System.IO.File.ReadAllText(filePath); }
                catch { }
                var reloadPort = ParsePortFromJson(existing, "reloadPort");
                var json = reloadPort.HasValue
                    ? $"{{\"port\":{port},\"chatPort\":{chatPort},\"reloadPort\":{reloadPort.Value}}}"
                    : $"{{\"port\":{port},\"chatPort\":{chatPort}}}";
                var tmp = filePath + ".tmp";
                System.IO.File.WriteAllText(tmp, json);
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                System.IO.File.Move(tmp, filePath);
            }
            catch { }
        }

        // Reads reloadPort from MCP_Port.json. Returns 0 if absent or file missing.
        public static int ReadReloadPort(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return 0;
                var json = System.IO.File.ReadAllText(filePath);
                var val = ParsePortFromJson(json, "reloadPort");
                return val ?? 0;
            }
            catch { return 0; }
        }
    }
}
