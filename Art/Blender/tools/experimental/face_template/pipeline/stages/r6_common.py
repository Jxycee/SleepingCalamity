"""Round-3 shared helpers: round-5 paths (fit6/bake6/out6). Inputs that round 3 only reads stay in fit/ (FIT2). numpy/scipy only."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = _P_TPLROOT      # template data (unchanged)
D2 = _P_RUN
FIT2 = _P_FIT                           # round-2 data (read only)
FIT = _P_FIT                           # round-5 data (written)
TGT = _P_TGT
B4 = _P_BAKE; O4 = _P_OUT
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
MP = 'C:/AI/envs/facekeys/Scripts/python.exe'
REPO = _P_REPO + ''
TEX = REPO + 'Assets/_Project/Content/Characters/TobinBase/Textures/'
GLB = REPO + 'Art/Source/MetaPerson/Tobin_Base_v2_sliders.glb'
TPL6 = FIT + 'tobin_head_template6.npz'; os.environ['SC_TPL'] = TPL6
for p in (FIT, B4, O4): os.makedirs(p, exist_ok=True)
from fit_common import Tpl, weld_faces, edges_of, umeyama, Surf, vertex_normals, p2s_stats
FINAL_TAG = os.environ.get('SC_FINAL_TAG', 'a2')   # final wrap tag (fit6/wrap_r<tag>_*)

def face_flips(t, P):
    """boolean per face: normal reversed vs the template basis"""
    f = lambda Q: (lambda tri: (lambda n: n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30))(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])))(Q[t.faces])
    return ((f(P) * f(t.basis)).sum(1) < 0)

def deflip(t, Pb, Pa, iters=30):
    """General rule: a displacement field that flips faces is damped locally. Vertices of flipped faces (and their face neighbours) get their displacement Pa-Pb halved, repeat until 0 flips.
    Welded copies (t.rep) move together so UV seams stay closed."""
    Pa = Pa.copy(); d0 = Pa - Pb; w = np.ones(len(Pa))
    for _ in range(iters):
        fl = face_flips(t, Pb + w[:, None] * d0)
        if not fl.any(): break
        vs = np.unique(t.rep[t.faces[fl]]); grp = np.zeros(len(Pa), bool); grp[np.isin(t.rep, vs)] = True
        nb = np.zeros(len(Pa), bool); nb[np.unique(t.rep[t.faces[grp[t.faces].any(1)]])] = True; nb = np.isin(t.rep, np.where(nb)[0]) | grp
        w[nb] *= 0.6; w[grp] *= 0.6
        w = w[t.rep] * 0 + np.minimum.reduceat(np.r_[w[np.argsort(t.rep, kind='stable')]], np.r_[0, np.where(np.diff(np.sort(t.rep)))[0] + 1])[np.searchsorted(np.unique(t.rep), t.rep)] if False else w
        # keep welded copies identical: take the minimum over each weld group
        m = np.full(len(Pa), 1.0); np.minimum.at(m, t.rep, w); w = m[t.rep]
    return Pb + w[:, None] * d0
