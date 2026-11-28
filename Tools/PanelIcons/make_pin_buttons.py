"""Draws the quest log's Pin and Unpin buttons (normal + pressed, 50x20) to match the stock Give Up / Close buttons.

The plate is setoa.dat's q_close.spf with its text area refilled from the plate's own edge colours. The letters are
whole blocks cut from q_gvp.spf ("GiveUp"), outline and all, so they keep the stock weight and shading:
U, p and i as they are; P is the p block raised two rows (bowl at cap height); n is the U's bottom curve flipped on
top of its straight stems. The pressed frames cut from GiveUp's pressed frame, whose text sits one row lower, and push
the word one pixel right and down the way Close's pressed frame does.

Run: python3 Tools/PanelIcons/make_pin_buttons.py [--game-dir DIR] [--unora-tools DIR] [--out DIR]
  --game-dir     folder holding setoa.dat (default ~/.local/share/chaos-launcher/Game)
  --unora-tools  Unora/Tools/Accessories, for acclib.daformats (default ~/Documents/GitHub/Unora/Tools/Accessories)
  --out          default Chaos.Client/Assets; writes quest-pin_0/1.png and quest-unpin_0/1.png
"""
import argparse
import random
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ap = argparse.ArgumentParser()
ap.add_argument("--game-dir", type=Path, default=Path.home() / ".local/share/chaos-launcher/Game")
ap.add_argument("--unora-tools", type=Path, default=Path.home() / "Documents/GitHub/Unora/Tools/Accessories")
ap.add_argument("--out", type=Path, default=HERE.parents[1] / "Chaos.Client" / "Assets")
args = ap.parse_args()
args.out.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(args.unora_tools))

from acclib.daformats import DatArchive, Spf  # noqa: E402
from PIL import Image  # noqa: E402

ARC = DatArchive.load(args.game_dir / "setoa.dat")
TOP, BOT, LEFT, RIGHT = 4, 16, 4, 46  # the text area that gets refilled


def frames(name: str) -> list[Image.Image]:
    spf = Spf.from_bytes(ARC.blobs[name])
    out = []
    for f in spf.frames:
        im = Image.frombytes("P", (f.byte_width, f.height), f.data[:f.byte_width * f.height]).crop((0, 0, f.width, f.height))
        im.putpalette(spf.palette_rgb())
        out.append(im.convert("RGB"))
    return out


def lum(c) -> int:
    return sum(c) // 3


def plate(fr: Image.Image, seed: int = 1) -> Image.Image:
    """The button with its text area refilled, row by row, from the middle tones of that row's clean edge columns."""
    rnd = random.Random(seed)
    im = fr.copy()
    px = im.load()
    for y in range(TOP, BOT):
        pool = sorted((px[x, y] for x in (4, 5, 6, 41, 42, 43, 44)), key=lum)[2:5]
        for x in range(LEFT, RIGHT):
            px[x, y] = pool[1] if rnd.random() < 0.7 else rnd.choice(pool)
    return im


def blocks(fr: Image.Image, dy: int) -> dict[str, Image.Image]:
    """Letter blocks (rows 5..15) from a GiveUp frame; dy is how far that frame's text sits below the normal one."""
    cut = lambda x0, x1, y0=5, y1=16: fr.crop((x0, y0 + dy, x1, y1 + dy))
    n = plate(fr).crop((30, 5 + dy, 40, 16 + dy))
    n.paste(fr.crop((30, 11 + dy, 40, 15 + dy)).transpose(Image.FLIP_TOP_BOTTOM), (0, 2))  # U's curve, now the arch
    n.paste(fr.crop((30, 8 + dy, 40, 12 + dy)), (0, 6))  # U's straight stems, down to the baseline
    return {"U": cut(30, 40), "p": cut(39, 47), "i": cut(14, 17), "P": cut(39, 47, 7, 18), "n": n}


def word(close: Image.Image, giveup: Image.Image, text: str, pressed: bool) -> Image.Image:
    push = 1 if pressed else 0
    base = plate(close)
    parts = [blocks(giveup, push)[c] for c in text]
    overlap = 1  # neighbouring blocks share their outline column
    x = (50 - (sum(p.width for p in parts) - overlap * (len(parts) - 1))) // 2 + push
    for part in parts:
        base.paste(part, (x, 5 + push))
        x += part.width - overlap
    return base


CLOSE, GIVEUP = frames("q_close.spf"), frames("q_gvp.spf")
for k in (0, 1):
    word(CLOSE[k], GIVEUP[k], "Pin", k == 1).save(args.out / f"quest-pin_{k}.png")
    word(CLOSE[k], GIVEUP[k], "Unpin", k == 1).save(args.out / f"quest-unpin_{k}.png")
print("wrote", *(args.out / f"quest-{n}_{k}.png" for n in ("pin", "unpin") for k in (0, 1)))
