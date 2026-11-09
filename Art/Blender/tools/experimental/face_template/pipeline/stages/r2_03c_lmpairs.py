# landmark pairs for the RBF pre-warp: template (slider result) landmark position -> target landmark (template frame). Face-oval contour points excluded.
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import numpy as np, json
from r2_common import *
t = Tpl(); al = json.load(open(FIT + 'align.json')); ids = al['lm_ids']; keep = np.array(al['kept'])
s, R, tr = al['scale'], np.array(al['R']), np.array(al['t'])
ht = json.load(open(FIT + 'tgt_hits.json')); hp = json.load(open(FIT + 'tpl_hits.json'))
oval = {33,133,362,263,243,463,10,338,297,332,284,251,389,356,454,323,361,288,397,365,379,378,400,377,148,176,149,150,136,172,58,132,93,234,127,162,21,54,103,67,109,
        226,113,225,224,446,342,445,444,247,30,29,27,28,56,190,467,466,259,260,257,258,286,414}
sel = [i for i, k in enumerate(ids) if keep[i] and int(k) not in oval]
Ps = np.load(FIT + 'slider_verts.npy').astype(np.float64)
tp = np.array([hp[ids[i]] for i in sel]); tg = np.array([s * np.array(ht[ids[i]]) @ R.T + tr for i in sel])
nn = cKDTree(t.basis).query(tp)[1]; sp_ = tp + (Ps - t.basis)[nn]
np.savez(FIT + 'tpl_to_tgt_landmarks.npz', slider_pts=sp_, target_pts=tg, ids=np.array([int(ids[i]) for i in sel]))
r = np.linalg.norm(tg - sp_, axis=1) * 1e3
print('pairs', len(sel), 'offset mm mean/med/max', r.mean(), np.median(r), r.max())
dz = (tg - sp_)[:, 2] * 1e3; print('mouth ids dz mm', {int(ids[i]): round(dz[k], 1) for k, i in enumerate(sel) if int(ids[i]) in (61, 291, 0, 17, 13, 152, 2, 1, 168, 105, 334)})

# eye-block translation (x,z) from the four eye-corner landmarks, per eye (mediapipe on both renders; y left to the TPS field)
eye = {}
for side, cid in (('R', (33, 133)), ('L', (362, 263))):
    ii = [ids.index(str(c)) for c in cid]
    tp_ = np.array([hp[ids[i]] for i in ii]); tg_ = np.array([s * np.array(ht[ids[i]]) @ R.T + tr for i in ii])
    nn_ = cKDTree(t.basis).query(tp_)[1]; spp = tp_ + (Ps - t.basis)[nn_]
    eye[side] = (tg_ - spp).mean(0); print('eye', side, 'corner-landmark shift mm (x,y,z)', np.round(eye[side] * 1e3, 2))
np.savez(FIT + 'eye_shift.npz', L=eye['L'], R=eye['R'])
