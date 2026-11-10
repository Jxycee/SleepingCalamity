# Reference crops -> four Hunyuan3D-2mv input views in ONE shared head frame (trellis venv python: numpy + cv2).
#   python mv_cutouts.py <refs_dir> <hull_dir> <out_dir> [--zcut 1.60] [--half 0.18]
# Each crop is resampled through the hull calibration (hull_prep.py: px = cx + s*h, py = eye_py - sz*(z - eye_z)) onto a square canvas
# centred on the head origin (h 0, z 1.75), so front / left / right / back share scale and centre the way the mv model expects
# (orthographic views of one object). Below zcut (chin) is dropped, and so are the cream hood and white background.
# 'left' = subject's left side, nose to image LEFT (= crop_side); 'right' = mirrored crop_side (no separate right crop yet).
import sys, os, json, numpy as np, cv2
refs, hull, out = sys.argv[1:4]; os.makedirs(out, exist_ok=True)
arg = lambda k, d: float(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
ZCUT, HALF, ZC, N = arg('--zcut', 1.60), arg('--half', 0.18), 1.75, 1024
cal = json.load(open(os.path.join(hull, 'hull_cal.json'))); EYE = cal['eye_z']
k = 0.92 * N / (2 * HALF)                                     # canvas px per metre
u, v = np.meshgrid(np.arange(N, dtype=np.float32), np.arange(N, dtype=np.float32))
h = (u - N / 2) / k; z = ZC - (v - N / 2) / k
views = {}
for name, src in (('front', 'front'), ('left', 'side'), ('back', 'back')):
    c = cal[src]; s = c['s']; sz = c.get('sz', s)
    im = cv2.imread(os.path.join(refs, f'crop_{src}.png'))
    mx = (c['cx'] + s * h).astype(np.float32); my = (c['eye_py'] - sz * (z - EYE)).astype(np.float32)
    rgb = cv2.remap(im, mx, my, cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=(255, 255, 255))
    hsv = cv2.cvtColor(cv2.GaussianBlur(rgb, (0, 0), 1.5), cv2.COLOR_BGR2HSV).astype(np.int16)
    fg = rgb.min(axis=2) < 232
    cream = (hsv[..., 1] < 46) & (hsv[..., 2] > 150)          # hood / shirt: low saturation, bright
    m = fg & ~cream & (z >= ZCUT)
    m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m); m = (lab == 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA]))).astype(np.uint8)
    cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); m = np.zeros_like(m); cv2.drawContours(m, cs, -1, 1, -1)  # fill holes
    a = cv2.GaussianBlur(m.astype(np.float32) * 255, (0, 0), 1.0)
    views[name] = np.dstack([rgb, a.astype(np.uint8)])
views['right'] = views['left'][:, ::-1].copy()
# skin masks (mv_hair.py drops skin-coloured faces): skin is redder than hair (Lab a* >= 10; hair highlights stay <= 9), bright skin
# highlights pass on lightness; a close fills eyes / brows inside the face.
for n_, im in views.items():
    lab = cv2.cvtColor(cv2.GaussianBlur(im[..., :3], (0, 0), 2), cv2.COLOR_BGR2LAB).astype(np.float32); L, A = lab[..., 0], lab[..., 1] - 128
    sk = (((A >= 10) & (L > 70)) | ((L > 165) & (A > 5))) & (im[..., 3] > 127)
    sk = cv2.morphologyEx(cv2.morphologyEx(sk.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)), cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8))
    cv2.imwrite(os.path.join(out, f'skin_{n_}.png'), sk * 255)
for n_, im in views.items(): cv2.imwrite(os.path.join(out, f'hair_{n_}.png'), im)
tops = {n_: float(ZC - (np.where(views[n_][..., 3].max(1) > 127)[0][0] - N / 2) / k) for n_ in ('front', 'left', 'back')}
json.dump(dict(zcut=ZCUT, half=HALF, zc=ZC, px_per_m=k, ztop_front=max(tops.values()), tops=tops), open(os.path.join(out, 'frame.json'), 'w'), indent=1)
sheet = np.hstack([cv2.resize(np.where(views[n_][..., 3:] > 127, views[n_][..., :3], 90).astype(np.uint8), (384, 384)) for n_ in ('front', 'left', 'right', 'back')])
for i in range(4): cv2.line(sheet, (i * 384, int(384 / 2 - (EYE - ZC) * k * 384 / N)), ((i + 1) * 384, int(384 / 2 - (EYE - ZC) * k * 384 / N)), (0, 255, 0), 1)
cv2.imwrite(os.path.join(out, 'cutouts.png'), sheet); print('CUTOUTS ->', out, 'px/m', round(k, 1))
