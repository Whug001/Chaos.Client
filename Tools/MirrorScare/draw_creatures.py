"""Draws the haunted-mirror scare creatures: Bloody Mary, the Grinner and the Eye.

Every frame is 160 x 120 and drawn from shapes here (no AI art). The client scales them up to the
640 x 480 screen. Run from the client repo root:

    python Tools/MirrorScare/draw_creatures.py Chaos.Client.Rendering/Assets/MirrorScare
"""
import math, random, sys
import numpy as np
from PIL import Image, ImageDraw

W, H = 160, 120
OUT = sys.argv[1] if len(sys.argv) > 1 else "."

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
YY, XX = np.mgrid[0:H, 0:W]


def hexc(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.uint8)


def ramp(*hs):
    return [hexc(h) for h in hs]


class Canvas:
    def __init__(self, bg="#000000"):
        self.px = np.zeros((H, W, 3), dtype=np.uint8)
        self.px[:] = hexc(bg)

    def fill(self, mask, color):
        self.px[mask] = hexc(color) if isinstance(color, str) else color

    def shade(self, mask, value, rmp, dither=0.6):
        """value in 0..1 per pixel -> ramp index with ordered dithering."""
        n = len(rmp)
        d = (BAYER[YY % 4, XX % 4] - 0.5) * dither
        idx = np.clip(np.floor(value * n + d), 0, n - 1).astype(int)
        for i in range(n):
            m = mask & (idx == i)
            self.px[m] = rmp[i]

    def save(self, name):
        Image.fromarray(self.px, "RGB").save(f"{OUT}/{name}.png")


def ellipse(cx, cy, rx, ry, angle=0.0):
    c, s = math.cos(angle), math.sin(angle)
    dx, dy = XX - cx, YY - cy
    u = (dx * c + dy * s) / rx
    v = (-dx * s + dy * c) / ry
    return u * u + v * v <= 1.0


def lambert(cx, cy, rx, ry, lx=-0.45, ly=-0.55, lz=0.7):
    u = (XX - cx) / rx
    v = (YY - cy) / ry
    nz = np.sqrt(np.clip(1 - u * u - v * v, 0, 1))
    ln = math.sqrt(lx * lx + ly * ly + lz * lz)
    return np.clip((u * lx + v * ly + nz * lz) / ln, 0, 1)


def poly_mask(points):
    img = Image.new("L", (W, H), 0)
    ImageDraw.Draw(img).polygon([tuple(p) for p in points], fill=1)
    return np.array(img, dtype=bool)


def line_mask(points, width=1):
    img = Image.new("L", (W, H), 0)
    ImageDraw.Draw(img).line([tuple(p) for p in points], fill=1, width=width)
    return np.array(img, dtype=bool)


def outline(mask):
    m = np.zeros_like(mask)
    m[1:, :] |= mask[:-1, :]
    m[:-1, :] |= mask[1:, :]
    m[:, 1:] |= mask[:, :-1]
    m[:, :-1] |= mask[:, 1:]
    return m & ~mask


def walk(rng, x, y, ang, steps, step=2.0, wobble=0.5):
    pts = [(x, y)]
    for _ in range(steps):
        ang += rng.uniform(-wobble, wobble)
        x += math.cos(ang) * step
        y += math.sin(ang) * step
        pts.append((x, y))
    return pts


# ---------------------------------------------------------------- Bloody Mary
def bloody_mary(scream=1.0, look=0, name="mary"):
    rng = random.Random(4)
    c = Canvas("#000000")
    skin = ramp("#1e2430", "#39424f", "#596470", "#7e8a93", "#a6b0b4", "#c9d0cf")
    hair = ramp("#030305", "#0a0b10", "#14161e", "#20242f")
    blood = ramp("#2a0306", "#55080d", "#801016")

    # hair mass behind the head
    back = poly_mask([(36, 0), (124, 0), (134, 40), (140, 120), (20, 120), (26, 40)])
    c.fill(back, hair[1])

    # face
    fcx, fcy, frx, fry = 80, 64, 29, 41
    face = ellipse(fcx, fcy, frx, fry)
    jaw = poly_mask([(54, 80), (106, 80), (96, 108), (80, 112), (64, 108)])
    face |= jaw & ellipse(80, 80, 30, 33)
    val = lambert(fcx, fcy, frx + 4, fry + 4) * 0.95
    # hollow cheeks and temple shadows
    val = val - 0.32 * (ellipse(62, 82, 7, 12) | ellipse(98, 82, 7, 12))
    val = val - 0.25 * ellipse(80, 108, 16, 6)
    c.shade(face, np.clip(val, 0, 1), skin)

    # eye sockets
    for ex in (68, 93):
        sock = ellipse(ex, 58, 9.5, 7)
        c.shade(face & sock, np.clip(0.25 - 0.25 * (1 - ((XX - ex) ** 2 / 90 + (YY - 58) ** 2 / 49)), 0, 1), skin)
        c.fill(face & ellipse(ex, 58, 7, 4.8), "#0b0d12")
    # the visible eye (viewer's right): bloodshot rim, pale white, tiny pupil
    c.fill(ellipse(93, 58, 6, 3.6), "#5a0c10")
    c.fill(ellipse(93, 58, 5, 2.8), "#d8d6c4")
    c.fill(ellipse(93, 58.6, 5, 2.2) & (YY >= 59), "#a9a796")
    c.px[58:60, 93 + look:95 + look] = hexc("#000000")
    c.px[57, 93] = hexc("#f4f2e4")

    # nose: a shadow and two nostrils
    c.fill(line_mask([(81, 62), (82, 72)]), skin[2])
    c.fill(line_mask([(78, 75), (79, 75)]), "#0b0d12")
    c.fill(line_mask([(83, 75), (84, 75)]), "#0b0d12")
    c.fill(line_mask([(77, 73), (78, 74)]), skin[1])

    # screaming mouth
    mry = 2.5 + 10 * scream
    mcy = 82 + mry * 0.8
    mtop = mcy - mry
    mouth = ellipse(80, mcy, 6 + 2 * scream, mry)
    c.fill(outline(mouth) & face, skin[0])
    c.fill(mouth, "#140204")
    c.fill(mouth & ellipse(80, mcy + mry * 0.5, 6, mry * 0.55), "#2a0306")
    c.fill(mouth & ellipse(80, mcy + mry * 0.8, 4, mry * 0.25), blood[1])
    if scream > 0.4:
        for tx in (76, 78, 82, 84):
            c.fill(line_mask([(tx, mtop + 1.5), (tx, mtop + 3.5)]) & mouth, "#a49c86")
        c.fill(line_mask([(80, mtop + 0.5), (80, mtop + 2.5)]) & mouth, "#beb69e")

    # blood tears and mouth corners
    for pts in ([(92, 62), (91, 70), (92, 78), (90, 88), (91, 96)],
                [(95, 62), (96, 67), (96, 72)],
                [(73, 98), (72, 104), (73, 110)],
                [(86, 101), (87, 107)]):
        c.fill(line_mask(pts) & face, blood[1])
    c.fill(line_mask([(92, 62), (91, 70), (92, 78)]) & face & (XX == 92), blood[2])

    # skin cracks on the left cheek
    for start in ((58, 70, 1.4), (63, 92, -0.6), (70, 40, 1.2)):
        pts = walk(rng, *start, steps=7, step=2.2, wobble=0.7)
        c.fill(line_mask(pts) & face, skin[0])

    # fringe and a curtain of hair over the left eye
    strands = []
    for i in range(60):
        x0 = rng.uniform(34, 128)
        if x0 < 52 or x0 > 108:
            y1 = 120
        elif x0 < 76:
            y1 = rng.uniform(64, 92)
        else:
            y1 = rng.uniform(32, 44)
        strands.append((x0, x0 + rng.uniform(-3, 3), y1))
    hairmask = np.zeros((H, W), dtype=bool)
    for x0, xm, y1 in strands:
        pts = []
        for k in range(13):
            t = k / 12
            y = 6 + (y1 - 6) * t
            x = x0 + (xm - x0) * t + math.sin(t * 5 + x0) * 1.6
            pts.append((x, y))
        hairmask |= line_mask(pts, width=2)
    hairmask |= ellipse(80, 22, 40, 20) & (YY < 32)
    hairmask |= poly_mask([(44, 20), (54, 20), (52, 120), (36, 120), (38, 60)])
    hairmask |= poly_mask([(106, 20), (116, 20), (124, 60), (124, 120), (108, 120)])
    hairmask |= poly_mask([(58, 26), (78, 26), (77, 50), (72, 70), (64, 80), (57, 66)])
    c.fill(hairmask, hair[1])
    # strand highlights
    for x0, xm, y1 in strands[::2]:
        pts = [(x0 + (xm - x0) * k / 10 + math.sin(k / 10 * 5 + x0) * 1.6 + 0.6, 6 + (y1 - 6) * k / 10) for k in range(11)]
        hl = line_mask(pts) & hairmask & ((XX + YY) % 3 != 0)
        c.fill(hl, hair[2] if rng.random() < 0.7 else hair[3])
    c.fill(outline(hairmask) & face, hair[0])
    # a glint of the hidden eye through the hair
    c.px[58, 68] = hexc("#8a887a")
    c.save(name)


# ---------------------------------------------------------------- The Grinner
def grinner(open_=1.0, pupils=True, name="grinner"):
    c = Canvas("#000000")
    skin = ramp("#1d1f18", "#3a3d31", "#5d604f", "#868a74", "#aeb29a", "#d0d3bd")
    fcx, fcy, frx, fry = 80, 52, 40, 58
    head = ellipse(fcx, fcy, frx, fry)
    val = lambert(fcx, fcy, frx + 6, fry + 6)
    # deep brow ridge shadow and sunken temples
    val = val - 0.35 * (ellipse(60, 48, 16, 10) | ellipse(100, 48, 16, 10))
    val = val + 0.12 * (ellipse(60, 38, 14, 4) | ellipse(100, 38, 14, 4))
    val = val - 0.3 * (ellipse(44, 52, 6, 16) | ellipse(116, 52, 6, 16))
    # cheeks bunched up by the smile
    val = val + 0.18 * (ellipse(56, 70, 12, 7) | ellipse(104, 70, 12, 7))
    c.shade(head, np.clip(val, 0, 1), skin)

    # big black eyes, tilted, with pinprick pupils
    c.fill(ellipse(61, 50, 12.5, 7, angle=0.28), "#000000")
    c.fill(ellipse(99, 50, 12.5, 7, angle=-0.28), "#000000")
    c.fill(outline(ellipse(61, 50, 12.5, 7, angle=0.28)) & (YY > 50), skin[1])
    c.fill(outline(ellipse(99, 50, 12.5, 7, angle=-0.28)) & (YY > 50), skin[1])
    if pupils:
        c.px[50, 63] = hexc("#ffffff")
        c.px[50, 97] = hexc("#ffffff")

    # nose slits
    c.fill(line_mask([(77, 62), (76, 66)]), "#0c0d09")
    c.fill(line_mask([(83, 62), (84, 66)]), "#0c0d09")
    c.fill(line_mask([(80, 52), (80, 60)]), skin[2])

    # the smile: a band from cheek to cheek, deepest at the middle
    top, bot = [], []
    for k in range(41):
        t = k / 40
        x = 38 + 84 * t
        sag = math.sin(t * math.pi)
        yc = 70 + 14 * sag
        half = 0.6 + 6 * open_ * sag ** 1.5
        top.append((x, yc - half))
        bot.append((x, yc + half))
    mouth = poly_mask(top + bot[::-1])
    c.fill(outline(mouth) & head, skin[0])
    c.fill(mouth, "#160407")
    # two rows of thin teeth
    for k in range(2, 39):
        t = k / 40
        x = 38 + 84 * t
        sag = math.sin(t * math.pi)
        yc = 70 + 14 * sag
        half = 0.6 + 6 * open_ * sag ** 1.5
        if half < 1.6 or k % 2:
            continue
        th = max(1, int(half * 0.7))
        c.fill(line_mask([(x, yc - half + 0.5), (x, yc - half + th)]) & mouth, "#c8c0a0")
        c.fill(line_mask([(x + 1, yc + half - th), (x + 1, yc + half - 0.5)]) & mouth, "#a39b7e")
    # creases at the mouth corners and under the eyes
    for pts in ([(42, 66), (38, 60)], [(118, 66), (122, 60)], [(46, 74), (44, 80)], [(114, 74), (116, 80)],
                [(52, 58), (58, 61), (66, 60)], [(108, 58), (102, 61), (94, 60)]):
        c.fill(line_mask(pts) & head, skin[1])
    c.save(name)


# ---------------------------------------------------------------- The Eye
def the_eye(iris_dx=0, pupil_r=11, name="eye"):
    rng = random.Random(9)
    c = Canvas("#000000")
    lid = ramp("#1a0d0c", "#3a1e1b", "#5e3530", "#86524a", "#a9736a")
    white = ramp("#6e5d4c", "#a8977e", "#cdbfa5", "#e6dcc6")
    iris = ramp("#2a2a08", "#4f5212", "#7a7c1c", "#a8a52c", "#d1c45a")

    # lids / surrounding skin
    skin = np.ones((H, W), dtype=bool)
    sv = lambert(80, 60, 110, 80) * 0.8
    sv = sv + 0.04 * np.sin(YY * 0.9 + np.sin(XX * 0.08) * 3)
    c.shade(skin, np.clip(sv, 0, 1), lid)

    # the opening: an almond
    top, bot = [], []
    for k in range(51):
        t = k / 50
        x = 8 + 144 * t
        top.append((x, 62 - 40 * math.sin(t * math.pi) ** 0.8))
        bot.append((x, 62 + 30 * math.sin(t * math.pi) ** 0.9))
    opening = poly_mask(top + bot[::-1])
    # lid creases above and below
    for off in (8, 14, 21):
        pts = [(x, y - off + 3 * math.sin(x * 0.07)) for x, y in top[6:45]]
        c.fill(line_mask(pts), lid[1])
        c.fill(line_mask([(x, y + 1) for x, y in pts]) & (XX % 2 == 0), lid[3])
    for off in (6, 12):
        pts = [(x, y + off) for x, y in bot[8:43]]
        c.fill(line_mask(pts), lid[1])
    c.fill(outline(opening), "#0a0404")
    c.fill(outline(outline(opening) | opening) & (YY < 62), lid[0])

    # eyeball
    ev = lambert(80, 62, 76, 44, lx=-0.3, ly=-0.6, lz=0.75)
    c.shade(opening, ev, white)
    # shadow of the upper lid on the ball
    lidshadow = np.zeros((H, W), dtype=bool)
    lidshadow |= poly_mask(top + [(x, y + 6) for x, y in top[::-1]])
    c.shade(opening & lidshadow, np.full((H, W), 0.15), white)

    # veins: red random walks from the corners and edges toward the iris
    for _ in range(26):
        side = rng.random()
        if side < 0.35:
            x, y, a = rng.uniform(14, 34), rng.uniform(50, 76), rng.uniform(-0.4, 0.4)
        elif side < 0.7:
            x, y, a = rng.uniform(126, 146), rng.uniform(50, 76), math.pi + rng.uniform(-0.4, 0.4)
        else:
            x, y, a = rng.uniform(40, 120), 90, -math.pi / 2 + rng.uniform(-0.6, 0.6)
        pts = walk(rng, x, y, a, steps=rng.randint(6, 14), step=2.0, wobble=0.8)
        v = line_mask(pts) & opening
        c.fill(v, "#8c1a1a" if rng.random() < 0.6 else "#b0302a")
        if rng.random() < 0.5:
            mx, my = pts[len(pts) // 2]
            c.fill(line_mask(walk(rng, mx, my, a + rng.choice((-1, 1)), steps=4, step=2)) & opening, "#7a1616")

    # iris with radial streaks, a dark limbal ring and the pupil
    icx, icy, ir = 84 + iris_dx, 60, 25
    d = np.sqrt((XX - icx) ** 2 + (YY - icy) ** 2)
    ang = np.arctan2(YY - icy, XX - icx)
    irism = (d <= ir) & opening
    iv = 0.85 - 0.55 * (d / ir) + 0.18 * np.sin(ang * 23) * (d / ir) + 0.1 * np.sin(ang * 7 + 1)
    iv = iv - 0.25 * ((YY < icy - 8) & (d > 12))
    c.shade(irism, np.clip(iv, 0, 1), iris)
    c.fill(opening & (d > ir - 2) & (d <= ir), "#1c1c06")
    c.fill(opening & (d <= pupil_r), "#000000")
    # wet highlight
    c.fill(ellipse(76 + iris_dx, 52, 3, 2), "#f8f4e8")
    c.fill(ellipse(93 + iris_dx, 69, 1.2, 1.2), "#d8d4c8")
    # upper lid shadow over the iris too
    c.fill(irism & lidshadow, iris[0])

    # lashes along the upper lid
    for x, y in top[4:47:2]:
        a = -math.pi / 2 + (x - 80) / 160 + rng.uniform(-0.25, 0.25)
        L = rng.uniform(4, 9)
        c.fill(line_mask([(x, y - 1), (x + math.cos(a) * L, y - 1 + math.sin(a) * L)]), "#070303")
    # blood pooled at the lower lid
    c.fill(opening & poly_mask([(x, y - 3) for x, y in bot[18:34]] + [(x, y) for x, y in bot[33:17:-1]]), "#6a0c0e")
    c.save(name)


bloody_mary(0.0, -3, "mary0")
bloody_mary(0.5, -1, "mary1")
bloody_mary(1.0, 0, "mary2")
grinner(0.0, False, "grinner0")
grinner(0.0, True, "grinner1")
grinner(0.5, True, "grinner2")
grinner(1.0, True, "grinner3")
the_eye(-26, 13, "eye0")
the_eye(0, 13, "eye1")
the_eye(0, 7, "eye2")
