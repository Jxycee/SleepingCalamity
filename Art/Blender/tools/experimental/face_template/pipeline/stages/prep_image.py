"""Stage 1 prep_image (venv python: cv2 numpy PIL). Config-driven version of the hand-run target/prep.py + prep2.py.
usage: prep_image.py <cfg.json>   -> target/{crop_src.png, mask_raw.png, mask_vis.png, cutout_rgba.png, input_1536.png}
cfg.prep: bg_seed (default true; false = the Cloud hand-tuned behaviour), bg_seed_dist, box [x0,y0,x1,y1] (crop in photo px) or null = auto from the mediapipe face box; ellipses (probable head / sure face / neck), cut_rows, side_cuts, border."""
import os, sys, json
import numpy as np, cv2
from PIL import Image
cfg = json.load(open(sys.argv[1])); P = cfg['prep']; D = os.environ['SCP_RUN'].rstrip('/') + '/target/'
im = np.array(Image.open(cfg['image']).convert('RGB'))[:, :, ::-1].copy()
box = P.get('box')
if box is None:   # fallback for a new photo: face box from mediapipe landmarks, grown to a head crop
    import mediapipe as mp
    from mediapipe.tasks import python as mpp
    from mediapipe.tasks.python import vision
    lm = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=os.environ['SCP_FACE_MODEL']), num_faces=1))
    r = lm.detect(mp.Image.create_from_file(cfg['image']))
    if not r.face_landmarks: raise SystemExit('no face found in the photo: give prep.box explicitly')
    L = np.array([[p.x * im.shape[1], p.y * im.shape[0]] for p in r.face_landmarks[0]])
    cx, top, bot = L[:, 0].mean(), L[:, 1].min(), L[:, 1].max(); fh = bot - top
    y0 = top - 0.75 * fh; y1 = bot + 0.55 * fh; w = (y1 - y0) * 0.95
    box = [int(max(cx - w / 2, 0)), int(max(y0, 0)), int(min(cx + w / 2, im.shape[1])), int(min(y1, im.shape[0]))]
    print('auto crop box', box)
X0, Y0, X1, Y1 = box
crop = im[Y0:Y1, X0:X1].copy(); cv2.imwrite(D + 'crop_src.png', crop); h, w = crop.shape[:2]
E = P.get('ellipses')
if E is None:      # relative to the crop: head, face, neck (fractions of w, h)
    E = {'head': [0.5 * w, 0.5 * h, 0.32 * w, 0.42 * h], 'face': [0.5 * w, 0.46 * h, 0.22 * w, 0.32 * h], 'neck': [0.55 * w, 0.88 * h, 0.19 * w, 0.085 * h]}
    E = {k: [v[0] + X0, v[1] + Y0, v[2], v[3]] for k, v in E.items()}     # stored in photo px like the explicit form
mask = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
def e(c, v): cv2.ellipse(mask, (int(c[0] - X0), int(c[1] - Y0)), (int(c[2]), int(c[3])), 0, 0, 360, v, -1)
e(E['head'], cv2.GC_PR_FGD); e(E['face'], cv2.GC_FGD); e(E['neck'], cv2.GC_FGD)
mask[:int(P.get('border_top', 40)), :] = cv2.GC_BGD
sc0 = P.get('side_cuts')      # photo-px columns outside which is sure background; null = none (the Cloud values only make sense for the Cloud photo)
if sc0:
    sl, sr = sc0; mask[:, :max(int(sl - X0), 0)] = cv2.GC_BGD; mask[:, max(int(sr - X0), 0):] = cv2.GC_BGD
if P.get('bg_seed', True):    # plain backgrounds: pixels close to the border colour (Lab, median of the top/side strips) are sure background (overrides the ellipses: they are wide at the jaw)
    lab = cv2.cvtColor(crop, cv2.COLOR_BGR2LAB).astype(np.float32)
    strips = np.concatenate([lab[:int(0.04 * h)].reshape(-1, 3), lab[:, :int(0.04 * w)].reshape(-1, 3), lab[:, w - int(0.04 * w):].reshape(-1, 3)])
    bgc = np.median(strips, 0); dist = np.linalg.norm(lab - bgc, axis=2)
    mask[dist < float(P.get('bg_seed_dist', 10))] = cv2.GC_BGD
    print('bg seed colour (Lab)', bgc.round(1), 'pixels', int((dist < float(P.get('bg_seed_dist', 10))).sum()))
bg = np.zeros((1, 65)); fg = np.zeros((1, 65)); cv2.grabCut(crop, mask, None, bg, fg, 10, cv2.GC_INIT_WITH_MASK)
m = np.where((mask == 1) | (mask == 3), 255, 0).astype(np.uint8)
n, l, s, _ = cv2.connectedComponentsWithStats(m); k = 1 + np.argmax(s[1:, 4]); m = np.where(l == k, 255, 0).astype(np.uint8)
m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE); m = np.zeros_like(m); cv2.drawContours(m, cnts, -1, 255, -1)
m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
cv2.imwrite(D + 'mask_raw.png', m)
vis = crop.copy(); vis[m == 0] = (vis[m == 0] * 0.25 + np.array([0, 0, 160]) * 0.75).astype(np.uint8); cv2.imwrite(D + 'mask_vis.png', vis)
# prep2: cut the chest/shoulders, composite on grey, square pad, 2x Lanczos to 1536
cr = P.get('cut_rows'); cr = cr if cr is not None else [int(0.9 * h), int(0.79 * h), int(0.79 * h)]
m[cr[0]:, :] = 0
sc = P.get('shoulder_cols'); sc = sc if sc is not None else [int(0.29 * w), int(0.81 * w)]
m[cr[1]:, :sc[0]] = 0; m[cr[1]:, sc[1]:] = 0
m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((7, 7), np.uint8))
n, l, s, _ = cv2.connectedComponentsWithStats(m); k = 1 + np.argmax(s[1:, 4]); m = np.where(l == k, 255, 0).astype(np.uint8); m = cv2.erode(m, np.ones((3, 3), np.uint8))
mf = cv2.GaussianBlur(m, (3, 3), 0).astype(np.float32) / 255
comp = (crop * mf[..., None] + np.array([128, 128, 128], np.float32) * (1 - mf[..., None])).astype(np.uint8)
S = max(h, w) + 60; sq = np.full((S, S, 3), 128, np.uint8); sqa = np.zeros((S, S), np.uint8); oy = (S - h) // 2; ox = (S - w) // 2
sq[oy:oy + h, ox:ox + w] = comp; sqa[oy:oy + h, ox:ox + w] = m
sq = cv2.resize(sq, (1536, 1536), interpolation=cv2.INTER_LANCZOS4); sqa = cv2.resize(sqa, (1536, 1536), interpolation=cv2.INTER_LANCZOS4)
cv2.imwrite(D + 'cutout_rgba.png', np.dstack([sq, sqa])); cv2.imwrite(D + 'input_1536.png', sq)
print('prep done', S, ox, oy, h, w)
