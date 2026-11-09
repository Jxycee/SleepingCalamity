"""Strip out6/r5_r6.png (venv python: PIL): row 1 photo | r4 front | r5 front (Cycles cells); row 2 clay 3/4 and row 3 side (fit6/clay_r56.png from r6_sheet.py: rows r4, r5);
rows 4-5: jawOpen 0.5 and aa mouth crops for r4 | r5 (cells of out5/, out6/)."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
from PIL import Image, ImageDraw
D2 = _P_RUN; W = 560
def L(p, h=W):
    im = Image.open(p).convert('RGB'); return im.resize((int(im.width * h / im.height), h))
def row(ims, labs):
    h = max(i.height for i in ims); r = Image.new('RGB', (sum(i.width for i in ims) + 6 * len(ims), h), (25, 25, 25)); x = 0
    for i, lb in zip(ims, labs): r.paste(i, (x, 0)); ImageDraw.Draw(r).text((x + 6, 4), lb, fill=(255, 220, 120)); x += i.width + 6
    return r
cl = Image.open(D2 + 'fit6/clay_r56.png'); cw = cl.width // 3
def clay(i, j): return cl.crop((j * cw, i * cw, (j + 1) * cw, (i + 1) * cw)).resize((W, W))
ph = Image.open(D2 + 'target/crop_src.png').convert('RGB'); ph = ph.resize((int(ph.width * W / ph.height), W))
rows = [row([ph, L(D2 + 'out5/cells/front_neutral_full.png'), L(D2 + 'out6/cells/front_neutral_full.png')], ['photo', 'r5 front', 'r6 front']),
        row([clay(0, 1), clay(1, 1)], ['r5 3/4 clay', 'r6 3/4 clay']), row([clay(0, 2), clay(1, 2)], ['r5 side clay', 'r6 side clay'])]
for nm in ('jawOpen', 'aa', 'oh'):
    rows.append(row([L(D2 + f'out{v}/cells/mouth_{nm}_full.png', 330) for v in (5, 6)], [f'r5 {nm}', f'r6 {nm}']))
import json
_lm = __import__('numpy').array(json.load(open(D2 + 'target/landmarks.json'))['landmarks_px_xyz'])[:, :2]
_ids = [33, 263, 133, 362, 105, 334, 159, 386, 145, 374]; _x0, _y0 = _lm[_ids].min(0) - 40; _x1, _y1 = _lm[_ids].max(0) + 40
_pe = Image.open(D2 + 'target/crop_src.png').convert('RGB').crop((int(_x0), int(_y0), int(_x1), int(_y1))); _pe = _pe.resize((int(_pe.width * 330 / _pe.height), 330))
rows.append(row([_pe] + [L(D2 + f'out{v}/cells/eyes_neutral_full.png', 330) for v in (5, 6)] + [L(D2 + 'out6/cells/eyes_blink_full.png', 330)], ['photo eyes', 'r5 eyes', 'r6 eyes', 'r6 blink']))
Wd = max(r.width for r in rows); im = Image.new('RGB', (Wd, sum(r.height for r in rows)), (25, 25, 25)); y = 0
for r in rows: im.paste(r, (0, y)); y += r.height
im.save(D2 + 'out6/r5_r6.png'); print(im.size)
