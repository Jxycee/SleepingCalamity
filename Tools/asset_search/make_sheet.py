#!/usr/bin/env python3
"""Contact sheet: reference frames on the top row, ranked candidates below with rank, score, title, format, price.
  make_sheet.py --ranked ranked.csv --meta cands.jsonl --refs ref1.jpg ref2.jpg [ref3.jpg] --out sheet.jpg [--title "need"] [--cols 4]
Self-contained (PIL only); the layout idea follows Art/Blender/tools/sc_ab_sheet.py (--grid) but adds captions."""
import argparse, csv, json, os
from PIL import Image, ImageDraw, ImageFont

def tile(p, w, h):
    im = Image.open(p).convert('RGB'); im.thumbnail((w, h))
    c = Image.new('RGB', (w, h), (40, 40, 40)); c.paste(im, ((w - im.width) // 2, (h - im.height) // 2)); return c

ap = argparse.ArgumentParser()
ap.add_argument('--ranked', required=True); ap.add_argument('--meta'); ap.add_argument('--refs', nargs='+', required=True)
ap.add_argument('--out', required=True); ap.add_argument('--title', default=''); ap.add_argument('--cols', type=int, default=4)
a = ap.parse_args()
meta = {}
if a.meta:
    for l in open(a.meta, encoding='utf-8'):
        if l.strip():
            m = json.loads(l)
            if m.get('thumb_file'): meta[os.path.basename(m['thumb_file'])] = m
rows = list(csv.DictReader(open(a.ranked, encoding='utf-8')))
try: font = ImageFont.truetype('arial.ttf', 15); big = ImageFont.truetype('arial.ttf', 20)
except Exception: font = big = ImageFont.load_default()
TW, TH, CAP = 440, 250, 78
cols = a.cols; W = cols * TW
nrows = (len(rows) + cols - 1) // cols
S = Image.new('RGB', (W, 40 + 300 + 30 + nrows * (TH + CAP)), (25, 25, 25)); d = ImageDraw.Draw(S)
d.text((10, 8), a.title or 'asset search', fill=(255, 255, 255), font=big)
rw = W // len(a.refs)
for k, p in enumerate(a.refs): S.paste(tile(p, rw, 300), (rw * k, 40))
d.text((10, 344), 'REFERENCES above; candidates below ranked by style score', fill=(255, 220, 0), font=font)
y0 = 370
for i, r in enumerate(rows):
    x, y = (i % cols) * TW, y0 + (i // cols) * (TH + CAP)
    S.paste(tile(r['file'], TW - 6, TH), (x + 3, y))
    m = meta.get(os.path.basename(r['file']), {})
    fm = ','.join(f for f in m.get('formats', []) if f in ('fbx', 'gltf', 'unity', 'obj', 'blend', 'unreal-engine', 'texture')) or ','.join(m.get('formats', []))
    flag = ' [UE-ONLY]' if m.get('unreal_only') else ''
    price = 'free' if m.get('free') else m.get('price')
    d.text((x + 6, y + TH + 2), f"#{r['rank']}  {float(r['score']):.3f}  {price}{flag}", fill=(120, 255, 120), font=font)
    d.text((x + 6, y + TH + 22), (r.get('title') or os.path.basename(r['file']))[:48], fill=(255, 255, 255), font=font)
    d.text((x + 6, y + TH + 42), f"{m.get('creator', '')[:26]} | {fm[:40]}", fill=(190, 190, 190), font=font)
S.save(a.out, quality=90)
print('sheet', a.out)
