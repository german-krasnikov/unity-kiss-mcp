namespace UnityMCP.Editor
{
    /// <summary>
    /// Pure state model — zero UnityEngine/UnityEditor deps.
    /// Maps (isRunning, isClientConnected) -> display values.
    /// </summary>
    public static class MCPStatusModel
    {
        public enum State { Down, Listen, Up }

        public static State GetState(bool isRunning, bool isClientConnected)
        {
            if (!isRunning)          return State.Down;
            if (!isClientConnected)  return State.Listen;
            return State.Up;
        }

        public static string GetCssKey(State state) => state switch
        {
            State.Up     => "up",
            State.Listen => "listen",
            _            => "down",
        };

        public static string GetLabel(bool isRunning, bool isClientConnected, int port)
        {
            if (!isRunning)         return "OFFLINE";
            if (!isClientConnected) return "LISTENING";
            return $"ONLINE :{port}";
        }

        public static string GetSub(bool isRunning, bool isClientConnected)
        {
            if (!isRunning)         return "server stopped";
            if (!isClientConnected) return "no client";
            return "client connected";
        }

        /// <summary>Short pill text for status bar widget.</summary>
        public static string GetPill(State state, int port) => state switch
        {
            State.Up     => $"MCP :{port}",
            State.Listen => "MCP ...",
            _            => "MCP off",
        };
    }
}
