using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor.Chat.Annotation
{
    internal static class AnnotationCompositor
    {
        /// <summary>
        /// Alpha-composite annotations over original PNG.
        /// Returns composited PNG bytes, or null on failure.
        /// </summary>
        internal static byte[] Composite(byte[] originalPng, IReadOnlyList<IAnnotationCommand> commands)
        {
            if (originalPng == null || originalPng.Length == 0) return null;
            if (commands == null || commands.Count == 0) return originalPng;

            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(originalPng))
            {
                Object.DestroyImmediate(tex);
                return null;
            }

            try
            {
                int w = tex.width, h = tex.height;
                var rasterizer = new AnnotationRasterizer(w, h);
                rasterizer.RenderAll(commands);

                var overlay    = rasterizer.Buffer;
                var basePixels = tex.GetPixels32();

                for (int i = 0; i < basePixels.Length; i++)
                {
                    var over = overlay[i];
                    if (over.a == 0)   continue;
                    if (over.a == 255) { basePixels[i] = over; continue; }
                    float sa = over.a / 255f, da = 1f - sa;
                    var b = basePixels[i];
                    basePixels[i] = new Color32(
                        (byte)(over.r * sa + b.r * da),
                        (byte)(over.g * sa + b.g * da),
                        (byte)(over.b * sa + b.b * da),
                        255);
                }

                tex.SetPixels32(basePixels);
                tex.Apply();
                return tex.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
