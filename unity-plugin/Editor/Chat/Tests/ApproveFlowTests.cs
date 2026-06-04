// TDD — RED first. Tests for Approve & Execute flow (Feature #11).
// Pure logic tests (1-6): ClaudeArgBuilder + string asserts, no EditorWindow.
// UI tests (7-10): bare VisualElement tree, no EditorWindow.
using NUnit.Framework;
using System.Linq;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ApproveFlowTests
    {
        // ── Pure logic: ClaudeArgBuilder ─────────────────────────────────────

        [Test]
        public void Test_ApproveArgs_ContainsResumeAndAcceptEdits()
        {
            var (args, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/tmp/mcp.json", "acceptEdits", "sess-X");

            Assert.Contains("--resume",      (System.Array)args);
            Assert.Contains("sess-X",        (System.Array)args);
            Assert.Contains("acceptEdits",   (System.Array)args);
        }

        [Test]
        public void Test_ApproveArgs_PlanToAcceptEdits_PermissionModeChanges()
        {
            var (argsPlan, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/tmp/mcp.json", "plan",         "sess-X");
            var (argsAgent, _) = ClaudeArgBuilder.Build(
                "/bin/claude", "/tmp/mcp.json", "acceptEdits",  "sess-X");

            var idxPlan  = System.Array.IndexOf(argsPlan,  "--permission-mode");
            var idxAgent = System.Array.IndexOf(argsAgent, "--permission-mode");
            Assert.AreEqual("plan",         argsPlan [idxPlan  + 1]);
            Assert.AreEqual("acceptEdits",  argsAgent[idxAgent + 1]);
        }

        [Test]
        public void Test_ApproveGuard_NullSessionId_NoOp()
        {
            // ApproveHelper must not throw when sessionId is null.
            Assert.DoesNotThrow(() => ApproveHelper.BuildPromptOrNull(null));
            Assert.IsNull(ApproveHelper.BuildPromptOrNull(null));
        }

        [Test]
        public void Test_ApproveGuard_EmptySessionId_NoOp()
        {
            Assert.IsNull(ApproveHelper.BuildPromptOrNull(""));
        }

        [Test]
        public void Test_ApprovePrompt_NonEmptySessionId_ReturnsPrompt()
        {
            // A non-empty sessionId must yield a non-null prompt.
            var result = ApproveHelper.BuildPromptOrNull("sess-Y");
            Assert.IsNotNull(result);
        }

        [Test]
        public void Test_ApprovePrompt_ContainsExecuteInstruction()
        {
            var prompt = ApproveHelper.BuildPromptOrNull("sess-Z");
            StringAssert.Contains("Execute", prompt);
        }

        // ── UI: bare VisualElement tree ───────────────────────────────────────

        [Test]
        public void Test_ApproveButton_RemovedAfterClick()
        {
            var container = new VisualElement();
            var btn = ApproveButtonFactory.MakeButton(container, () => { });
            container.Add(btn);

            Assert.AreEqual(1, container.childCount);
            // MakeButton stores the click Action in userData for testability.
            var click = (System.Action)btn.userData;
            click.Invoke();
            Assert.AreEqual(0, container.childCount,
                "Button must remove itself from hierarchy after click");
        }

        [Test]
        public void Test_ApproveButton_NotShown_InAgentMode()
        {
            var container = new VisualElement();
            // agentMode = true → should not add any button
            ApproveButtonFactory.MaybeAppend(container, agentMode: true, sessionId: "sess-A", onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }

        [Test]
        public void Test_ApproveButton_NotShown_WhenNoSessionId()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: null, onApprove: () => { });
            Assert.AreEqual(0, container.childCount);
        }

        [Test]
        public void Test_ApproveButton_ShownAfterAskModeTurnDone()
        {
            var container = new VisualElement();
            ApproveButtonFactory.MaybeAppend(container, agentMode: false, sessionId: "sess-B", onApprove: () => { });
            Assert.AreEqual(1, container.childCount);
            var btn = container[0];
            Assert.IsTrue(btn.ClassListContains("approve-btn"));
        }
    }
}
