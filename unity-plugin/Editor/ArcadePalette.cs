using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ArcadePalette
    {
        public static Color Up     => new Color(0.227f, 0.824f, 0.624f); // #3ad29f
        public static Color Listen => new Color(0.910f, 0.635f, 0.227f); // #e8a23a
        public static Color Down   => new Color(0.431f, 0.169f, 0.227f); // #6e2b3a
        public static Color Accent => new Color(0.914f, 0.271f, 0.376f); // #e94560

        public static Color ForState(string stateKey) => stateKey switch
        {
            "up"     => Up,
            "listen" => Listen,
            "down"   => Down,
            _        => Down
        };

        public static string StateClass =>
            MCPServer.IsRunning && MCPServer.IsClientConnected ? "conn-up"
            : MCPServer.IsRunning ? "conn-listen" : "conn-down";
    }
}
