# Comparison sheet: reference crops on top, then the top-N variants (front / side / back previews), ~1100 px wide (PIL; any python with Pillow).
#   python sweep_sheet.py <round_dir> <refs_dir> [--top 4]      reads <round_dir>/ranking.json (list of {id, rank, iou_mean, ...}) -> sheet.png
import sys, os, json
from PIL import Image, ImageDraw
rd, refs = sys.argv[1:3]; top = int(sys.argv[sys.argv.index('--top') + 1]) if '--top' in sys.argv else 4
rank = json.load(open(os.path.join(rd, 'ranking.json')))[:top]
LW, TW, TH = 200, 300, 300; W = LW + 3 * TW; views = ('front', 'side', 'back')
def tile(path, crop=None):
    t = Image.new('RGB', (TW, TH), (30, 30, 30))
    if os.path.exists(path):
        im = Image.open(path).convert('RGB')
        if crop: im = im.crop(crop(im))
        im.thumbnail((TW, TH)); t.paste(im, ((TW - im.width) // 2, (TH - im.height) // 2))
    return t
S = Image.new('RGB', (W, TH * (1 + len(rank))), (20, 20, 20)); d = ImageDraw.Draw(S)
d.text((8, 8), 'REFERENCE', fill=(255, 255, 255))
for k, v in enumerate(views): S.paste(tile(os.path.join(refs, f'crop_{v}.png')), (LW + k * TW, 0))
fmt = lambda x, f='%.3f': '-' if x is None else f % x
for r, e in enumerate(rank, 1):
    y = r * TH; vd = os.path.join(rd, 'variants', e['id'])
    lines = [f"#{r} {e['id']}", f"rank {fmt(e['rank'])}", f"IoU {fmt(e['iou_mean'])} frame {fmt(e['iou_frame'], '%.2f')}", f"jut side {fmt(e['jut_side_frac'], '%.3f')}", f"dE {fmt(e['dE_mean'], '%.1f')}{' WARM' if e.get('warm_back') else ''}"]
    for i, t in enumerate(lines): d.text((8, y + 8 + 16 * i), t, fill=(255, 220, 120) if i == 0 else (230, 230, 230))
    for k, v in enumerate(views):   # previews are 700 px with the head centred: keep the upper 2/3, the hair
        S.paste(tile(os.path.join(vd, f'prev_{v}.png'), lambda im: (70, 20, im.width - 70, int(im.height * 0.80))), (LW + k * TW, y))
S.save(os.path.join(rd, 'sheet.png')); print('SHEET', os.path.join(rd, 'sheet.png'), S.size)
