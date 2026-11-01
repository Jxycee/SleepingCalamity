"""[Ported from FishGame, M8.] Blind A/B sheet: real reference photos on top, two candidate renders below in a random order.
python ab_sheet.py <out.png> --refs r1.jpg r2.jpg ... --a new.png --b old.png [--seed N]
python ab_sheet.py <out.png> --grid COLS WIDTH img1 img2 ...     (plain contact-sheet grid; the old sc_sheet.py)
Writes <out.png> and <out>.key.txt ("X = a|b"); give reviewers only the png. Use a fresh seed per test."""
import argparse, random
from PIL import Image, ImageDraw

def tile(p, w, h):
    im = Image.open(p).convert('RGB'); im.thumbnail((w, h))
    c = Image.new('RGB', (w, h), (40, 40, 40)); c.paste(im, ((w - im.width) // 2, (h - im.height) // 2)); return c

def grid(out, cols, w, paths):
    ims = [Image.open(p).convert('RGB') for p in paths]
    ims = [i.resize((w, int(i.height * w / i.width))) for i in ims]
    h = max(i.height for i in ims); rows = (len(ims) + cols - 1) // cols
    S = Image.new('RGB', (cols * w, rows * h), (20, 20, 20))
    for k, i in enumerate(ims): S.paste(i, ((k % cols) * w, (k // cols) * h))
    S.save(out)

import sys
if '--grid' in sys.argv:
    g = sys.argv.index('--grid'); out = sys.argv[1]
    grid(out, int(sys.argv[g + 1]), int(sys.argv[g + 2]), sys.argv[g + 3:]); print('sheet', out); sys.exit(0)
ap = argparse.ArgumentParser(); ap.add_argument('out'); ap.add_argument('--refs', nargs='+', required=True)
ap.add_argument('--a', required=True); ap.add_argument('--b', required=True); ap.add_argument('--seed', type=int, default=0)
a = ap.parse_args()
pair = [('a', a.a), ('b', a.b)]
random.Random(a.seed).shuffle(pair)
W = 1800; n = len(a.refs); rw = W // n
S = Image.new('RGB', (W, 600 + 40 + 640), (25, 25, 25)); d = ImageDraw.Draw(S)
for k, p in enumerate(a.refs): S.paste(tile(p, rw, 600), (rw * k, 0))
d.text((10, 608), 'REFERENCE (real photos)', fill=(255, 255, 255))
for k, (lab, p) in enumerate(pair):
    S.paste(tile(p, 900, 620), (900 * k, 640)); d.text((900 * k + 10, 1262), 'XY'[k], fill=(255, 255, 0))
S.save(a.out, quality=92) if a.out.lower().endswith('.jpg') else S.save(a.out)   # .jpg: the review image viewer palettises big PNGs into fake banding
open(a.out + '.key.txt', 'w').write(f'X = {pair[0][0]} ({pair[0][1]})\nY = {pair[1][0]} ({pair[1][1]})\n')
print('sheet', a.out)
