"""sc_tex_sheet.py (M8): the batch contact sheet and its colour/value measurement (the rewritten review protocol for tileables, sc-asset-review / sc-texture-paint).
python Art/Blender/textures/sc_tex_sheet.py <batch dir, e.g. Art/Textures/B1_town> [--work <scratch dir>] [--cell 256] [--cols 4] [--tile 2.0]
1. one Blender session (sc_tex_sheet_blender.py) renders every material as a plane (2 x 2 tiles) plus a sphere under neutral light, calibrated so 50% grey reads 0.5;
2. the cells are assembled into <batch dir>/sheet.jpg (1024 px wide at the defaults) with the material names;
3. sc_ref_palette.analyse (k = 7, Lab) measures every cell (value p10/p50/p90 as sRGB max channel, light-vs-dark warmth, masses) and the pooled sheet:
   written to <batch dir>/sheet_measure.json and printed, for comparison with PHASE1 3.1.
"""
import argparse, importlib.util, json, os, subprocess, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location('sc_ref_palette', os.path.join(HERE, '..', 'tools', 'sc_ref_palette.py'))
RP = importlib.util.module_from_spec(spec); spec.loader.exec_module(RP)
BLENDER = os.environ.get('BLENDER', r'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe')

ap = argparse.ArgumentParser()
ap.add_argument('batch_dir'); ap.add_argument('--work', default=''); ap.add_argument('--cell', type=int, default=256)
ap.add_argument('--cols', type=int, default=4); ap.add_argument('--tile', type=float, default=2.0)
ap.add_argument('--skip-render', action='store_true')
a = ap.parse_args()
batch = os.path.abspath(a.batch_dir)
work = os.path.abspath(a.work or os.path.join(batch, '..', '_sheet_work', os.path.basename(batch)))
os.makedirs(work, exist_ok=True)
if not a.skip_render:
    r = subprocess.run([BLENDER, '-b', '--factory-startup', '--python-exit-code', '1', '--python', os.path.join(HERE, 'sc_tex_sheet_blender.py'), '--',
                        batch, work, str(a.cell * 2), str(a.tile)], capture_output=True, text=True)
    print('\n'.join(l for l in r.stdout.splitlines() if l.startswith(('CAL', 'CELL'))))
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:]); sys.exit(1)
names = sorted(d for d in os.listdir(batch) if os.path.isdir(os.path.join(batch, d)))
rows = (len(names) + a.cols - 1) // a.cols
S = Image.new('RGB', (a.cell * a.cols, a.cell * rows), (30, 30, 30)); dr = ImageDraw.Draw(S)
meas = {}; pooled = []
for i, n in enumerate(names):
    rgba = Image.open(os.path.join(work, n + '.png')).convert('RGBA')
    im = Image.new('RGB', rgba.size, (30, 30, 30)); im.paste(rgba, (0, 0), rgba)
    al = np.asarray(rgba.resize((256, 256)), np.float32)[..., 3].reshape(-1) > 200
    t = im.resize((a.cell, a.cell), Image.LANCZOS)
    x, y = (i % a.cols) * a.cell, (i // a.cols) * a.cell
    S.paste(t, (x, y)); dr.text((x + 4, y + 3), n, fill=(255, 255, 0))
    rgb = (np.asarray(im.resize((256, 256)), np.float32).reshape(-1, 3) / 255.0)[al]   # subject pixels only (not the backdrop)
    m = RP.analyse(rgb, 7); meas[n] = {k: m[k] for k in ('p10', 'p50', 'p90', 'light_warmth')}; meas[n]['masses'] = [(h, round(s, 3)) for h, s, _ in m['masses'][:4]]
    pooled.append(rgb)
S.save(os.path.join(batch, 'sheet.jpg'), quality=88)
pm = RP.analyse(np.concatenate(pooled), 7)
meas['POOLED'] = {k: pm[k] for k in ('p10', 'p50', 'p90', 'light_warmth')}; meas['POOLED']['masses'] = [(h, round(s, 3)) for h, s, _ in pm['masses']]
json.dump(meas, open(os.path.join(batch, 'sheet_measure.json'), 'w'), indent=1)
for n, m in meas.items():
    print('%-14s p10 %.2f p50 %.2f p90 %.2f warmth %+.2f  %s' % (n, m['p10'], m['p50'], m['p90'], m['light_warmth'], ' '.join(h for h, _ in m['masses'])))
print('sheet ->', os.path.join(batch, 'sheet.jpg'))
