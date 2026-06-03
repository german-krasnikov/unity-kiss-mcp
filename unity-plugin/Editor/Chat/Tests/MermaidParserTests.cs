// Tests for MermaidParser. Pure, NUnit-testable.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MermaidParserTests
    {
        [Test]
        public void GraphTD_Direction()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A-->B" });
            Assert.IsNotNull(g);
            Assert.AreEqual(MermaidDir.TD, g.Dir);
        }

        [Test]
        public void FlowchartLR_Direction()
        {
            var g = MermaidParser.TryParse(new[] { "flowchart LR", "A-->B" });
            Assert.IsNotNull(g);
            Assert.AreEqual(MermaidDir.LR, g.Dir);
        }

        [Test]
        public void TB_TreatedAsTD()
        {
            var g = MermaidParser.TryParse(new[] { "graph TB", "A-->B" });
            Assert.IsNotNull(g);
            Assert.AreEqual(MermaidDir.TD, g.Dir);
        }

        [Test]
        public void NonFlowchart_SequenceDiagram_ReturnsNull()
        {
            var g = MermaidParser.TryParse(new[] { "sequenceDiagram", "A->>B: msg" });
            Assert.IsNull(g);
        }

        [Test]
        public void Empty_ReturnsNull()
        {
            Assert.IsNull(MermaidParser.TryParse(null));
            Assert.IsNull(MermaidParser.TryParse(new string[0]));
        }

        [Test]
        public void RectNode_Bracket()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A[My Label]" });
            Assert.IsNotNull(g);
            Assert.AreEqual(1, g.Nodes.Count);
            Assert.AreEqual("A", g.Nodes[0].Id);
            Assert.AreEqual("My Label", g.Nodes[0].Label);
            Assert.AreEqual(NodeShape.Rect, g.Nodes[0].Shape);
        }

        [Test]
        public void RoundNode_Paren()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "B(Round)" });
            Assert.AreEqual(NodeShape.Round, g.Nodes[0].Shape);
            Assert.AreEqual("Round", g.Nodes[0].Label);
        }

        [Test]
        public void DiamondNode_Brace()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "C{Decision}" });
            Assert.AreEqual(NodeShape.Diamond, g.Nodes[0].Shape);
            Assert.AreEqual("Decision", g.Nodes[0].Label);
        }

        [Test]
        public void Edge_Arrow_True()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A-->B" });
            Assert.AreEqual(1, g.Edges.Count);
            Assert.IsTrue(g.Edges[0].Arrow);
            Assert.AreEqual("A", g.Edges[0].From);
            Assert.AreEqual("B", g.Edges[0].To);
        }

        [Test]
        public void Edge_PlainTripleDash_False()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A---B" });
            Assert.AreEqual(1, g.Edges.Count);
            Assert.IsFalse(g.Edges[0].Arrow);
        }

        [Test]
        public void Edge_PipeLabel_Captured()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A-->|yes it is|B" });
            Assert.AreEqual("yes it is", g.Edges[0].Label);
        }

        [Test]
        public void ChainedEdge_SplitIntoTwo()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A-->B-->C" });
            Assert.AreEqual(2, g.Edges.Count);
            Assert.AreEqual("A", g.Edges[0].From);
            Assert.AreEqual("B", g.Edges[0].To);
            Assert.AreEqual("B", g.Edges[1].From);
            Assert.AreEqual("C", g.Edges[1].To);
        }

        [Test]
        public void SelfLoop_Handled()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A-->A" });
            Assert.IsNotNull(g);
            Assert.AreEqual(1, g.Edges.Count);
            Assert.AreEqual("A", g.Edges[0].From);
            Assert.AreEqual("A", g.Edges[0].To);
        }

        [Test]
        public void NodeDedup_FirstDefinitionWins()
        {
            var g = MermaidParser.TryParse(new[] { "graph TD", "A[First]", "A[Second]" });
            Assert.AreEqual(1, g.Nodes.Count);
            Assert.AreEqual("First", g.Nodes[0].Label);
        }
    }
}
