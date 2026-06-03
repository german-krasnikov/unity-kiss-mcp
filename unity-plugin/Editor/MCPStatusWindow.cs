using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    public class MCPStatusWindow : EditorWindow
    {
        private VisualElement _orb, _halo;
        private Label _word, _sub;

        [MenuItem("MCP/Status", priority = 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<MCPStatusWindow>("MCP Status");
            window.minSize = new Vector2(240, 220);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            var ss = MCPEditorUtils.LoadStyleSheet("MCPStatus.uss");
            if (ss != null) root.styleSheets.Add(ss);
            root.AddToClassList("mcp-root");

            var brand = new Label("UNITY MCP");
            brand.AddToClassList("brand");

            var stage = new VisualElement();
            stage.AddToClassList("orb-stage");
            _halo = new VisualElement(); _halo.AddToClassList("orb-halo");
            _orb  = new VisualElement(); _orb.AddToClassList("orb");
            stage.Add(_halo);
            stage.Add(_orb);

            _word = new Label(); _word.AddToClassList("status-word");
            _sub  = new Label(); _sub.AddToClassList("status-sub");

            var spacerTop = new VisualElement(); spacerTop.style.flexGrow = 1;
            var spacerBot = new VisualElement(); spacerBot.style.flexGrow = 1;

            var row = new VisualElement(); row.AddToClassList("btn-row");
            row.Add(MakeBtn("Restart",  MCPActions.Restart));
            row.Add(MakeBtn("Kill MCP", MCPActions.Kill));
            row.Add(MakeBtn("Reimport", MCPActions.Reimport));

            root.Add(brand);
            root.Add(spacerTop);
            root.Add(stage);
            root.Add(_word);
            root.Add(_sub);
            root.Add(spacerBot);
            root.Add(row);

            RefreshState();
            root.schedule.Execute(RefreshState).Every(700);
            root.schedule.Execute(BeatFast).Every(900);
            root.schedule.Execute(BeatSoft).Every(1500);
        }

        private Button MakeBtn(string t, System.Action a)
        {
            var b = new Button(a) { text = t };
            b.AddToClassList("mcp-btn");
            return b;
        }

        private void BeatFast()
        {
            if (MCPServer.IsRunning && MCPServer.IsClientConnected)
            {
                _halo.ToggleInClassList("beat");
                _orb.ToggleInClassList("beat");
            }
            else
            {
                _halo.RemoveFromClassList("beat");
                _orb.RemoveFromClassList("beat");
            }
        }

        private void BeatSoft()
        {
            if (MCPServer.IsRunning && !MCPServer.IsClientConnected)
                _halo.ToggleInClassList("beat-soft");
            else
                _halo.RemoveFromClassList("beat-soft");
        }

        private void RefreshState()
        {
            bool run = MCPServer.IsRunning, cli = MCPServer.IsClientConnected;
            var s = MCPStatusModel.GetCssKey(MCPStatusModel.GetState(run, cli));

            foreach (var k in new[] { "up", "listen", "down" })
            {
                _orb.RemoveFromClassList("orb--" + k);
                _halo.RemoveFromClassList("halo--" + k);
                _word.RemoveFromClassList("status-word--" + k);
            }

            _orb.AddToClassList("orb--" + s);
            _halo.AddToClassList("halo--" + s);
            _word.AddToClassList("status-word--" + s);

            _word.text = MCPStatusModel.GetLabel(run, cli, MCPServer.ServerPort);
            _sub.text  = MCPStatusModel.GetSub(run, cli);
        }
    }
}
