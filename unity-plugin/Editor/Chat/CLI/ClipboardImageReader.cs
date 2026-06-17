// Platform-specific clipboard image read.
// Returns PNG bytes or null (never throws — all exceptions are caught).
// macOS: NSPasteboard via Foundation PInvoke (Editor is not sandboxed).
// Windows: user32 CF_DIB check (drag-from-file still works without this).
// Linux: xclip subprocess.
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor.Chat
{
    internal static class ClipboardImageReader
    {
        /// <summary>Returns PNG bytes if clipboard contains an image, otherwise null.</summary>
        internal static byte[] TryRead()
        {
            try
            {
#if UNITY_EDITOR_OSX
                return TryReadMac();
#elif UNITY_EDITOR_WIN
                return TryReadWin();
#else
                return TryReadLinux();
#endif
            }
            catch { return null; }
        }

        // ── macOS ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR_OSX
        // Foundation PInvoke — Unity Editor on macOS is NOT sandboxed.
        // All selectors return IntPtr; length is cast from NSUInteger (pointer-width).
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        private static extern IntPtr objc_getClass(string name);
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        private static extern IntPtr sel_registerName(string name);
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        private static extern IntPtr objc_msgSend(IntPtr self, IntPtr sel);
        [DllImport("/System/Library/Frameworks/Foundation.framework/Foundation")]
        private static extern IntPtr objc_msgSend(IntPtr self, IntPtr sel, IntPtr arg);

        private static byte[] TryReadMac()
        {
            // generalPasteboard
            var pbCls = objc_getClass("NSPasteboard");
            var pb    = objc_msgSend(pbCls, sel_registerName("generalPasteboard"));
            if (pb == IntPtr.Zero) return null;

            // Try UTIs in priority order
            string[] utis = { "public.png", "public.jpeg", "public.tiff" };
            var dataForType = sel_registerName("dataForType:");
            var lengthSel   = sel_registerName("length");
            var bytesSel    = sel_registerName("bytes");

            foreach (var uti in utis)
            {
                var nsStr  = NewNSString(uti);
                var nsData = objc_msgSend(pb, dataForType, nsStr);
                // no retain/release needed — autorelease pool manages these
                if (nsData == IntPtr.Zero) continue;

                // length returns NSUInteger (pointer-width)
                var len   = (long)objc_msgSend(nsData, lengthSel).ToInt64();
                var bytes = objc_msgSend(nsData, bytesSel);
                if (len <= 0 || bytes == IntPtr.Zero) continue;

                var result = new byte[len];
                Marshal.Copy(bytes, result, 0, (int)len);

                // TIFF → PNG via Texture2D
                if (uti == "public.tiff") result = TiffToPng(result);
                return result;
            }
            return null;
        }

        private static IntPtr NewNSString(string s)
        {
            var cls = objc_getClass("NSString");
            var sel = sel_registerName("stringWithUTF8String:");
            var ptr = Marshal.StringToHGlobalAnsi(s);
            try   { return objc_msgSend(cls, sel, ptr); }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        private static byte[] TiffToPng(byte[] tiff)
        {
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(tiff)) { Object.DestroyImmediate(tex); return null; }
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }
#endif

        // ── Windows ───────────────────────────────────────────────────────────
#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);
        private const uint CF_DIB = 8;

        private static byte[] TryReadWin()
        {
            // On Windows, Finder-equivalent drag works via DragAndDrop.paths.
            // Clipboard image (Snipping Tool / PrintScreen) needs CF_DIB → Bitmap → PNG.
            // For now return null; drag-from-file path covers the primary use case.
            if (!IsClipboardFormatAvailable(CF_DIB)) return null;
            return null; // TODO: CF_DIB → PNG conversion
        }
#endif

        // ── Linux ─────────────────────────────────────────────────────────────
#if !UNITY_EDITOR_OSX && !UNITY_EDITOR_WIN
        private static byte[] TryReadLinux()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("xclip",
                    "-selection clipboard -t image/png -o")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                using var ms   = new System.IO.MemoryStream();
                proc.StandardOutput.BaseStream.CopyTo(ms);
                // C5: 2-second timeout to avoid blocking Unity main thread
                if (!proc.WaitForExit(2000))
                {
                    try { proc.Kill(); } catch { }
                    return null;
                }
                var bytes = ms.ToArray();
                return bytes.Length > 0 ? bytes : null;
            }
            catch { return null; }
        }
#endif
    }
}
