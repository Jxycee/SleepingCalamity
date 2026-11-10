# Silhouette score of head + hair against the reference crops (trellis venv python: numpy + cv2).
#   python hd_sil.py <head.npz> <hair.npz | hair dumped by hd_dump.py> <hull_dir> [out_overlay.png] [--zmin 1.68]
# Uses hull_prep.py's masks and calibration (px = cx + sign*s*h, py = eye_py - sz*(z - eye_z); front h = x, side h = y, back h = x with
# sign -1). Scores only rows above zmin (hair dominates there; the stylised reference face differs from TobinBase below).
# IoU, 'over' (ours outside the reference, fraction of ours) and 'miss' (reference not covered, fraction of the reference) per view.
import sys, os, json, numpy as np, cv2
AX = dict(front=0, side=1, back=0)
def load_cal(hull):
    cal = json.load(open(os.path.join(hull, 'hull_cal.json'))); M = {v: np.load(os.path.join(hull, f'mask_{v}.npy')).astype(bool) for v in AX}
    return cal, M
def raster(V, F, view, cal, shape):
    c = cal[view]; s = c['s']; sz = c.get('sz', s)
    px = c['cx'] + c['sign'] * s * V[:, AX[view]]; py = c['eye_py'] - sz * (V[:, 2] - cal['eye_z'])
    P = np.stack([px, py], 1); m = np.zeros(shape, np.uint8)
    tri = np.round(P[F] * 4).astype(np.int32)                       # 2-bit subpixel
    cv2.fillPoly(m, list(tri), 1, lineType=cv2.LINE_8, shift=2)
    return m.astype(bool)
def score(meshes, hull, zmin=1.68, overlay=None):
    cal, M = load_cal(hull); out = {}; tiles = []
    for v in AX:
        ref = M[v]; ours = np.zeros_like(ref)
        for V, F in meshes: ours |= raster(V, F, v, cal, ref.shape)
        c = cal[v]; sz = c.get('sz', c['s']); row = int(c['eye_py'] - sz * (zmin - cal['eye_z']))
        reg = np.zeros_like(ref); reg[:max(row, 0)] = True
        r, o = ref & reg, ours & reg; inter = (r & o).sum(); uni = (r | o).sum()
        out[v] = dict(iou=inter / max(uni, 1), over=(o & ~r).sum() / max(o.sum(), 1), miss=(r & ~o).sum() / max(r.sum(), 1))
        if overlay:
            im = np.full(ref.shape + (3,), 255, np.uint8); im[r & o] = (150, 150, 150); im[o & ~r] = (40, 40, 230); im[r & ~o] = (230, 120, 30)
            cv2.line(im, (0, row), (im.shape[1], row), (0, 160, 0), 1); tiles.append(cv2.resize(im, (int(im.shape[1] * 400 / im.shape[0]), 400)))
    # face-framing band: front view between chin and zmin, outside the face width (|x| > 0.06 m), so locks hanging to the cheeks count
    # without the stylised-reference vs TobinBase face mismatch
    c = cal['front']; sz = c.get('sz', c['s']); ref = M['front']; ours = np.zeros_like(ref)
    for V, F in meshes: ours |= raster(V, F, 'front', cal, ref.shape)
    r0, r1 = int(c['eye_py'] - sz * (zmin - cal['eye_z'])), int(c['eye_py'] - sz * (1.60 - cal['eye_z']))
    cols = np.abs(np.arange(ref.shape[1]) - c['cx']) > c['s'] * 0.06
    band = np.zeros_like(ref); band[max(r0, 0):max(r1, 0)] = True; band &= cols[None, :]
    rb, ob = ref & band, ours & band
    out['frame'] = dict(iou=(rb & ob).sum() / max((rb | ob).sum(), 1), over=(ob & ~rb).sum() / max(ob.sum(), 1), miss=(rb & ~ob).sum() / max(rb.sum(), 1))
    out['mean_iou'] = float(np.mean([out[v]['iou'] for v in AX]))
    if overlay: cv2.imwrite(overlay, np.hstack(tiles))
    return out
def load_mesh(npz, name):
    d = np.load(npz); return d[name + '_v'].astype(float), d[name + '_f']
if __name__ == '__main__':
    a = sys.argv[1:]; zmin = float(a[a.index('--zmin') + 1]) if '--zmin' in a else 1.68
    head = load_mesh(a[0], 'AvatarHead'); d = np.load(a[1]); key = [k[:-2] for k in d.files if k.endswith('_v')][0]
    hair = (d[key + '_v'].astype(float), d[key + '_f'])
    r = score([head, hair], a[2], zmin, a[3] if len(a) > 3 and not a[3].startswith('--') else None)
    print('SIL', ' '.join(f"{v}: iou {r[v]['iou']:.3f} over {r[v]['over']:.3f} miss {r[v]['miss']:.3f}" for v in list(AX) + ['frame']), f"| mean {r['mean_iou']:.3f}")
