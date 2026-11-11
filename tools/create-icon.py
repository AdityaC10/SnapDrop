"""Rebuild the code-authored screenshot frame icon; requires Pillow only at development time."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
target = root / "packaging" / "assets"
target.mkdir(parents=True, exist_ok=True)
image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((20, 30, 236, 226), radius=30, fill=(28, 73, 128, 255))
draw.rounded_rectangle((54, 64, 202, 192), radius=10, outline=(255, 255, 255, 255), width=12)
draw.line((144, 148, 184, 108), fill=(255, 255, 255, 255), width=12)
draw.line((154, 108, 184, 108, 184, 138), fill=(255, 255, 255, 255), width=12)
image.save(target / "SnapDrop.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print(target / "SnapDrop.ico")
