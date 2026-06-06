using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal static class HubHeaderAnim
    {
        private static readonly float[] PacketX = { 3f, 15f, 27f, 39f, 50f, 61f, 73f, 85f, 97f };

        public static VisualElement Build(VisualElement scheduleHost)
        {
            var root = new VisualElement();
            root.AddToClassList("hub-anim-root");

            var nodeL1 = MakeNode("han-node--sm"); var lineL1 = MakeLine();
            var nodeL2 = MakeNode("han-node--md"); var lineL2 = MakeLine();
            var hub    = MakeHub(out var statusLabel);
            var lineR1 = MakeLine();               var nodeR1 = MakeNode("han-node--md");
            var lineR2 = MakeLine();               var nodeR2 = MakeNode("han-node--sm");

            root.Add(nodeL1); root.Add(lineL1);
            root.Add(nodeL2); root.Add(lineL2);
            root.Add(hub);
            root.Add(lineR1); root.Add(nodeR1);
            root.Add(lineR2); root.Add(nodeR2);

            var packet = new VisualElement();
            packet.AddToClassList("han-packet");
            root.Add(packet);

            var stateEls = new VisualElement[]
                { nodeL1, nodeL2, lineL1, lineL2, hub, lineR1, lineR2, nodeR1, nodeR2, packet, statusLabel };

            int tick = 0;
            int pIdx = 0;

            scheduleHost.schedule.Execute(() =>
            {
                tick++;

                bool run  = MCPServer.IsRunning;
                bool cli  = MCPServer.IsClientConnected;
                bool chat = ChatBackendProbe.IsChatBackendRunning();
                var  state = MCPStatusModel.GetState(run, cli, chat);
                string key = MCPStatusModel.GetCssKey(state);

                foreach (var el in stateEls)
                {
                    el.RemoveFromClassList("han--up");
                    el.RemoveFromClassList("han--listen");
                    el.RemoveFromClassList("han--down");
                    el.AddToClassList("han--" + key);
                }

                statusLabel.text = MCPStatusModel.GetLabel(state, MCPServer.ServerPort);

                if (tick % 7 == 0) hub.ToggleInClassList("han-hub--pulse");

                pIdx = (pIdx + 1) % PacketX.Length;
                packet.style.left = new StyleLength(new Length(PacketX[pIdx], LengthUnit.Percent));
                packet.EnableInClassList("han-packet--bright", pIdx % 2 == 0);

            }).Every(80);

            return root;
        }

        private static VisualElement MakeNode(string sizeClass)
        {
            var n = new VisualElement();
            n.AddToClassList("han-node");
            n.AddToClassList(sizeClass);
            return n;
        }

        private static VisualElement MakeLine()
        {
            var l = new VisualElement();
            l.AddToClassList("han-line");
            return l;
        }

        private static VisualElement MakeHub(out Label statusLabel)
        {
            var hub = new VisualElement();
            hub.AddToClassList("han-hub");
            statusLabel = new Label();
            statusLabel.AddToClassList("han-status");
            hub.Add(statusLabel);
            return hub;
        }
    }
}
