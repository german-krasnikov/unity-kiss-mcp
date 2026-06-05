"""Pixel-level image comparison — pure Pillow math, no external dependencies."""
from dataclasses import dataclass
from typing import Literal

DiffMode = Literal["auto", "pixel", "structural", "targeted",
                   "ui_layout", "animation", "color", "position"]

DIFF_PROMPTS = {
    "general": "Compare BEFORE (image 1) and AFTER (image 2). List semantic changes only:\n- <change>\nIgnore anti-aliasing/jitter. If no semantic change: 'NO_CHANGE'. Max 5 bullets.",
    "ui_layout": "BEFORE vs AFTER UI screenshots. Output:\n- moved: <element> from <where> to <where>\n- resized: <element>\n- broken: <element> (overlap/clipped/off-screen)\nIf intact: 'LAYOUT_OK'.",
    "animation": "BEFORE = frame 1, AFTER = frame N. Describe motion of foreground subject.\nFormat: '<subject> moved <direction> by <distance>'. If static: 'NO_MOTION'.",
    "color": "List color changes: '<element>: <before-color> -> <after-color>'. Ignore lighting noise. If no change: 'NO_COLOR_CHANGE'.",
    "position": "For each distinct element: '<element>: <before-pos> -> <after-pos>'. Use screen quadrants. Max 5.",
    "regression": "Was anything REMOVED, BROKEN, or VISUALLY CORRUPTED? Output: 'PASS' or 'FAIL: <reason>'.",
    "targeted": "Question: {question}\nAnswer in <=2 sentences. Start with YES or NO.",
}


@dataclass
class PixelDiff:
    similarity: float  # 0-100
    max_diff: int      # 0-255
    size_mismatch: bool
    identical: bool
    unavailable: bool = False  # PIL not installed
    corrupt: bool = False      # file unreadable / truncated


def _pixel_diff(before: str, after: str) -> PixelDiff:
    """Pure Pillow diff. Returns PixelDiff dataclass."""
    try:
        from PIL import Image, ImageChops
    except ImportError:
        return PixelDiff(0.0, 255, False, False, unavailable=True)
    try:
        img1 = Image.open(before).convert("RGB")
        img2 = Image.open(after).convert("RGB")
        if img1.size != img2.size:
            return PixelDiff(0.0, 255, True, False)
        diff = ImageChops.difference(img1, img2)
        bbox = diff.getbbox()
        if bbox is None:
            return PixelDiff(100.0, 0, False, True)
        extr = diff.getextrema()
        max_d = max(ch[1] for ch in extr)
        if max_d == 0:
            return PixelDiff(100.0, 0, False, True)
        total = img1.size[0] * img1.size[1] * 255 * 3
        # Use histogram for sum — avoids deprecated getdata()
        hist = diff.histogram()  # 256 buckets * 3 channels
        sum_diff = sum(v * i for ch in range(3) for i, v in enumerate(hist[ch*256:(ch+1)*256]))
        sim = max(0.0, 100.0 * (1 - sum_diff / total))
        return PixelDiff(sim, max_d, False, False)
    except (OSError, ValueError):
        # OSError = truncated file, FileNotFoundError, IO error
        # ValueError = PIL.UnidentifiedImageError (subclass)
        return PixelDiff(0.0, 255, False, False, corrupt=True)
    except Exception:
        return PixelDiff(0.0, 255, False, False, unavailable=True)


def _format_pixel(px: PixelDiff) -> str:
    if px.corrupt:
        return "PIXEL_CORRUPT"
    if px.unavailable:
        return "PIXEL_UNAVAILABLE"
    if px.identical:
        return "IDENTICAL (pixel)"
    if px.size_mismatch:
        return "SIZE_MISMATCH"
    return f"PIXEL: {px.similarity:.1f}% similar (max diff {px.max_diff})"
