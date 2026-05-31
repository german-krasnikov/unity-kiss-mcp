PROMPTS: dict[str, tuple[str, int]] = {
    "auto":            ("Describe this Unity scene in <=80 tokens: visible objects, layout, colors, anomalies. No preamble.", 100),
    "scene_overview":  ("List visible GameObjects + spatial layout in <=120 tokens. Bullet form. No preamble.", 150),
    "verify_position": ("Where is the main object positioned? Center/left/right/top/bottom + visible bounds. <=40 tokens.", 60),
    "verify_color":    ("Colors of foreground objects, hex if obvious. <=40 tokens. No preamble.", 60),
    "verify_visible":  ("PASS or FAIL: object visible and not clipped/occluded. 1 sentence.", 30),
    "ui_check":        ("UI elements: text content, alignment, overflow, contrast issues. <=100 tokens.", 120),
    "animation":       ("Pose/frame state: which limbs/parts moved, motion direction. <=60 tokens.", 80),
    "particle":        ("Particle effect: count est., color, spread, intensity. <=50 tokens.", 70),
    "multi_view":      ("4-panel view (Front/Right/Top/Iso). For each: object size+orientation. <=160 tokens total.", 200),
}


def resolve(key_or_text: str) -> tuple[str, int]:
    """If key in PROMPTS -> canned. Else treat as custom prompt, max_tokens=150."""
    if key_or_text in PROMPTS:
        return PROMPTS[key_or_text]
    return (key_or_text, 150)


def resolve_som(key_or_text: str, legend: str) -> tuple[str, int]:
    """Like resolve() but appends SoM legend to prompt. For mark=True calls."""
    base, max_tok = resolve(key_or_text)
    prompt = (
        f"{base}\n\n"
        f"Numbered elements on image — Legend: {legend}\n"
        "Reference elements by number when describing the scene."
    )
    return (prompt, max_tok + 50)
