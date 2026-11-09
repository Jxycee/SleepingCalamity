"""Round 2 step 3: recolour Tobin's iris to the photo's cyan (HSV on the iris disc only; pupil and limbal ring untouched). venv python.
writes bake/Eye_BaseColor_char.png, bake/eyes.json"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
from c6_common import *
import json, cv2
from PIL import Image
im = np.array(Image.open(TEX + 'Eye_BaseColor.png').convert('RGB')); Hh, Ww = im.shape[:2]
hsv = cv2.cvtColor(im, cv2.COLOR_RGB2HSV).astype(np.float32)
dark = hsv[..., 2] < 40; yy, xx = np.mgrid[:Hh, :Ww]
core = dark & ((xx - 256) ** 2 + (yy - 310) ** 2 < 60 ** 2)
cy, cx_ = (yy[core].mean(), xx[core].mean()); pr = np.sqrt(core.sum() / np.pi)
print('pupil centre', cx_, cy, 'radius', pr)
rr = np.hypot(xx - cx_, yy - cy)
# photo iris colour
ph = np.array(Image.open(TGT + 'crop_src.png').convert('RGB')); lm = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]
ph_hsv = cv2.cvtColor(ph, cv2.COLOR_RGB2HSV).astype(np.float32); cols = []
for c, ring in ((468, (469, 470, 471, 472)), (473, (474, 475, 476, 477))):
    r = np.mean([np.linalg.norm(lm[k] - lm[c]) for k in ring]); y_, x_ = np.mgrid[:ph.shape[0], :ph.shape[1]]
    m = (np.hypot(x_ - lm[c, 0], y_ - lm[c, 1]) < 0.85 * r) & (ph_hsv[..., 1] > 90) & (ph_hsv[..., 2] > 120) & (np.abs(ph_hsv[..., 0] - 90) < 15)
    print('photo iris', c, 'r px', round(r, 1), 'cyan px', int(m.sum())); cols.append(ph_hsv[m])
# PIPELINE: iris colour from cfg.iris_colour (env SCP_IRIS): null = keep Tobin's iris, "auto" = photo (cyan window first, any saturated hue as fallback), [r,g,b] or "#rrggbb" = explicit
_ir = json.loads(_os.environ.get('SCP_IRIS', '"auto"'))
if _ir is None:
    Image.fromarray(im).save(B2 + 'Eye_BaseColor_char.png'); Image.fromarray(np.zeros_like(im)).save(B2 + 'Eye_Emission_char.png')
    json.dump(dict(recolour=False), open(B2 + 'eyes.json', 'w')); print('iris recolour off'); raise SystemExit(0)
if isinstance(_ir, str) and _ir.startswith('#'): _ir = [int(_ir[i:i + 2], 16) for i in (1, 3, 5)]
if isinstance(_ir, list):
    H_, S_, V_ = cv2.cvtColor(np.uint8([[_ir]]), cv2.COLOR_RGB2HSV)[0, 0].astype(float); Vhi = V_
else:
    if sum(len(x) for x in cols) < 50:   # no cyan iris in the photo: take the saturated pixels of the iris discs whatever their hue
        cols = []
        for c, ring in ((468, (469, 470, 471, 472)), (473, (474, 475, 476, 477))):
            r = np.mean([np.linalg.norm(lm[k] - lm[c]) for k in ring]); y_, x_ = np.mgrid[:ph.shape[0], :ph.shape[1]]
            mm = (np.hypot(x_ - lm[c, 0], y_ - lm[c, 1]) < 0.85 * r) & (ph_hsv[..., 1] > 60) & (ph_hsv[..., 2] > 50); cols.append(ph_hsv[mm])
        print('auto iris: no cyan found, using saturated iris pixels', sum(len(x) for x in cols))
    cols = np.concatenate(cols); H_, S_, V_ = np.median(cols, 0); Vhi = np.percentile(cols[:, 2], 85)
print('photo iris median HSV(cv)', H_, S_, V_, 'rgb', cv2.cvtColor(np.uint8([[[H_, S_, V_]]]), cv2.COLOR_HSV2RGB)[0, 0])
# iris disc mask: from pupil edge to just inside the limbal ring (Tobin iris radius ~ 1.85 x pupil)
r_in, r_out = pr + 2, 1.85 * pr
m = np.clip((rr - r_in) / 3, 0, 1) * np.clip((r_out - rr) / 4, 0, 1)
# keep Tobin's radial fibre detail: modulate value around its median
V0 = hsv[..., 2]; med = np.median(V0[m > 0.9]); mod = np.clip(V0 / max(med, 1), 0.55, 1.5)
newV = np.clip(V_ * (0.55 + 0.45 * mod) * 1.1, 0, 255); newS = np.full_like(V0, S_)
# the eyeball UV actually samples the brown fibre STRIP at the top of the texture as its iris: recolour that too (rows ~10-110)
strip = np.clip((yy - 9) / 3, 0, 1) * np.clip((113 - yy) / 3, 0, 1); strip = strip * (hsv[..., 1] > 60)
ms = float(np.median(V0[strip > 0.9])); modS = np.clip(V0 / max(ms, 1), 0.5, 1.7)
V_strip = np.clip(V_ * (0.45 + 0.6 * modS) * 1.15, 0, 255)
m = np.maximum(m, strip); newV = np.where(strip > 0.5, V_strip, newV)
# slight radial glow: brighter toward the pupil (the photo irises glow)
glow = 1 + 0.25 * np.clip(1 - (rr - r_in) / (r_out - r_in), 0, 1); newV = np.clip(newV * glow, 0, 255)
new = hsv.copy(); new[..., 0] = H_; new[..., 1] = newS; new[..., 2] = newV
rgb_new = cv2.cvtColor(new.astype(np.uint8), cv2.COLOR_HSV2RGB).astype(np.float32)
out = (im.astype(np.float32) * (1 - m[..., None]) + rgb_new * m[..., None]).astype(np.uint8)
Image.fromarray(out).save(B2 + 'Eye_BaseColor_char.png')
Image.fromarray(np.concatenate([im, out], 1)).save(B2 + '_eye_cmp.png')
json.dump(dict(pupil_centre=[float(cx_), float(cy)], pupil_r=float(pr), iris_hsv_cv=[float(H_), float(S_), float(V_)]), open(B2 + 'eyes.json', 'w'))
em = (rgb_new * m[..., None]).astype(np.uint8); Image.fromarray(em).save(B2 + 'Eye_Emission_char.png')
