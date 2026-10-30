"""Blind scene A/B: reference crops on top, two 6-view candidate blocks below in random order.
python sc_stand_ab.py out.jpg tagA tagB seed --render-dir <dir with r/<tag>_<mood>_<view>.png> --refs ref0.png ref1.png
 [--mood hearth] [--views wide,fp,still,mortar,left,kcd_still]
Writes out.jpg and out.jpg.key.txt. Give reviewers only the jpg. Ported from FishGame (stand_ab.py); the
hard-coded session paths are now arguments. Views default to the potion-stand names: pass your own."""
import sys, random, argparse
from PIL import Image, ImageDraw

ap = argparse.ArgumentParser(description=__doc__)
ap.add_argument('out'); ap.add_argument('tag_a'); ap.add_argument('tag_b'); ap.add_argument('seed', type=int)
ap.add_argument('--render-dir', required=True); ap.add_argument('--refs', nargs='+', required=True)
ap.add_argument('--mood', default='hearth'); ap.add_argument('--views', default='wide,fp,still,mortar,left,kcd_still')
a = ap.parse_args()
V = a.views.split(',')

def tile(p, w, h):
    im = Image.open(p).convert('RGB'); im.thumbnail((w, h))
    c = Image.new('RGB', (w, h), (25, 25, 25)); c.paste(im, ((w - im.width) // 2, (h - im.height) // 2)); return c

pair = [('a', a.tag_a), ('b', a.tag_b)]; random.Random(a.seed).shuffle(pair)
S = Image.new('RGB', (1800, 420 + 2 * (2 * 300 + 40)), (25, 25, 25)); d = ImageDraw.Draw(S)
rw = 1800 // len(a.refs)
for k, p in enumerate(a.refs): S.paste(tile(p, rw, 420), (rw * k, 0))
for b, (lab, t) in enumerate(pair):
    y0 = 420 + b * 640
    d.text((10, y0 + 8), 'XY'[b], fill=(255, 255, 0))
    for k, v in enumerate(V):
        S.paste(tile('%s/r/%s_%s_%s.png' % (a.render_dir, t, a.mood, v), 600, 300), (600 * (k % 3), y0 + 40 + 300 * (k // 3)))
S.save(a.out, quality=90)
open(a.out + '.key.txt', 'w').write('X = %s (%s)\nY = %s (%s)\n' % (pair[0][0], pair[0][1], pair[1][0], pair[1][1]))
print('sheet', a.out)
