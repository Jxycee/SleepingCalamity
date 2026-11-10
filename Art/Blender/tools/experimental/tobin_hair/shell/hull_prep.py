# Reference silhouettes -> hull masks in the TobinBase head frame (run with the trellis venv python: numpy + cv2).
#   python hull_prep.py <refs_dir> <out_dir>
# Foreground (not white) masks of the front / side / back reference crops, plus a calibration that maps head-frame world points to
# mask pixels. Calibration = face landmarks: front eye line y 338 px, chin y 515 px, centre x 362 px; side eye y 160, chin y 285,
# nose tip x 155; TobinBase eyes z 1.720, chin z 1.600, nose tip y -0.146 (back crop shares the side crop's scale, centred on the hair).
# The stylised reference face is narrower than the MetaPerson head, so the hull is only a soft target (see build_shell_hair.py hull=).
import sys, os, json, numpy as np, cv2
refs, out = sys.argv[1], sys.argv[2]; os.makedirs(out, exist_ok=True)
EYE_Z, CHIN_Z, NOSE_Y = 1.720, 1.600, -0.146
cal = {}
def fg(name):
    im = cv2.imread(os.path.join(refs, f'crop_{name}.png')).astype(np.int16)
    m = (im.min(axis=2) < 236).astype(np.uint8)                     # anything not white background
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m)            # keep the big blob (character), drop specks
    k = 1 + int(np.argmax(st[1:, cv2.CC_STAT_AREA])); m = (lab == k).astype(np.uint8)
    return m
sf = (515 - 338) / (EYE_Z - CHIN_Z); ss = (285 - 160) / (EYE_Z - CHIN_Z)
cal['front'] = dict(axis='x', sign=1.0, cx=362.0, eye_py=338.0, s=sf)          # px = cx + s*x ; py = eye_py - s*(z-EYE_Z)
# stylised reference proportions differ per view, so side/back get separate horizontal (s) and vertical (sz) scales:
# vertical from eye line to the hair mass top (= TobinBase skull top 1.835 + 3.5 cm), horizontal from head depth / width incl. hair.
TOP_Z = 1.87
sz_side = (160 - 40) / (TOP_Z - EYE_Z); s_side = (408 - 165) / (0.085 + 0.025 + 0.120)      # forehead px 165 ~ y -0.120, hair back px 408 ~ y 0.110
cal['side'] = dict(axis='y', sign=1.0, cx=165.0 + s_side * 0.120, eye_py=160.0, s=s_side, sz=sz_side)  # camera at +x: image right = +y
cal['back'] = dict(axis='x', sign=-1.0, cx=294.0, eye_py=20.0 + sz_side * (TOP_Z - EYE_Z), s=(410 - 178) / 0.22, sz=sz_side)  # camera at +y: image right = -x
cal['eye_z'] = EYE_Z
for n in ('front', 'side', 'back'):
    m = fg(n); np.save(os.path.join(out, f'mask_{n}.npy'), m); cv2.imwrite(os.path.join(out, f'mask_{n}.png'), m * 255)
json.dump(cal, open(os.path.join(out, 'hull_cal.json'), 'w'), indent=1)
print('HULL masks + calibration ->', out, {k: round(v['s'], 1) for k, v in cal.items() if isinstance(v, dict)})
# outline contours (pixels) for the guide tip targets: outer contour of each mask, every ~8 px of arc
for n in ('front', 'side', 'back'):
    m = np.load(os.path.join(out, f'mask_{n}.npy')).astype(np.uint8)
    cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); c = max(cs, key=len)[:, 0, :]
    keep = c[::8].tolist(); json.dump(keep, open(os.path.join(out, f'contour_{n}.json'), 'w'))
    print('CONTOUR', n, len(keep))
