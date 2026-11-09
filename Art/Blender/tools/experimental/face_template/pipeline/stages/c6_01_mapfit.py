"""Fit the 2D map from wrapped-head front-ortho render px -> photo px (landmark pairs; affine + thin-plate residual).
Reads bake/wrap_lm.json (mediapipe on the wrapped render), target/landmarks.json (mediapipe on the photo). Writes bake/mapfit.npz + prints residuals.
Also exposes load_map() used by c2_02_texture.py and c2_07 overlay."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
from c6_common import *
import json
from scipy.interpolate import RBFInterpolator
OVAL = {33,133,362,263,243,463,10,338,297,332,284,251,389,356,454,323,361,288,397,365,379,378,400,377,148,176,149,150,136,172,58,132,93,234,127,162,21,54,103,67,109,
        226,113,225,224,446,342,445,444,247,30,29,27,28,56,190,467,466,259,260,257,258,286,414}
def pairs():
    a = json.load(open(B2 + 'wrap_lm.json')); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]
    ids = [i for i in range(468) if str(i) in a and i not in OVAL]   # skip face-oval and iris
    return ids, np.array([a[str(i)] for i in ids]), ph[ids]
def fit(smooth=3000.0):
    ids, src, dst = pairs()
    # trimmed affine
    A = np.c_[src, np.ones(len(src))]; keep = np.ones(len(src), bool)
    for _ in range(4):
        M, *_ = np.linalg.lstsq(A[keep], dst[keep], rcond=None); r = np.linalg.norm(A @ M - dst, axis=1)
        keep = r < max(3 * np.median(r), 4.0)
    res = dst - A @ M
    tps = RBFInterpolator(src[keep], res[keep], kernel='thin_plate_spline', smoothing=smooth, degree=1)
    return ids, src, dst, keep, M, tps
def make_map(M, tps, hull_src):
    from scipy.spatial import Delaunay, cKDTree
    kd = cKDTree(hull_src)
    def f(p):
        p = np.asarray(p, np.float64); base = np.c_[p, np.ones(len(p))] @ M
        d, _ = kd.query(p); fade = np.clip(1 - (d - 40) / 120.0, 0, 1)[:, None]   # residual fades out beyond ~40-160 px from any landmark
        return base + tps(p) * fade
    return f
if __name__ == '__main__':
    for s in (300.0, 3000.0, 30000.0):
        ids, src, dst, keep, M, tps = fit(s); f = make_map(M, tps, src[keep])
        e = np.linalg.norm(f(src) - dst, axis=1); ea = np.linalg.norm(np.c_[src, np.ones(len(src))] @ M - dst, axis=1)
        print(f'smooth {s}: pairs {len(ids)} kept {keep.sum()}  affine rms {np.sqrt((ea**2).mean()):.2f} med {np.median(ea):.2f} px | tps rms {np.sqrt((e**2).mean()):.2f} med {np.median(e):.2f} max {e.max():.1f}')
    print('affine M', M.tolist())
    ids, src, dst, keep, M, tps = fit(3000.0)
    np.savez(B2 + 'mapfit.npz', ids=ids, src=src, dst=dst, keep=keep, M=M, tps_y=tps.y, tps_d=tps.d, tps_smooth=3000.0)
