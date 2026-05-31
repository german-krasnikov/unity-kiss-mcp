"""Helper: annotate before/after images with SoM circles, call sampling."""
import hashlib
import os
import tempfile
from typing import TYPE_CHECKING, Optional

if TYPE_CHECKING:
    from ..sampling import SamplingService


async def diff_with_annotation(
    before: str,
    after: str,
    rects: list,
    rects_after: Optional[list],
    prompt: str,
    sampling: "SamplingService",
    feature: str,
) -> Optional[str]:
    """Annotate both frames with SoM circles, call sampling, cleanup."""
    from PIL import Image
    from .overlay import annotate

    # Build canonical pool once from union of both frames — ensures circles and
    # legend share the same index space (fixes index mismatch on subset frames).
    effective_after = rects_after or rects
    pool = sorted(
        {r.get("path") for r in rects if r.get("path")} |
        {r.get("path") for r in effective_after if r.get("path")},
        key=lambda p: hashlib.sha256(p.encode()).hexdigest(),
    )[:30]  # cap @ 30 to keep indices small ints

    with tempfile.TemporaryDirectory() as tmp:
        ann_before = os.path.join(tmp, "before.png")
        ann_after = os.path.join(tmp, "after.png")

        img_b = Image.open(before)
        annotated_b, _ = annotate(img_b, rects, path_pool=pool)
        annotated_b.save(ann_before)

        img_a = Image.open(after)
        annotated_a, _ = annotate(img_a, effective_after, path_pool=pool)
        annotated_a.save(ann_after)

        return await sampling.verify_visual_diff(ann_before, ann_after, prompt, feature=feature)
