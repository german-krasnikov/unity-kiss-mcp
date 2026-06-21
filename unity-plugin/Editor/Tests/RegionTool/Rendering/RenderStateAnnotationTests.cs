using NUnit.Framework;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class RenderStateAnnotationTests
    {
        [Test]
        public void RenderState_NewFields_DefaultNull()
        {
            var state = new RenderState();
            Assert.IsNull(state.AnnotationType);
            Assert.IsNull(state.Label);
            Assert.AreEqual(0f, state.Length, 1e-5f);
        }

        [Test]
        public void RenderState_AnnotationType_CanBeSet()
        {
            var state = new RenderState { AnnotationType = "point" };
            Assert.AreEqual("point", state.AnnotationType);
        }

        [Test]
        public void RenderState_Label_CanBeSet()
        {
            var state = new RenderState { Label = "SpawnPoint" };
            Assert.AreEqual("SpawnPoint", state.Label);
        }

        [Test]
        public void RenderState_Length_CanBeSet()
        {
            var state = new RenderState { Length = 42.5f };
            Assert.AreEqual(42.5f, state.Length, 1e-5f);
        }

        [Test]
        public void DrawAnnotation_NullAnnotationType_DoesNotThrow()
        {
            var state = new RenderState { AnnotationType = null };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_PointType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "point",
                Vertices       = new[] { new UnityEngine.Vector2(1f, 2f) },
                Label          = "Test"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_PolylineType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "polyline",
                Vertices       = new[]
                {
                    new UnityEngine.Vector2(0f, 0f),
                    new UnityEngine.Vector2(5f, 0f),
                    new UnityEngine.Vector2(5f, 5f),
                },
                Length = 10f,
                Label  = "Path"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_MeasurementType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "measurement",
                Vertices       = new[]
                {
                    new UnityEngine.Vector2(0f, 0f),
                    new UnityEngine.Vector2(8f, 6f),
                },
                Length = 10f,
                Label  = "Gap"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_UnknownType_DoesNotThrow()
        {
            var state = new RenderState { AnnotationType = "future_type" };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }
    }
}
