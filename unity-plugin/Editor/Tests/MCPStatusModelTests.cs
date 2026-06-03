using NUnit.Framework;
using UnityMCP.Editor;
using static UnityMCP.Editor.MCPStatusModel;

namespace UnityMCP.Editor.Tests
{
    public class MCPStatusModelTests
    {
        // ── GetState ────────────────────────────────────────────────────────
        [Test] public void GetState_NotRunning_ReturnsDown()
            => Assert.AreEqual(State.Down, GetState(false, false));

        [Test] public void GetState_NotRunning_ClientConnected_StillDown()
            => Assert.AreEqual(State.Down, GetState(false, true));

        [Test] public void GetState_Running_NoClient_ReturnsListen()
            => Assert.AreEqual(State.Listen, GetState(true, false));

        [Test] public void GetState_Running_WithClient_ReturnsUp()
            => Assert.AreEqual(State.Up, GetState(true, true));

        // ── GetCssKey ───────────────────────────────────────────────────────
        [Test] public void GetCssKey_Down_ReturnsDown()
            => Assert.AreEqual("down", GetCssKey(State.Down));

        [Test] public void GetCssKey_Listen_ReturnsListen()
            => Assert.AreEqual("listen", GetCssKey(State.Listen));

        [Test] public void GetCssKey_Up_ReturnsUp()
            => Assert.AreEqual("up", GetCssKey(State.Up));

        // ── GetLabel ────────────────────────────────────────────────────────
        [Test] public void GetLabel_NotRunning_ReturnsOffline()
            => Assert.AreEqual("OFFLINE", GetLabel(false, false, 9500));

        [Test] public void GetLabel_Running_NoClient_ReturnsListening()
            => Assert.AreEqual("LISTENING", GetLabel(true, false, 9500));

        [Test] public void GetLabel_Running_WithClient_ReturnsOnlineWithPort()
            => Assert.AreEqual("ONLINE :9500", GetLabel(true, true, 9500));

        [Test] public void GetLabel_Running_WithClient_CustomPort()
            => Assert.AreEqual("ONLINE :9999", GetLabel(true, true, 9999));

        // ── GetSub ──────────────────────────────────────────────────────────
        [Test] public void GetSub_NotRunning_ReturnsServerStopped()
            => Assert.AreEqual("server stopped", GetSub(false, false));

        [Test] public void GetSub_Running_NoClient_ReturnsNoClient()
            => Assert.AreEqual("no client", GetSub(true, false));

        [Test] public void GetSub_Running_WithClient_ReturnsClientConnected()
            => Assert.AreEqual("client connected", GetSub(true, true));

        // ── GetPill ─────────────────────────────────────────────────────────
        [Test] public void GetPill_Down_ReturnsMcpOff()
            => Assert.AreEqual("MCP off", GetPill(State.Down, 9500));

        [Test] public void GetPill_Listen_ReturnsMcpDots()
            => Assert.AreEqual("MCP ...", GetPill(State.Listen, 9500));

        [Test] public void GetPill_Up_ReturnsMcpWithPort()
            => Assert.AreEqual("MCP :9500", GetPill(State.Up, 9500));
    }
}
