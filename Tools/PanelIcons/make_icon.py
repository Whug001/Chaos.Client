"""Draws the 32x32 page-swap icon (no parchment): two bronze slot frames, the back one up and to the right,
and a gold arrow pointing right, after the owner's concept art.

Run: python3 Tools/PanelIcons/make_icon.py [out.png]
"""
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else HERE.parents[1] / "Chaos.Client" / "Assets" / "page-swap.png"

OUTLINE = (24, 14, 8, 255)
BRONZE_HI = (244, 214, 150, 255)
BRONZE_LIGHT = (226, 170, 92, 255)
BRONZE = (196, 130, 64, 255)
BRONZE_DARK = (140, 84, 38, 255)
INNER_RIM = (122, 86, 58, 255)
INNER = (76, 50, 34, 255)
INNER_DARK = (58, 38, 26, 255)
GOLD_HI = (255, 244, 190, 255)
GOLD_LIGHT = (252, 214, 90, 255)
GOLD = (230, 172, 40, 255)
GOLD_DARK = (176, 112, 20, 255)

S = 32
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
px = img.load()


def frame(x0, y0, size):
    """A square bronze frame: 1px outline, 2px bevelled rim, 1px inner rim, dark well."""
    x1, y1 = x0 + size - 1, y0 + size - 1
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            edge = min(x - x0, y - y0, x1 - x, y1 - y)
            if edge == 0:
                c = OUTLINE
            elif edge in (1, 2):
                # lit from the top-left: top/left rims light, bottom/right rims dark
                top_left = (x - x0) <= (y1 - y) and (y - y0) <= (x1 - x) if False else ((x - x0) == edge or (y - y0) == edge)
                c = (BRONZE_LIGHT if edge == 1 else BRONZE) if top_left else (BRONZE if edge == 1 else BRONZE_DARK)
            elif edge == 3:
                c = OUTLINE
            elif edge == 4:
                c = INNER_RIM
            else:
                c = INNER if (x - x0) + (y - y0) < size else INNER_DARK
            px[x, y] = c
    # glints on the top-left corner and top edge
    px[x0 + 1, y0 + 1] = BRONZE_HI
    px[x0 + 3, y0 + 1] = BRONZE_HI
    px[x0 + 4, y0 + 1] = BRONZE_HI
    px[x1 - 1, y0 + 1] = BRONZE_HI


#a 2px margin all round, like the retail ability icons, so it sits inside the slot
frame(12, 2, 16)  # back frame, up and to the right
frame(3, 9, 16)  # front frame, overlapping its lower-left corner

# gold arrow pointing right in the bottom-right corner: shaft rows 23-25, a 9px head with its tip at x=28
arrow = set()
for x in range(19, 24):
    for y in range(23, 26):
        arrow.add((x, y))
for k in range(5):
    for y in range(24 - (4 - k), 25 + (4 - k)):
        arrow.add((24 + k, y))
for x, y in arrow:
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            n = (x + dx, y + dy)
            if 0 <= n[0] < S and 0 <= n[1] < S and n not in arrow:
                px[n] = OUTLINE
for x, y in arrow:
    px[x, y] = GOLD_LIGHT if y <= 23 else GOLD if y <= 25 else GOLD_DARK
for x, y in [(20, 23), (21, 23), (24, 20), (24, 21)]:
    px[x, y] = GOLD_HI

OUT.parent.mkdir(parents=True, exist_ok=True)
img.save(OUT)
print(OUT)
