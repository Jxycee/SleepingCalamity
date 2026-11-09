
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys; from r6_common import *
t = Tpl(); r = sys.argv[1]; P = np.load(FIT + f'wrap_r{r}_basis.npy').astype(float)
def nrm(V): tri = V[t.faces]; return np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
n, n0 = nrm(P), nrm(t.basis); fl = (n * n0).sum(1) / (np.linalg.norm(n, axis=1) * np.linalg.norm(n0, axis=1) + 1e-30) < 0
c = t.basis[t.faces][fl].mean(1); print('flips', fl.sum())
reg = {k: np.zeros(len(t.basis), bool) for k in ('eye_socket', 'mouth_interior', 'nostril', 'lip_line', 'ear_L', 'ear_R', 'eyelid_ring_L', 'eyelid_ring_R')}
for k in reg: reg[k][t.S(k)] = True
cnt = {k: int(reg[k][t.faces[fl]].any(1).sum()) for k in reg}; print('flipped faces touching region', cnt)
area0 = np.linalg.norm(n0, axis=1) / 2; area = np.linalg.norm(n, axis=1) / 2
print('flipped face areas mm2 (new/old)', np.round(area[fl][:8] * 1e6, 2), np.round(area0[fl][:8] * 1e6, 2))
print('min/max area ratio overall', (area / (area0 + 1e-12)).min(), np.percentile(area / (area0 + 1e-12), [1, 99]))
