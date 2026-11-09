"""Write the warp mesh (y-snapped, de-flipped, delta-transferred keys) for a stored theta. usage: r6_05_save_theta.py <theta.json|warp_x.json> <in_basis> <lm.json> <cam.json> <png> <tag>"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json
th_json = sys.argv[1]; sys.argv = [sys.argv[0]] + sys.argv[2:]
import r6_02_warp as W
from r6_common import *
d = json.load(open(th_json)); d = d.get('theta', d); th = np.array([d.get(k, 0.0) for k in W.NAMES])
P = W.ysnap(W.warp(th)); chk = W.gates(P); print('GATES PASS=%s flips %d' % (chk['PASS'], W.flips_of(P)), W.MT.tgt_stats(P))
dB = P - W.t.basis; nk = (W.t.keys + dB[None]).astype(np.float32); nk[0] = P; tag = W.A.tag
np.save(FIT + f'wrap_r{tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=P, f=W.t.faces)
json.dump(dict(theta=dict(zip(W.NAMES, map(float, th))), checks=chk), open(FIT + f'wrap_r{tag}_report.json', 'w'), indent=1, default=float)
