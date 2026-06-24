# Annotation Guide

Mark up screenshots in the Chat window to highlight issues or point out features.

## Overview

Annotations are visual overlays (lines, arrows, shapes, text) drawn on screenshots. They appear in LLM responses to help clarify communication.

**Access:** Chat window → Toolbar → Annotation tools (or keyboard shortcuts).

## Tools

| Tool | Shortcut | Purpose | Color |
|------|----------|---------|-------|
| Pen | P | Free-hand drawing | Teal |
| Line | L | Straight line | Teal |
| Arrow | A | Directional arrow | Orange |
| Rectangle | R | Rect outline | Yellow |
| Ellipse | O | Oval outline | Green |
| Text | T | Add text label | White on black bg |

## Usage

### Pen (Free-Hand)
```
1. Click Pen button or press P
2. Click + drag to draw
3. Release to finish stroke
4. Repeat for additional strokes
```

**Use case:** Circling UI elements, marking problem areas.

### Line (Straight)
```
1. Click Line button or press L
2. Click start point
3. Click end point
4. Press Enter to confirm or Escape to cancel
```

**Use case:** Connecting related elements, pointing along axis.

### Arrow (Directional)
```
1. Click Arrow button or press A
2. Click start point (tail)
3. Click end point (head)
4. Arrow automatically orients
```

**Use case:** Show direction of movement, flow, sequence.

### Rectangle (Outline)
```
1. Click Rectangle button or press R
2. Click top-left corner
3. Drag to bottom-right
4. Release to confirm
```

**Use case:** Highlight UI regions, problem zones, hitboxes.

### Ellipse (Oval)
```
1. Click Ellipse button or press O
2. Click center
3. Drag to set radius
4. Release to confirm
```

**Use case:** Highlight circular features (heads, spawners, projectiles).

### Text (Label)
```
1. Click Text button or press T
2. Click location to place text
3. Type label (single line)
4. Press Enter to confirm
```

**Use case:** Add callouts, error names, coordinates.

**Supported:** ASCII text, numbers, basic symbols.

## Example Workflow

```
Screenshot taken automatically after scene change

1. User sees issue in chat response
2. In annotation toolbar: select Pen
3. Draw circle around problematic UI element
4. Select Text
5. Type "Off by 10px"
6. Select Arrow
7. Draw from buggy element to expected position
8. Press Enter to commit annotations
9. Annotations visible in chat transcript
10. LLM sees annotated image in prompt
```

## Appearing in LLM Prompt

Annotated screenshots are sent as:

**Image + overlay data:**
- Raw screenshot (PNG)
- Annotation layer (as vector metadata or separate image)
- Text transcription of labels

**LLM interprets:**
```
"I see the health bar is misaligned (circled in teal).
You marked it should be at top-left corner (arrow points there).
Error: Off by 10px (as labeled)."
```

## Tips

**Clear annotations:**
- Click undo (Ctrl+Z) to remove last stroke
- Clear all: Click "Clear" button in annotation toolbar
- Revert: Press Escape before confirming

**Visibility:**
- Use contrasting colors (yellow on dark, teal on light)
- Thin lines for precision; thicker for UI elements
- Multiple arrows for flow sequences

**Efficiency:**
- Combine shapes (rectangle outline + text label inside)
- Use arrows to connect related annotations
- Keep text labels short and specific

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| P | Activate Pen |
| L | Activate Line |
| A | Activate Arrow |
| R | Activate Rectangle |
| O | Activate Ellipse |
| T | Activate Text |
| Ctrl+Z | Undo last stroke |
| Escape | Cancel current drawing |
| Enter | Confirm and finalize |

## Annotation Export

**Save annotated screenshot:**

```
Chat → Right-click message → Export annotation
→ Saves as: ScreenShots/YYYY-MM-DD_HH-MM-SS_annotated.png
```

**In transcript:** Annotated images appear inline with permanent metadata.

## Limitations

- Annotations are **image overlays** (not stored separately; baked into screenshot)
- Text is **single-line** (no multi-line labels)
- Colors are **fixed per tool** (no custom color picker)
- Precision is **relative to viewport** (if camera moves, annotations move too)

---

**See also:** `docs/chat/backends.md` for backend chat features, `docs/features/session-skills.md` for storing screenshots as baselines.
