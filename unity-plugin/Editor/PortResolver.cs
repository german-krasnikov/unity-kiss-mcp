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
                System.IO.File.WriteAllText(filePath, $"{{\"port\":{port},\"chatPort\":{chatPort}}}");
            }
            catch { }
        }
    }
}
