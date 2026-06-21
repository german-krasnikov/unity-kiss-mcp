using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// All Scene View rendering for the Region Selection tool.
    /// Call Draw() from OnSceneGUI. GL calls are always before Handles.BeginGUI.
    /// </summary>
    internal static class RegionRenderer
    {
        static readonly Color AnnotationColor = new Color(0.133f, 0.827f, 0.933f); // cyan #22d3ee

        // ── Annotation entry point ─────────────────────────────────────────

        /// <summary>Draw annotation primitives (point/polyline/measurement) in SceneView.</summary>
        public static void DrawAnnotation(RenderState state)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            switch (state.AnnotationType)
            {
                case "point":       DrawPoint(state);       break;
                case "polyline":    DrawPolyline(state);    break;
                case "measurement": DrawMeasurement(state); break;
            }
        }

        static void DrawPoint(RenderState state)
        {
            var verts = state.Vertices;
            if (verts == null || verts.Count < 1) return;

            var xz = verts[0];
            var pos = new Vector3(xz.x, 0.01f, xz.y);

            Handles.color = AnnotationColor;
            float sz = HandleUtility.GetHandleSize(pos) * RenderStyle.VertexSize * 2f;
            Handles.DotHandleCap(0, pos, Quaternion.identity, sz, EventType.Repaint);

            if (!string.IsNullOrEmpty(state.Label))
                Handles.Label(pos + Vector3.right * sz * 2f, state.Label);

            // Dashed cross: ±1m
            DrawDashedCross(pos, 1f);
        }

        static void DrawPolyline(RenderState state)
        {
            var verts = state.Vertices;
            if (verts == null || verts.Count < 2) return;

            var buf = BuildHandlesBuffer(verts, closed: false);
            Handles.color = AnnotationColor;
            Handles.DrawAAPolyLine(2.5f, buf);

            // Vertex dots
            foreach (var v in buf)
            {
                float vsz = HandleUtility.GetHandleSize(v) * RenderStyle.VertexSize;
                Handles.DotHandleCap(0, v, Quaternion.identity, vsz, EventType.Repaint);
            }

            // Arrow on midpoint of each segment
            DrawSegmentArrows(verts);

            // HUD
            Handles.BeginGUI();
            string label = string.IsNullOrEmpty(state.Label) ? "" : state.Label + " | ";
            GUI.Box(new Rect(10, 60, 200, 24), GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(14, 64, 192, 18),
                $"{label}{verts.Count}pts | {state.Length:F1}m", EditorStyles.miniLabel);
            Handles.EndGUI();
        }

        static void DrawMeasurement(RenderState state)
        {
            var verts = state.Vertices;
            if (verts == null || verts.Count < 2) return;

            var a = new Vector3(verts[0].x, 0.01f, verts[0].y);
            var b = new Vector3(verts[1].x, 0.01f, verts[1].y);

            Handles.color = AnnotationColor;
            Handles.DrawAAPolyLine(2.5f, a, b);

            // Perpendicular tick marks at each end (±0.3m, like a ruler)
            DrawEndTick(a, b, 0.3f);
            DrawEndTick(b, a, 0.3f);

            // Floating distance label at midpoint
            var mid = (a + b) * 0.5f;
            string distLabel = state.Length > 0f
                ? $"{state.Length:F2}m"
                : $"{Vector3.Distance(a, b):F2}m";
            if (!string.IsNullOrEmpty(state.Label)) distLabel = state.Label + ": " + distLabel;
            Handles.Label(mid + Vector3.up * 0.2f, distLabel);
        }

        static void DrawDashedCross(Vector3 center, float halfLen)
        {
            Handles.DrawDottedLine(
                center + Vector3.right   * halfLen,
                center - Vector3.right   * halfLen, 4f);
            Handles.DrawDottedLine(
                center + Vector3.forward * halfLen,
                center - Vector3.forward * halfLen, 4f);
        }

        static void DrawSegmentArrows(IReadOnlyList<Vector2> verts)
        {
            for (int i = 0; i < verts.Count - 1; i++)
            {
                var a2 = verts[i];
                var b2 = verts[i + 1];
                var mid = (a2 + b2) * 0.5f;
                var dir2d = (b2 - a2).normalized;

                var midPos = new Vector3(mid.x, 0.01f, mid.y);
                float sz = HandleUtility.GetHandleSize(midPos) * 0.15f;

                var fwd = new Vector3(dir2d.x, 0f, dir2d.y);
                var right = Vector3.Cross(Vector3.up, fwd).normalized;

                var tip   = midPos + fwd * sz;
                var left  = midPos - fwd * sz * 0.5f + right * sz * 0.4f;
                var right3 = midPos - fwd * sz * 0.5f - right * sz * 0.4f;
                Handles.DrawAAPolyLine(1.5f, tip, left);
                Handles.DrawAAPolyLine(1.5f, tip, right3);
            }
        }

        static void DrawEndTick(Vector3 endPt, Vector3 otherPt, float halfLen)
        {
            var along = (otherPt - endPt).normalized;
            var perp  = Vector3.Cross(along, Vector3.up).normalized;
            Handles.DrawAAPolyLine(2f,
                endPt + perp * halfLen,
                endPt - perp * halfLen);
        }


        static Material _fillMaterial;

        static Material FillMaterial
        {
            get
            {
                if (_fillMaterial != null) return _fillMaterial;
                _fillMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                    { hideFlags = HideFlags.HideAndDontSave };
                _fillMaterial.SetInt("_ZWrite", 0);
                _fillMaterial.SetInt("_Cull", 0);
                _fillMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _fillMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                return _fillMaterial;
            }
        }

        // ── Main entry point ──────────────────────────────────────────────

        public static void Draw(RenderState state)
        {
            if (Event.current.type != EventType.Repaint) return;
            var verts = state.Vertices;
            if (verts == null || verts.Count < 2) return;

            var color = state.IsPreview ? RenderStyle.Preview : RenderStyle.Drawing;

            // 1. GL fill (MUST be before BeginGUI)
            DrawFill(verts, RenderStyle.FillColor(color));

            // 2. Glow (Preview only) — 3-pass AA lines
            if (state.IsPreview)
                DrawGlow(verts, color);

            // 3. Contour
            DrawContour(verts, color, RenderStyle.ContourWidth);

            // 4. Vertex handles (Preview only)
            if (state.IsPreview && verts.Count >= 3)
                DrawVertexHandles(ToArray(verts));

            // 5. Object highlights (Preview only)
            if (state.IsPreview && state.MatchedObjects != null)
                DrawObjectHighlights(state.MatchedObjects);

            // 6. Close indicator (PointByPoint drawing mode)
            if (state.IsDrawing && state.Mode == DrawingModeId.PointByPoint && verts.Count >= 3)
                DrawCloseIndicator(verts[0], state.CursorXZ);

            // 7. HUD (MUST be inside BeginGUI/EndGUI — after all GL/Handles)
            DrawHUD(state);
        }

        // ── Rendering layers ──────────────────────────────────────────────

        static void DrawFill(IReadOnlyList<Vector2> verts, Color fillColor)
        {
            if (verts.Count < 3) return;
            if (Event.current.type != EventType.Repaint) return;

            // Skip GL fill when scene view is not focused — prevents black flash
            // (Unity may not render 3D content before duringSceneGui in unfocused windows)
            var sv = SceneView.currentDrawingSceneView;
            if (sv != null && !sv.hasFocus) return;

            // Centroid
            float cx = 0f, cz = 0f;
            foreach (var v in verts) { cx += v.x; cz += v.y; }
            cx /= verts.Count;
            cz /= verts.Count;

            GL.PushMatrix();
            GL.MultMatrix(Handles.matrix);
            FillMaterial.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            GL.Color(fillColor);
            for (int i = 0; i < verts.Count; i++)
            {
                int j = (i + 1) % verts.Count;
                GL.Vertex3(cx, 0f, cz);
                GL.Vertex3(verts[i].x, 0f, verts[i].y);
                GL.Vertex3(verts[j].x, 0f, verts[j].y);
            }
            GL.End();
            GL.PopMatrix();
        }

        static void DrawContour(IReadOnlyList<Vector2> verts, Color color, float width)
        {
            var buf = BuildHandlesBuffer(verts, closed: true);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, buf);
        }

        static void DrawGlow(IReadOnlyList<Vector2> verts, Color baseColor)
        {
            var buf = BuildHandlesBuffer(verts, closed: true);
            Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
            Handles.DrawAAPolyLine(RenderStyle.GlowWidthOuter, buf);
            Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.30f);
            Handles.DrawAAPolyLine(6f, buf);
            Handles.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.90f);
            Handles.DrawAAPolyLine(3f, buf);
        }

        static void DrawVertexHandles(Vector2[] verts)
        {
            // Cap at 32 visible: skip alternates on large polygons
            int step = verts.Length > 32 ? 2 : 1;
            Handles.color = RenderStyle.VertexIdle;
            for (int i = 0; i < verts.Length; i += step)
            {
                var pos = new Vector3(verts[i].x, 0.01f, verts[i].y);
                float sz = HandleUtility.GetHandleSize(pos) * RenderStyle.VertexSize;
                Handles.DotHandleCap(0, pos, Quaternion.identity, sz, EventType.Repaint);
            }
        }

        static void DrawObjectHighlights(GameObject[] objects)
        {
            Handles.color = RenderStyle.ObjectHighlight;
            foreach (var go in objects)
            {
                var b = MultiViewCapture.ComputeBounds(go);
                Handles.DrawWireCube(b.center, b.size * 1.05f);
            }
        }

        static void DrawCloseIndicator(Vector2 firstVert, Vector2 cursor)
        {
            // Highlight first vertex in green to signal "click to close"
            var pos = new Vector3(firstVert.x, 0.01f, firstVert.y);
            float sz = HandleUtility.GetHandleSize(pos) * RenderStyle.VertexSize * 1.5f;
            Handles.color = RenderStyle.VertexClose;
            Handles.DotHandleCap(0, pos, Quaternion.identity, sz, EventType.Repaint);

            // Dotted line from cursor to first vertex
            var cursorPos = new Vector3(cursor.x, 0.01f, cursor.y);
            Handles.DrawDottedLine(cursorPos, pos, RenderStyle.DottedSpacing);
        }

        static void DrawHUD(RenderState state)
        {
            Handles.BeginGUI();
            string modeName = state.Mode.ToString();
            string line1, line2;

            if (state.IsPreview)
            {
                line1 = $"Preview: {state.ObjectCount} objects | {state.Area:F1}m² | ~{state.TokenEstimate}t";
                line2 = $"Verts: {state.VertexCount} | Enter=Commit  Esc=Cancel";
            }
            else
            {
                line1 = $"Mode: {modeName}{(state.GridSnap ? "  [Snap ON]" : "")}";
                line2 = $"Points: {state.RawVertexCount} | Esc=Cancel";
            }

            GUI.Box(new Rect(10, 10, 240, 44), GUIContent.none, EditorStyles.helpBox);
            GUI.Label(new Rect(14, 14, 232, 18), line1, EditorStyles.miniLabel);
            GUI.Label(new Rect(14, 30, 232, 18), line2, EditorStyles.miniLabel);
            Handles.EndGUI();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        /// <summary>Build closed Vector3 buffer from 2D vertices (y=0.01 avoids z-fight).</summary>
        internal static Vector3[] BuildHandlesBuffer(IReadOnlyList<Vector2> verts, bool closed)
        {
            int n = verts.Count;
            var buf = new Vector3[closed ? n + 1 : n];
            for (int i = 0; i < n; i++)
                buf[i] = new Vector3(verts[i].x, 0.01f, verts[i].y);
            if (closed) buf[n] = buf[0];
            return buf;
        }

        static Vector2[] ToArray(IReadOnlyList<Vector2> verts)
        {
            var arr = new Vector2[verts.Count];
            for (int i = 0; i < verts.Count; i++) arr[i] = verts[i];
            return arr;
        }
    }
}
