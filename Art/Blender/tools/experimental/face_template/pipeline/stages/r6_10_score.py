"""Round-3 match score vs the photo (facekeys env: numpy scipy cv2 mediapipe PIL).
usage: r6_10_score.py label=front_render.png [label=...]   (Workbench front-ortho renders from rr3: rn_<tag>.png) [--tex texture.json]
 1. front-view feature ratios (11, same as r2_06): mean / max |error| %
 2. silhouette IoU of the render vs the photo head mask after the eye-aligned similarity (4 eye corners), rows above the chin landmark (head + jaw; neck/sweater excluded), plus skull-only IoU (rows above the brows)
 3. mean Lab skin difference: texture patches (cheeks, forehead; from bake6/texture.json) vs the photo's de-lit mean skin tone (deltaE76)
writes fit6/score.json"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json, numpy as np, cv2
from PIL import Image
from scipy import ndimage as ndi
import mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
D2 = _P_RUN; TGT = _P_TGT; F3 = _P_FIT
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1)
lm_ = vision.FaceLandmarker.create_from_options(opt)
def get(p):
    im = mp.Image.create_from_file(p); r = lm_.detect(im)
    return np.array([[l.x * im.width, l.y * im.height] for l in r.face_landmarks[0]]) if r.face_landmarks else None
def Rt(P):
    d = lambda a, b: np.linalg.norm(P[a] - P[b]); io = d(33, 263); eyec = (P[33] + P[263]) / 2
    return {'nose_width': d(129, 358) / io, 'mouth_width': d(61, 291) / io, 'face_width': d(234, 454) / io, 'jaw_width': d(172, 397) / io,
            'brow_eye_gap': ((abs(P[105][1] - P[159][1]) + abs(P[334][1] - P[386][1])) / 2) / io, 'eye_to_nose_base': (P[2][1] - eyec[1]) / io,
            'nose_to_mouth': (P[13][1] - P[2][1]) / io, 'mouth_to_chin': (P[152][1] - P[13][1]) / io, 'eye_to_chin': (P[152][1] - eyec[1]) / io,
            'forehead_eye': (eyec[1] - P[10][1]) / io, 'lip_thickness': d(0, 17) / io}
ph_img = cv2.imread(TGT + 'crop_src.png'); H, W = ph_img.shape[:2]
ph = get(TGT + 'crop_src.png'); a = Rt(ph)
mv = np.array(Image.open(TGT + 'mask_vis.png').convert('RGB')).astype(int)
head = ((mv[..., 0] - mv[..., 1]) <= 50); head = ndi.binary_opening(head, iterations=2); lab, n = ndi.label(head); head = ndi.binary_fill_holes(lab == lab[int(ph[168, 1]), int(ph[168, 0])])
ychin = int(ph[152, 1]); ybrow = int(ph[[105, 334], 1].mean())
out = {}
for arg in [x for x in sys.argv[1:] if '=' in x and not x.startswith('--')]:
    lab_, p = arg.split('=', 1); P = get(p)
    if P is None: print(lab_, 'NO FACE'); continue
    b = Rt(P); err = {k: 100 * (b[k] / a[k] - 1) for k in a}
    # detector-noise robustness: the same render re-framed by +-1 % scale / shift (5 framings incl. nominal); the ratios are scale/shift invariant, so the spread is pure detector noise.
    # The reported (thresholded) ratio MAE / worst are the MEDIANS over the framings; nominal, min and max are kept alongside.
    jit = [(1.0, 0, 0), (1.01, 0, 0), (0.99, 0, 0), (1.0, 0.01, -0.01), (1.0, -0.01, 0.01)]; jm = []; jw = []; jerr = []; src_ = cv2.imread(p); h0, w0 = src_.shape[:2]; bgc = tuple(int(x) for x in src_[2, 2])
    for js, jx, jy in jit:
        if (js, jx, jy) == (1.0, 0, 0): Pj = P
        else:
            Mj = np.float32([[js, 0, (1 - js) * w0 / 2 + jx * w0], [0, js, (1 - js) * h0 / 2 + jy * h0]]); tmp = p[:-4] + '_jit.png'
            cv2.imwrite(tmp, cv2.warpAffine(src_, Mj, (w0, h0), flags=cv2.INTER_LANCZOS4, borderValue=bgc)); Pj = get(tmp)
            if Pj is None: continue
        bj = Rt(Pj); ej = {k: 100 * (bj[k] / a[k] - 1) for k in a}; jerr.append(ej); jm.append(float(np.mean(np.abs(list(ej.values()))))); jw.append(float(np.max(np.abs(list(ej.values())))))
    EC = [33, 263, 133, 362]
    M, _ = cv2.estimateAffinePartial2D(P[EC].astype(np.float32), ph[EC].astype(np.float32))
    img = cv2.imread(p); bg = img[2, 2].astype(int); m = (np.abs(img.astype(int) - bg).sum(2) > 25).astype(np.uint8)
    m = ndi.binary_opening(m, iterations=2).astype(np.uint8); w = cv2.warpAffine(m, M, (W, H), flags=cv2.INTER_NEAREST).astype(bool)
    yy = np.arange(H)[:, None]
    # Neck / collar / prop clip (round 11): below the jaw-angle row both silhouettes are cut to their OWN dilated face-oval polygon (photo: photo landmarks; render: render landmarks through M), so a mask that
    # keeps (or drops) the neck, a collar or a sword guard no longer decides the jaw rows. Above that row (skull, ears) nothing is clipped. The old unclipped numbers stay in the output.
    OVAL = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109]
    def oval_mask(pts):
        m_ = np.zeros((H, W), np.uint8); cv2.fillPoly(m_, [np.round(pts[OVAL]).astype(np.int32)], 1)
        k_ = int(round(0.05 * abs(pts[454][0] - pts[234][0]))) * 2 + 1
        return cv2.dilate(m_, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k_, k_))).astype(bool)
    PM = cv2.transform(P[None].astype(np.float32), M)[0]
    yjaw = int(round((ph[172][1] + ph[397][1]) / 2)); low = yy >= yjaw
    head_c = np.where(low, head & oval_mask(ph), head); w_c = np.where(low, w & oval_mask(PM), w)
    def centred(rowmax, A=None, B=None):
        A = w if A is None else A; B = head if B is None else B
        # yaw-compensated: shift each render row so its midpoint equals the photo's midpoint (removes the photo's yaw offset of the skull, keeps the width profile)
        inter = uni = 0
        for y in range(40, rowmax):
            ra = np.where(A[y])[0]; rb = np.where(B[y])[0]
            if len(ra) < 3 or len(rb) < 3: continue
            sh = int(round((rb.min() + rb.max()) / 2 - (ra.min() + ra.max()) / 2)); r2_ = np.roll(A[y], sh)
            inter += (r2_ & B[y]).sum(); uni += (r2_ | B[y]).sum()
        return float(inter / max(uni, 1))
    def iou(rowmax, A=None, B=None):
        A = w if A is None else A; B = head if B is None else B
        sel = yy < rowmax; A_ = A & sel; B_ = B & sel; return float((A_ & B_).sum() / max((A_ | B_).sum(), 1))
    bands = {}
    for y0 in range(40, ychin, 60):
        A_ = w[y0:y0 + 60]; B_ = head[y0:y0 + 60]; xa = np.where(A_.any(0))[0]; xb = np.where(B_.any(0))[0]
        bands[y0] = dict(iou=round(float((A_ & B_).sum() / max((A_ | B_).sum(), 1)), 3), width_render=int(A_.sum(1).mean()), width_photo=int(B_.sum(1).mean()), left_dx=int(xa.min() - xb.min()) if len(xa) and len(xb) else None, right_dx=int(xa.max() - xb.max()) if len(xa) and len(xb) else None)
    # head aspect (round 14): crown-to-chin height over dome width, both in the photo frame (render warped through M). Parts: crown->brow-line (skull), brow-line->chin (face). Dome width = mask extent 15 px above the brow line.
    def _asp(mk, ych, Pm=None):
        rows = np.where(mk.sum(1) >= 3)[0]; top = int(rows.min()); h = ych - top; yd = int(round(top + 0.55 * (ybrow - top))); xs = np.where(mk[yd])[0]; wd = float(xs.max() - xs.min()) if len(xs) else float('nan')   # dome width: mid-skull row (above the ears, whose height differs between photo and template)
        cw = float(abs(Pm[454][0] - Pm[234][0])) if Pm is not None else float('nan')      # bizygomatic width from the landmarks (cheek width)
        return dict(top=top, height=float(h), dome_width=wd, cheek_width=cw, aspect=float(h / wd), aspect_cheek=float(h / cw), skull_h=float(ybrow - top), face_h=float(ych - ybrow))
    ya = _asp(head, int(round(ph[152][1])), ph); yr = _asp(w, int(round(PM[152][1])), PM)
    asp = dict(photo=ya, render=yr, err_pct=100 * (yr['aspect'] / ya['aspect'] - 1), err_cheek_pct=100 * (yr['aspect_cheek'] / ya['aspect_cheek'] - 1), height_err_pct=100 * (yr['height'] / ya['height'] - 1), width_err_pct=100 * (yr['dome_width'] / ya['dome_width'] - 1), skull_h_err_pct=100 * (yr['skull_h'] / ya['skull_h'] - 1), face_h_err_pct=100 * (yr['face_h'] / ya['face_h'] - 1))
    print('   head aspect (height / mid-skull width; cheek-width variant %+.1f%%): photo %.3f render %.3f err %+.1f%% (height %+.1f%%, dome width %+.1f%%, skull h %+.1f%%, face h %+.1f%%)' % (asp['err_cheek_pct'], ya['aspect'], yr['aspect'], asp['err_pct'], asp['height_err_pct'], asp['width_err_pct'], asp['skull_h_err_pct'], asp['face_h_err_pct']))
    mae_nom = float(np.mean(np.abs(list(err.values())))); worst_nom = float(np.max(np.abs(list(err.values()))))
    err_med = {k: float(np.median([e[k] for e in jerr])) for k in a}
    out[lab_] = dict(head_aspect=asp, bands=bands, ratios=b, err_pct=err, err_pct_median=err_med, mae_pct=float(np.median(jm)), max_abs_pct=float(np.median(jw)),
                     mae_nominal=mae_nom, max_abs_nominal=worst_nom, mae_min=float(min(jm)), mae_max=float(max(jm)), max_abs_min=float(min(jw)), max_abs_max=float(max(jw)), jitter_mae=jm, jitter_worst=jw, jitter_n=len(jm),
                     iou_head_jaw=iou(ychin), iou_skull=iou(ybrow - 20), iou_head_jaw_rowcentred=centred(ychin), iou_skull_rowcentred=centred(ybrow - 20),
                     iou_head_jaw_oval=iou(ychin, w_c, head_c), iou_head_jaw_rowcentred_oval=centred(ychin, w_c, head_c))
    print('   jitter x%d: MAE median %.2f (min %.2f max %.2f; nominal %.2f) | worst median %.2f (min %.2f max %.2f; nominal %.2f) | median err %s' % (len(jm), np.median(jm), min(jm), max(jm), mae_nom, np.median(jw), min(jw), max(jw), worst_nom, {k: round(v, 1) for k, v in err_med.items()}))
    print('%-8s ratio MAE %.2f%% max %.1f%% (%s) | silhouette IoU head+jaw %.3f skull %.3f (row-centred %.3f / %.3f)' % (lab_, out[lab_]['mae_pct'], out[lab_]['max_abs_pct'], max(err_med, key=lambda k: abs(err_med[k])), out[lab_]['iou_head_jaw'], out[lab_]['iou_skull'], out[lab_]['iou_head_jaw_rowcentred'], out[lab_]['iou_skull_rowcentred']))
    print('   bands(y: iou, left_dx,right_dx px):', {k: (v['iou'], v['left_dx'], v['right_dx']) for k, v in bands.items()})
    print('   worst (median over framings):', ', '.join('%s %+.1f%%' % (k, err_med[k]) for k in sorted(err_med, key=lambda k: -abs(err_med[k]))[:4]))
if '--tex' in sys.argv:
    tj = json.load(open(sys.argv[sys.argv.index('--tex') + 1])); tone = np.array(tj['photo_fit_mean_Lab']); rep = tj['skin_Lab_report']
    de = {k: float(np.linalg.norm(np.array(v['texture_Lab']) - tone)) for k, v in rep.items()}
    out['skin_Lab'] = dict(photo_delit_tone=tone.tolist(), texture_patches={k: v['texture_Lab'] for k, v in rep.items()}, deltaE76_vs_tone=de, mean_deltaE76=float(np.mean(list(de.values()))))
    print('skin Lab: photo de-lit tone', tone.round(1), 'texture patches', {k: v['texture_Lab'] for k, v in rep.items()}, 'mean dE76 %.2f' % out['skin_Lab']['mean_deltaE76'])
json.dump(out, open(F3 + 'score.json', 'w'), indent=1)
