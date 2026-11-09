"""Shared face-proportion measures (numpy + cv2 + scipy; no mediapipe import here).
measure(img_bgr, lm) -> dict of dimensionless proportions from MediaPipe-478 landmarks (px, image frame) and the head silhouette (background = border colour).
The SAME function is applied to the SDXL prior portraits, to the target photo and to the fitted mesh's front render, so the detector's own bias cancels.
Every key in FEATURES has a plain-language label for the plausibility report: (name, low-phrase, high-phrase)."""
import numpy as np, cv2
from scipy import ndimage as ndi

FEATURES = {   # key: (what it is, phrase when LOW, phrase when HIGH)
    'face_len_over_width': ('face length (forehead point to chin) / cheek width', 'face too short and wide', 'face too long and narrow'),
    'eye_line_in_face': ('eye line height in the face (0 = forehead point, 1 = chin)', 'eyes too high in the face', 'eyes too low in the face'),
    'nose_base_in_face': ('nose base position in the face', 'nose base too high', 'nose base too low'),
    'mouth_in_face': ('mouth position in the face', 'mouth too high', 'mouth too low'),
    'chin_third': ('mouth-to-chin share of the face height', 'chin too short', 'chin too long'),
    'interocular_over_fw': ('outer eye corner distance / cheek width', 'eyes too close together', 'eyes too far apart'),
    'inner_eye_over_fw': ('inner eye corner distance / cheek width', 'inner eye gap too narrow', 'inner eye gap too wide'),
    'eye_width_over_io': ('eye opening width / outer eye distance', 'eyes too small', 'eyes too big'),
    'nose_width_over_fw': ('nose width / cheek width', 'nose too narrow', 'nose too wide'),
    'mouth_width_over_fw': ('mouth width / cheek width', 'mouth too narrow', 'mouth too wide'),
    'jaw_width_over_fw': ('jaw-angle width / cheek width', 'jaw too narrow (pointy chin)', 'jaw too wide (square)'),
    'lip_thick_over_mouthw': ('lip thickness / mouth width', 'lips too thin', 'lips too full'),
    'brow_gap_over_io': ('brow-to-eye gap / outer eye distance', 'brows too close to the eyes', 'brows too high above the eyes'),
    # silhouette (needs a clean, hair-free head silhouette)
    'head_aspect': ('head height (crown to chin) / dome width', 'head too short and round', 'head too long (football / egg shape)'),
    'head_aspect_cheek': ('head height / cheek width', 'head too short for its cheeks', 'head too tall for its cheeks'),
    'eye_line_in_head': ('eye line height in the whole head (0 = crown, 1 = chin; humans sit near 0.5)', 'eyes too high in the head (short face, tall skull)', 'eyes too low in the head (tall skull above the eyes)'),
    'skull_over_face': ('crown-to-brow height / brow-to-chin height', 'skull too low', 'skull too tall above the brows'),
    'crown_taper': ('skull width 10 % of the head height below the crown / width at 30 % (1 = flat top, small = pointed or gabled crown)', 'crown too pointed (egg / gable top)', 'crown too flat / boxy'),
    'dome_over_cheek': ('skull dome width / cheek width', 'skull too narrow for the cheeks', 'skull too wide for the cheeks'),
}
SIL_KEYS = ['crown_taper', 'head_aspect', 'head_aspect_cheek', 'eye_line_in_head', 'skull_over_face', 'dome_over_cheek']

def head_mask(img, seed_xy, thr=30):
    """Head+neck+shoulder silhouette of a portrait on a plain (possibly vignetted) backdrop: a quadratic surface per channel is fitted to the frame border (left/right 7 %, top 4 %),
    refitted without outliers; pixels farther than thr (sum |dBGR|) from the fitted backdrop are foreground; component through seed_xy, holes filled."""
    H, W = img.shape[:2]; sm = cv2.resize(img, (128, 128), interpolation=cv2.INTER_AREA).astype(np.float64)
    yy, xx = np.mgrid[0:128, 0:128]; X = (xx / 127.0 - 0.5).ravel(); Y = (yy / 127.0 - 0.5).ravel()
    A = np.stack([np.ones_like(X), X, Y, X * X, X * Y, Y * Y], 1); border = ((xx < 9) | (xx > 118) | (yy < 5)).ravel(); use = border.copy()
    for _ in range(3):
        coef = np.linalg.lstsq(A[use], sm.reshape(-1, 3)[use], rcond=None)[0]; res = np.abs(A @ coef - sm.reshape(-1, 3)).sum(1); use = border & (res < 24)
        if use.sum() < 200: break
    Xf = (np.arange(W) / (W - 1.0) - 0.5); Yf = (np.arange(H) / (H - 1.0) - 0.5); XX, YY = np.meshgrid(Xf, Yf)
    Af = np.stack([np.ones_like(XX), XX, YY, XX * XX, XX * YY, YY * YY], -1); bg = Af @ coef
    m = (np.abs(img.astype(np.float64) - bg).sum(2) > thr)
    m = ndi.binary_opening(m, iterations=2); lab, n = ndi.label(m)
    x, y = int(round(seed_xy[0])), int(round(seed_xy[1])); x = min(max(x, 0), W - 1); y = min(max(y, 0), H - 1)
    if lab[y, x] == 0:
        sz = ndi.sum(m, lab, range(1, n + 1))
        if not len(sz): return np.zeros((H, W), bool)
        return ndi.binary_fill_holes(lab == (1 + int(np.argmax(sz))))
    return ndi.binary_fill_holes(lab == lab[y, x])


def derolled(img, P):
    """rotate image + landmarks about the eye midpoint so the eye line is horizontal"""
    e1, e2 = P[33], P[263]; ang = np.degrees(np.arctan2(e2[1] - e1[1], e2[0] - e1[0])); c = (e1 + e2) / 2
    if abs(ang) < 0.3: return img, P, 0.0
    M = cv2.getRotationMatrix2D((float(c[0]), float(c[1])), ang, 1.0)
    img2 = cv2.warpAffine(img, M, (img.shape[1], img.shape[0]), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
    return img2, cv2.transform(P[None].astype(np.float32), M)[0].astype(float), float(ang)

def measure(img, lm, sil=True, top_margin=6, mask=None):
    """-> dict feature -> value (floats), plus 'ok_sil' (bool), 'geom' (px geometry used)"""
    P = np.asarray(lm, float)[:, :2]
    img, P, roll = derolled(img, P)
    d = lambda a, b: float(np.linalg.norm(P[a] - P[b]))
    fw = abs(P[454][0] - P[234][0]); io = d(33, 263); y10, y152 = P[10][1], P[152][1]; fh = y152 - y10
    eye_y = float(np.mean(P[[33, 263, 133, 362], 1]))
    o = {'face_len_over_width': fh / fw, 'eye_line_in_face': (eye_y - y10) / fh, 'nose_base_in_face': (P[2][1] - y10) / fh, 'mouth_in_face': (P[13][1] - y10) / fh,
         'chin_third': (y152 - P[13][1]) / fh, 'interocular_over_fw': io / fw, 'inner_eye_over_fw': d(133, 362) / fw, 'eye_width_over_io': (d(33, 133) + d(263, 362)) / 2 / io,
         'nose_width_over_fw': d(129, 358) / fw, 'mouth_width_over_fw': d(61, 291) / fw, 'jaw_width_over_fw': d(172, 397) / fw, 'lip_thick_over_mouthw': d(0, 17) / d(61, 291),
         'brow_gap_over_io': ((abs(P[105][1] - P[159][1]) + abs(P[334][1] - P[386][1])) / 2) / io}
    g = dict(roll_deg=roll, fw=fw, io=io, y10=float(y10), y152=float(y152), eye_y=eye_y, ybrow=float(P[[105, 334], 1].mean()))
    o['ok_sil'] = False
    if sil:
        if mask is not None:   # a given head mask (photo frame, e.g. the pipeline's mask_vis head): rotate it with the image
            m = mask
            if abs(roll) >= 0.3: m = cv2.warpAffine(mask.astype(np.uint8), cv2.getRotationMatrix2D((float((lm[33][0] + lm[263][0]) / 2), float((lm[33][1] + lm[263][1]) / 2)), roll, 1.0), (mask.shape[1], mask.shape[0]), flags=cv2.INTER_NEAREST).astype(bool)
        else: m = head_mask(img, P[168])
        rows = np.where(m.sum(1) >= 3)[0]
        if len(rows):
            top = int(rows.min()); ybrow = g['ybrow']; yd = int(round(top + 0.55 * (ybrow - top))); xs = np.where(m[yd])[0]
            wd = float(xs.max() - xs.min()) if len(xs) else float('nan'); Hh = y152 - top
            # hair / clipped-crown guard: crown must sit clearly above the forehead point and not touch the image top; dome must not be absurdly wide
            ok = (top > top_margin) and (y10 - top > 0.12 * fh) and (y10 - top < 0.9 * fh) and np.isfinite(wd) and (0.8 * fw < wd < 1.6 * fw)
            g.update(top=top, dome_row=yd, dome_width=wd, head_height=float(Hh))
            def _w(f):
                xs_ = np.where(m[int(round(top + f * Hh))])[0]; return float(xs_.max() - xs_.min()) if len(xs_) else float('nan')
            w10, w30 = _w(0.10), _w(0.30)
            if ok:
                o.update(crown_taper=w10 / w30, head_aspect=Hh / wd, head_aspect_cheek=Hh / fw, eye_line_in_head=(eye_y - top) / Hh, skull_over_face=(ybrow - top) / (y152 - ybrow), dome_over_cheek=wd / fw); o['ok_sil'] = True
    o['geom'] = g
    return o
