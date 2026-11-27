"""Draws the 48x48 tile shown for each death sound in the Effects window's Death Sound tab.

Run: python3 Tools/PanelIcons/make_sound_tile.py
Writes Chaos.Client/Assets/death-sound.png (embedded in the client): a torn parchment like the ability icons,
with a beamed pair of eighth notes in brown ink.
"""
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[1] / "Chaos.Client" / "Assets" / "death-sound.png"

OUTLINE = (33, 24, 16, 255)
SHADOW = (92, 57, 38, 255)
PAPER_DARK = (149, 98, 68, 255)
PAPER = (180, 124, 89, 255)
PAPER_LIGHT = (194, 140, 105, 255)
PAPER_HI = (210, 163, 124, 255)
INK = (92, 57, 38, 255)
INK_DARK = (60, 36, 24, 255)

S = 48
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
px = img.load()

# parchment 8..39 with ragged edges, lit from the top-left
left = [10, 9, 8, 8, 9, 8, 8, 7, 8, 8, 9, 8, 8, 8, 7, 8, 8, 9, 8, 8, 8, 7, 8, 8, 9, 8, 8, 8, 9, 10]
right = [37, 38, 39, 39, 38, 39, 40, 39, 39, 38, 39, 39, 40, 39, 39, 38, 39, 39, 39, 40, 39, 38, 39, 39, 40, 39, 38, 39, 38, 36]
for i, y in enumerate(range(9, 39)):
    for x in range(left[i], right[i] + 1):
        t = (x - 8) / 31 + (y - 9) / 29
        px[x, y] = PAPER_HI if t < 0.35 else PAPER_LIGHT if t < 0.8 else PAPER if t < 1.45 else PAPER_DARK

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


def head(cx, cy):
    """A tilted oval note head, 6x4."""
    for x, y in [(cx + 1, cy - 1), (cx + 2, cy - 1), (cx + 3, cy - 1),
                 (cx, cy), (cx + 1, cy), (cx + 2, cy), (cx + 3, cy), (cx + 4, cy),
                 (cx, cy + 1), (cx + 1, cy + 1), (cx + 2, cy + 1), (cx + 3, cy + 1), (cx + 4, cy + 1),
                 (cx + 1, cy + 2), (cx + 2, cy + 2), (cx + 3, cy + 2)]:
        dot(x, y, INK_DARK if y > cy else INK)


# two eighth notes joined by a thick beam
head(14, 31)
head(26, 28)
for y in range(16, 31):
    dot(18, y)
    dot(19, y, INK_DARK)
for y in range(13, 28):
    dot(30, y)
    dot(31, y, INK_DARK)
for x in range(18, 32):
    # the beam rises 3px from left to right
    top = 16 - (x - 18) * 3 // 13
    for y in range(top, top + 3):
        dot(x, y, INK if y < top + 2 else INK_DARK)

OUT.parent.mkdir(parents=True, exist_ok=True)
img.save(OUT)
img.resize((S * 4, S * 4), Image.NEAREST).save(HERE / "sound_preview.png")
print(OUT)
