"""Write the warp mesh (y-snapped, de-flipped, delta-transferred keys) for a stored theta. usage: r5_05_save_theta.py <theta.json|warp_x.json> <in_basis> <lm.json> <cam.json> <png> <tag>"""
import sys, json
th_json = sys.argv[1]; sys.argv = [sys.argv[0]] + sys.argv[2:]
import r5_02_warp as W
from r5_common import *
d = json.load(open(th_json)); d = d.get('theta', d); th = np.array([d.get(k, 0.0) for k in W.NAMES])
P = W.ysnap(W.warp(th)); chk = W.gates(P); print('GATES PASS=%s flips %d' % (chk['PASS'], W.flips_of(P)), W.MT.tgt_stats(P))
dB = P - W.t.basis; nk = (W.t.keys + dB[None]).astype(np.float32); nk[0] = P; tag = W.A.tag
np.save(FIT + f'wrap_r{tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=P, f=W.t.faces)
json.dump(dict(theta=dict(zip(W.NAMES, map(float, th))), checks=chk), open(FIT + f'wrap_r{tag}_report.json', 'w'), indent=1, default=float)
