// ReloadBinder — bind-retry helper for ReloadMiniServer.
// Pure: no Unity API, fully unit-testable.
using System.Net;
using System.Net.Sockets;

namespace UnityMCP.Reload
{
    public static class ReloadBinder
    {
        // Retry over [startPort..maxPort] with SO_REUSEADDR.
        // Returns (listener, actualPort). Throws SocketException if whole range is occupied.
        public static (TcpListener listener, int port) BindListener(int startPort, int maxPort)
        {
            for (var p = startPort; p <= maxPort; p++)
            {
                var l = new TcpListener(IPAddress.Loopback, p);
                l.Server.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                try
                {
                    l.Start();
                    return (l, p);
                }
                catch (SocketException)
                {
                    try { l.Stop(); } catch { }
                }
            }
            throw new SocketException((int)SocketError.AddressAlreadyInUse);
        }
    }
}
