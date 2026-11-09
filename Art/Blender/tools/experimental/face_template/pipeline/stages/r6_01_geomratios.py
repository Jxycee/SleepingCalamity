"""Front-plane (x,z) ratios from 3D landmark vertices: template / sliders / round-2 wrap / target(aligned), vs mediapipe-on-render. Same 11 ratios as r2_06."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json, sys
from r6_common import *
t = Tpl(); al = json.load(open(FIT + 'align.json')); s, R, tr = al['scale'], np.array(al['R']), np.array(al['t'])
ht = json.load(open(FIT + 'tgt_hits.json')); hp = json.load(open(FIT + 'tpl_hits.json'))
ks = [int(k) for k in hp if hp[k] and ht.get(k)]
tp = np.array([hp[str(k)] for k in ks]); tg = np.array([s * np.array(ht[str(k)]) @ R.T + tr for k in ks])
nn = cKDTree(t.basis).query(tp)[1]
def pos(P): return {k: np.array([P[nn[i]][0], -P[nn[i]][2]]) if P is not None else None for i, k in enumerate(ks)}
def ratios(L):
    d = lambda a, b: np.linalg.norm(L[a] - L[b]); io = d(33, 263); eyec = (L[33] + L[263]) / 2
    return {'nose_width': d(129, 358) / io, 'mouth_width': d(61, 291) / io, 'face_width': d(234, 454) / io, 'jaw_width': d(172, 397) / io,
            'brow_eye_gap': ((abs(L[105][1] - L[159][1]) + abs(L[334][1] - L[386][1])) / 2) / io if 159 in L else np.nan, 'eye_to_nose_base': (L[2][1] - eyec[1]) / io,
            'nose_to_mouth': (L[13][1] - L[2][1]) / io, 'mouth_to_chin': (L[152][1] - L[13][1]) / io, 'eye_to_chin': (L[152][1] - eyec[1]) / io,
            'forehead_eye': (eyec[1] - L[10][1]) / io, 'lip_thickness': d(0, 17) / io}
variants = {'template': t.basis, 'sliders': np.load(FIT + 'slider_verts_noeye.npy').astype(float), 'r2wrap': np.load(FIT2 + 'wrap_r3_basis.npy').astype(float)}
if len(sys.argv) > 1: variants['wrap3'] = np.load(sys.argv[1]).astype(float)
out = {}
for n, P in variants.items(): out[n] = ratios(pos(P))
Lt = {k: np.array([tg[i][0], -tg[i][2]]) for i, k in enumerate(ks)}; out['target_3d'] = ratios(Lt)
ph = json.load(open(FIT2 + 'ratios_final.json')); out['photo(mp)'] = ph['photo']; out['clay(mp)'] = ph['target_clay']
print('%-18s' % 'ratio' + ''.join('%11s' % k for k in out))
for k in out['template']: print('%-18s' % k + ''.join('%11.3f' % out[n].get(k, np.nan) for n in out))
