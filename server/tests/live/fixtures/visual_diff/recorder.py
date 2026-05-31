"""Generate deterministic PNG pairs for live Haiku tests.

Run once: `python recorder.py`. Skips if files exist (idempotent).
Commits PNGs as binary fixtures — visual ground truth.
"""
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    raise SystemExit("Pillow required: pip install Pillow")

FIXTURES = Path(__file__).parent
SIZE = (256, 256)
FONT_SIZE = 48


def _save_skip_existing(img, path: Path):
    """Idempotent — skip if file already committed."""
    if path.exists():
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)
    return True


def make_move():
    """Box at (40,100) before, (180,100) after — clear position change."""
    base = Image.new("RGB", SIZE, "white")

    img = base.copy()
    ImageDraw.Draw(img).rectangle([40, 100, 90, 150], fill="red")
    _save_skip_existing(img, FIXTURES / "move_pair" / "before.png")

    img = base.copy()
    ImageDraw.Draw(img).rectangle([180, 100, 230, 150], fill="red")
    _save_skip_existing(img, FIXTURES / "move_pair" / "after.png")


def make_identical():
    """Same image twice — no change at all."""
    img = Image.new("RGB", SIZE, "white")
    ImageDraw.Draw(img).rectangle([100, 100, 150, 150], fill="green")
    _save_skip_existing(img, FIXTURES / "identical_pair" / "before.png")
    _save_skip_existing(img, FIXTURES / "identical_pair" / "after.png")


def make_color():
    """Same position, color red→blue."""
    base = Image.new("RGB", SIZE, "white")

    img = base.copy()
    ImageDraw.Draw(img).rectangle([100, 100, 150, 150], fill="red")
    _save_skip_existing(img, FIXTURES / "color_pair" / "before.png")

    img = base.copy()
    ImageDraw.Draw(img).rectangle([100, 100, 150, 150], fill="blue")
    _save_skip_existing(img, FIXTURES / "color_pair" / "after.png")


def make_som():
    """3 numbered boxes; in 'after' #2 is colored red."""
    def _draw_boxes(canvas: Image.Image, color_2):
        d = ImageDraw.Draw(canvas)
        positions = [(40, 100), (110, 100), (180, 100)]
        try:
            font = ImageFont.truetype("DejaVuSans.ttf", FONT_SIZE)
        except OSError:
            font = ImageFont.load_default()
        for i, (x, y) in enumerate(positions, 1):
            box_color = color_2 if i == 2 else "lightgray"
            d.rectangle([x, y, x + 50, y + 50], fill=box_color, outline="black")
            # Draw number INSIDE box for VLM grounding
            d.text((x + 15, y + 5), str(i), fill="black", font=font)
        return canvas

    img = Image.new("RGB", SIZE, "white")
    _draw_boxes(img, "lightgray")
    _save_skip_existing(img, FIXTURES / "som_pair" / "before.png")

    img = Image.new("RGB", SIZE, "white")
    _draw_boxes(img, "red")
    _save_skip_existing(img, FIXTURES / "som_pair" / "after.png")


if __name__ == "__main__":
    make_move()
    make_identical()
    make_color()
    make_som()
    print(f"Fixtures generated in {FIXTURES}")
    for p in FIXTURES.rglob("*.png"):
        print(f"  {p.relative_to(FIXTURES)}")
