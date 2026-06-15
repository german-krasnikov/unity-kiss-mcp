using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ZoomPanManipulatorTests
    {
        private VisualElement _viewport;
        private VisualElement _content;
        private ZoomPanManipulator _zp;

        [SetUp]
        public void SetUp()
        {
            _viewport = new VisualElement();
            _content  = new VisualElement();
            _zp = new ZoomPanManipulator(_content);
            _viewport.AddManipulator(_zp);
            _viewport.Add(_content);
        }

        [Test]
        public void InitialZoom_IsOne()
        {
            Assert.AreEqual(1f, _zp.Zoom);
        }

        [Test]
        public void InitialPan_IsZero()
        {
            Assert.AreEqual(0f, _zp.PanX);
            Assert.AreEqual(0f, _zp.PanY);
        }

        [Test]
        public void Reset_SetsZoomToOne()
        {
            _zp.Reset();
            Assert.AreEqual(1f, _zp.Zoom);
        }

        [Test]
        public void Reset_SetsPanToZero()
        {
            _zp.Reset();
            Assert.AreEqual(0f, _zp.PanX);
            Assert.AreEqual(0f, _zp.PanY);
        }

        [Test]
        public void Apply_DoesNotThrow_OnNullContent()
        {
            var zp = new ZoomPanManipulator(null);
            Assert.DoesNotThrow(() => zp.Reset());
        }

        [Test]
        public void Manipulator_CanAttachToTarget()
        {
            Assert.IsNotNull(_zp);
            Assert.AreEqual(_viewport, _zp.target);
        }

        [Test]
        public void Reset_ZoomRemainsOne_AfterMultipleCalls()
        {
            _zp.Reset();
            _zp.Reset();
            Assert.AreEqual(1f, _zp.Zoom);
        }
    }
}
