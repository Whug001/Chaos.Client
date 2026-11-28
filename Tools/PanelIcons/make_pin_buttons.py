"""Draws the quest log's Pin and Unpin buttons (normal + pressed, 50x20) to match the stock Give Up / Close buttons.

The plate is setoa.dat's q_close.spf with its text cleared. The letters are lifted from the stock buttons' own pixels:
U, p and i from q_gvp.spf ("GiveUp"); P is that p raised to cap height and n is the U's arch turned upside down. Each
letter gets the stock buttons' colours and shadow ring; the pressed frames rank GiveUp's pressed text by brightness
(the pressed frames are not pixel-aligned with the normal ones) and use its darkest tone for the ring.

Run: python3 Tools/PanelIcons/make_pin_buttons.py [--game-dir DIR] [--unora-tools DIR] [--out DIR]
  --game-dir     folder holding setoa.dat (default ~/.local/share/chaos-launcher/Game)
  --unora-tools  Unora/Tools/Accessories, for acclib.daformats (default ~/Documents/GitHub/Unora/Tools/Accessories)
  --out          default Chaos.Client/Assets; writes quest-pin_0/1.png and quest-unpin_0/1.png
"""
import argparse, random, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ap = argparse.ArgumentParser()
ap.add_argument("--game-dir", type=Path, default=Path.home() / ".local/share/chaos-launcher/Game")
ap.add_argument("--unora-tools", type=Path, default=Path.home() / "Documents/GitHub/Unora/Tools/Accessories")
ap.add_argument("--out", type=Path, default=HERE.parents[1] / "Chaos.Client" / "Assets")
args = ap.parse_args()
args.out.mkdir(parents=True, exist_ok=True)
sys.path.insert(0, str(args.unora_tools))
from collections import Counter
from acclib.daformats import DatArchive, Spf
from PIL import Image
arc=DatArchive.load(args.game_dir/'setoa.dat')
def load(name):
    spf=Spf.from_bytes(arc.blobs[name]); pal=spf.palette_rgb(); out=[]
    for f in spf.frames:
        im=Image.frombytes('P',(f.byte_width,f.height),f.data[:f.byte_width*f.height]).crop((0,0,f.width,f.height))
        im.putpalette(pal); out.append(im.convert('RGB'))
    return out
close=load('q_close.spf'); gvp=load('q_gvp.spf')
lum=lambda c: sum(c)//3
def level(c):
    l=lum(c); return 5 if l>200 else 4 if l>170 else 3 if l>135 else 2 if l>100 else 1 if l>65 else 0
TOP,BOT,L,R=4,16,4,46
# per-level colours for each frame k: positions where frame0 has that level -> frame k colour
def level_colors(src, k):
    p0=src[0].load(); pk=src[k].load(); cols={}
    for lv in range(6):
        c=Counter(pk[x,y] for y in range(5,15) for x in range(7,42) if level(p0[x,y])==lv)
        cols[lv]=c.most_common(1)[0][0] if c else None
    return cols
def core(src, x0, x1, y0, y1, minlv=3):
    p=src[0].load()
    return {(x-x0,y-y0):level(p[x,y]) for y in range(y0,y1) for x in range(x0,x1) if level(p[x,y])>=minlv}
# glyph = (dict of (x,y)->level relative to cap row 5, width)
def clean(g, miny=0):
    g={k:v for k,v in g.items() if k[1]>=miny}
    keep=lambda x,y,v: v>=4 or any(g.get((x+dx,y+dy),0)>=4 for dx in (-1,0,1) for dy in (-1,0,1) if dx or dy)
    return {(x,y):v for (x,y),v in g.items() if keep(x,y,v)}
def G(src,x0,x1,y0=5,y1=16,miny=0): return (clean(core(src,x0,x1,y0,y1),miny), x1-x0)
GL={
 'C':G(close,9,17),'l':G(close,19,20),'o':G(close,23,29),'s':G(close,30,36),'e':G(close,37,43),
 'U':G(gvp,31,39,miny=1),'p':G(gvp,40,46,miny=3),'i':G(gvp,15,16),
}
# P: p raised so its bowl top (row 8) sits at cap row 6
pc,pw=GL['p']; GL['P']=({(x,y-2):v for (x,y),v in pc.items() if 0<=y-2},pw)
# n: U upside down; keep its arch, put it at x-height (rows 8..13 abs -> 3..8 rel to 5)
uc,uw=GL['U']; ys=[y for (_,y) in uc]; top,bot=min(ys),max(ys)
flip={(x,top+bot-y):v for (x,y),v in uc.items()}   # arch now at row 'top'
nm={}
for (x,y),v in flip.items():
    r=y-top                      # 0 = arch row
    if r<=1: nm[(x,3+r)]=v       # arch + shoulder at x-height rows 3,4
    elif r>=bot-top-3: nm[(x,8-(bot-top-r))]=v   # last 4 leg rows end at baseline row 8
    # middle leg rows dropped
# drop the flipped serif feet: on the baseline keep only pixels directly under a bright stem
nm={(x,y):v for (x,y),v in nm.items() if y<8 or nm.get((x,7),0)>=4}
GL['n']=(clean(nm),uw)
def plate(fr,seed=1):
    rnd=random.Random(seed); im=fr.copy(); px=im.load()
    for y in range(TOP,BOT):
        pool=sorted((px[x,y] for x in (4,5,6,41,42,43,44)), key=lum)[2:5]
        for x in range(L,R): px[x,y]=pool[1] if rnd.random()<0.7 else rnd.choice(pool)
    return im
_g=gvp[1].load(); _cs=sorted((_g[x,y] for y in range(6,14) for x in range(5,46)), key=lum)
RING1=Counter(_cs[:max(1,len(_cs)//20)]).most_common(1)[0][0]   # darkest 5% of GiveUp's pressed text area
def draw(k,text,gap=2):
    cols=level_colors(close,k); im=plate(close[k]); px=im.load()
    w=sum(GL[c][1] for c in text)+gap*(len(text)-1); x0=(50-w)//2
    on={}
    for c in text:
        g,cw=GL[c]
        for (x,y),v in g.items(): on[(x0+x,5+y)]=v
        x0+=cw+gap
    for (x,y) in on:
        for dx,dy,lv in ((1,0,0),(0,1,0),(1,1,0),(-1,1,1),(1,-1,1),(-1,0,2),(0,-1,2),(-1,-1,1)):
            q=(x+dx,y+dy)
            if q not in on and L<=q[0]<R and TOP<=q[1]<BOT:
                #the pressed frame's ring is drawn in its darkest tone, a shade deeper than the plate
                px[q]=cols[lv] if k==0 else RING1
    if k==0:
        core_cols=cols
    else:
        #pressed frames are not pixel-aligned with the normal ones, so rank GiveUp's pressed text by brightness
        g=gvp[1].load(); cs=sorted((g[x,y] for y in range(6,14) for x in range(5,46)), key=lum); n=len(cs)
        pick=lambda a,b: Counter(cs[int(n*a):int(n*b)]).most_common(1)[0][0]
        core_cols={5:pick(0.94,1.0),4:pick(0.86,0.94),3:pick(0.75,0.86)}
    for q,v in on.items(): px[q]=core_cols[v]
    return im
for k in (0, 1):
    draw(k, "Pin").save(args.out / f"quest-pin_{k}.png")
    draw(k, "Unpin").save(args.out / f"quest-unpin_{k}.png")
print("wrote", *(args.out / f"quest-{n}_{k}.png" for n in ("pin", "unpin") for k in (0, 1)))
