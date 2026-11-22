# A/B sheets for the AetherStrand hair trial, from TobinHairABCapture output (Assets/_Project/Editor/Builders/TobinHairABCapture.cs).
#   python ab_sheets.py <capture_dir> [--refs C:/AI/test_out/heroes/tobin/refs] [--out <dir>]
# Writes stills.jpg (reference row + HairDeck/AetherStrand rows, day and torch), motion.jpg (head turn, run-to-stop, idle strips),
# and torch.txt (does the torch light reach the hair? mean hair-region value with the torch on vs off, per variant).
import argparse, os, re
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ap = argparse.ArgumentParser(); ap.add_argument('cap'); ap.add_argument('--refs', default='C:/AI/test_out/heroes/tobin/refs'); ap.add_argument('--out')
A = ap.parse_args(); OUT = A.out or A.cap; os.makedirs(OUT, exist_ok=True)
try: FONT = ImageFont.truetype('arial.ttf', 18); SMALL = ImageFont.truetype('arial.ttf', 14)
except OSError: FONT = SMALL = ImageFont.load_default()
NAME = {'hairdeck': 'A: HairDeck cards (current)', 'aether': 'B: AetherStrand locks'}

def tile(p, s):
    if not os.path.exists(p): im = Image.new('RGB', (s, s), (40, 0, 0)); ImageDraw.Draw(im).text((8, 8), 'missing', fill=(255, 255, 255), font=SMALL); return im
    im = Image.open(p).convert('RGB'); w, h = im.size; k = s / max(w, h); im = im.resize((max(1, int(w * k)), max(1, int(h * k))), Image.LANCZOS)
    c = Image.new('RGB', (s, s), (20, 20, 20)); c.paste(im, ((s - im.width) // 2, (s - im.height) // 2)); return c

def sheet(rows, s, path, title):
    lab = 250; cols = max(len(r[1]) for r in rows)
    W, H = lab + cols * s, 40 + len(rows) * (s + 22)
    im = Image.new('RGB', (W, H), (12, 12, 12)); d = ImageDraw.Draw(im); d.text((10, 10), title, fill=(240, 240, 240), font=FONT)
    for ri, (label, cells) in enumerate(rows):
        y = 40 + ri * (s + 22); d.text((10, y + s // 2 - 10), label, fill=(230, 230, 230), font=FONT)
        for ci, (p, cap) in enumerate(cells):
            im.paste(tile(p, s), (lab + ci * s, y)); d.text((lab + ci * s + 4, y + s + 2), cap, fill=(180, 180, 180), font=SMALL)
    im.save(path, quality=90); print('sheet', path)

C = lambda n: os.path.join(A.cap, n + '.png')
rows = [('Reference', [(os.path.join(A.refs, f'crop_{v}.png'), f'ref {v}') for v in ('front', 'side', 'back')])]
for light in ('day', 'torch'):
    for v in ('hairdeck', 'aether'):
        rows.append((f'{NAME[v]}\n{light}', [(C(f'{v}_{light}_{vw}'), f'{light} {lbl}') for vw, lbl in (('f', 'front'), ('q', '3/4'), ('b', 'back'))]))
sheet(rows, 360, os.path.join(OUT, 'stills.jpg'), 'Tobin hair A/B: same camera, same light (day = Hollowmere Midday values, torch = warm point light front-left)')

times = ['0', '0.10', '0.20', '0.33', '0.50', '0.80', '1.20']
mrows = []
for m, lbl in (('headturn', 'head turn 60 deg / 0.1 s (3/4 back)'), ('runstop', 'run 5 m/s -> stop in 0.15 s (side)'), ('idle', 'idle (B: plugin wind 0.55, A: no wind)')):
    for v in ('hairdeck', 'aether'):
        mrows.append((f'{NAME[v]}\n{lbl}', [(C(f'{v}_{m}_{k}'), f't+{t}s') for k, t in enumerate(times)]))
sheet(mrows, 230, os.path.join(OUT, 'motion.jpg'), 'Tobin hair A/B motion strips (60 Hz solver steps, day light, wind 0 unless noted)')

lines = []
for v in ('hairdeck', 'aether'):
    try:
        on = np.asarray(Image.open(C(f'{v}_torch_f')).convert('RGB'), float); onn = np.asarray(Image.open(C(f'{v}_torch_f_nohair')).convert('RGB'), float)
        off = np.asarray(Image.open(C(f'{v}_torchoff_f')).convert('RGB'), float)
        mask = np.abs(on - onn).sum(2) > 12
        lines.append(f'{v}: hair px {int(mask.sum())}, mean value torch on {on[mask].mean():.1f} off {off[mask].mean():.1f} (diff {on[mask].mean() - off[mask].mean():.1f})')
    except Exception as e:
        lines.append(f'{v}: {e}')
open(os.path.join(OUT, 'torch.txt'), 'w').write('\n'.join(lines) + '\n'); print('\n'.join(lines))
