"""Draws the 32x32 page-swap icon shown in the last cell of skill/spell pages 1 and 2.

Run: python3 Tools/PanelIcons/make_icon.py
Writes Chaos.Client/Assets/page-swap.png (embedded in the client) and a 6x preview beside this script.
The look follows the retail ability icons: a torn tan parchment with a dark-brown ink drawing.
"""
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[1] / "Chaos.Client" / "Assets" / "page-swap.png"

OUTLINE = (33, 24, 16, 255)
SHADOW = (92, 57, 38, 255)
PAPER_DARK = (149, 98, 68, 255)
PAPER = (180, 124, 89, 255)
PAPER_LIGHT = (194, 140, 105, 255)
PAPER_HI = (210, 163, 124, 255)
INK = (92, 57, 38, 255)
INK_DARK = (60, 36, 24, 255)

S = 32
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
px = img.load()

# parchment body: 2..29 with ragged notches along the edges
left = [4, 3, 2, 2, 3, 4, 3, 2, 2, 2, 3, 2, 2, 1, 2, 2, 3, 3, 2, 2, 2, 1, 2, 2, 3, 2, 3, 4]
right = [26, 28, 29, 29, 28, 29, 30, 29, 29, 28, 27, 28, 29, 29, 29, 30, 29, 28, 29, 29, 28, 29, 30, 29, 28, 27, 28, 25]
top_y, bottom_y = 2, 29
for i, y in enumerate(range(top_y, bottom_y + 1)):
    l, r = left[i], right[i]
    for x in range(l, r + 1):
        # light from the top-left, darker toward the bottom-right
        t = (x - 2) / 27 + (y - 2) / 27
        px[x, y] = PAPER_HI if t < 0.35 else PAPER_LIGHT if t < 0.8 else PAPER if t < 1.45 else PAPER_DARK
# torn corner, bottom-left, and a dog-eared top-right
for x, y in [(2, 29), (3, 29), (2, 28), (4, 29)]:
    px[x, y] = (0, 0, 0, 0)
for x, y in [(29, 2), (28, 2), (29, 3)]:
    px[x, y] = (0, 0, 0, 0)
for x, y in [(27, 2), (28, 3), (29, 4)]:
    px[x, y] = PAPER_DARK

# outline the parchment, then a drop shadow down and right
body = {(x, y) for x in range(S) for y in range(S) if px[x, y][3]}
for x, y in list(body):
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        n = (x + dx, y + dy)
        if 0 <= n[0] < S and 0 <= n[1] < S and n not in body and px[n][3] == 0:
            px[n] = OUTLINE
for x, y in list(body):
    n = (x + 1, y + 1)
    if n[0] < S and n[1] < S and px[n][3] == 0:
        px[n] = SHADOW


def dot(x, y, c=INK):
    px[x, y] = c


def arrow(y0, tip_x, tail_x, direction):
    """A bold arrow: a 3px shaft on rows y0..y0+2 and a 9px-tall head ending at tip_x."""
    step = 1 if direction > 0 else -1
    head_len = 5
    base = tip_x - step * head_len
    for x in range(min(tail_x, base), max(tail_x, base) + 1):
        dot(x, y0, INK)
        dot(x, y0 + 1, INK)
        dot(x, y0 + 2, INK_DARK)
    for k in range(head_len + 1):
        x = base + step * k
        half = head_len - k
        for y in range(y0 + 1 - half - 1, y0 + 2 + half):
            dot(x, y, INK_DARK if y > y0 + 1 else INK)


# upper arrow pointing right, lower arrow pointing left: "move to the other page"
arrow(8, 25, 6, 1)
arrow(19, 6, 25, -1)

OUT.parent.mkdir(parents=True, exist_ok=True)
img.save(OUT)
img.resize((S * 6, S * 6), Image.NEAREST).save(HERE / "preview.png")
print(OUT)
