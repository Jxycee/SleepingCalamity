"""Round-3 shared helpers: round-5 paths (fit5/bake5/out5). Inputs that round 3 only reads stay in fit/ (FIT2). numpy/scipy only."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = 'C:/AI/test_out/heroes/face_spike/'      # template data (unchanged)
D2 = 'C:/AI/test_out/heroes/face_spike2/'
FIT2 = D2 + 'fit/'                           # round-2 data (read only)
FIT = D2 + 'fit5/'                           # round-5 data (written)
TGT = D2 + 'target/'
B4 = D2 + 'bake5/'; O4 = D2 + 'out5/'
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
MP = 'C:/AI/envs/facekeys/Scripts/python.exe'
REPO = 'C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/'
TEX = REPO + 'Assets/_Project/Content/Characters/TobinBase/Textures/'
GLB = REPO + 'Art/Source/MetaPerson/Tobin_Base_v2_sliders.glb'
for p in (FIT, B4, O4): os.makedirs(p, exist_ok=True)
from fit_common import Tpl, weld_faces, edges_of, umeyama, Surf, vertex_normals, p2s_stats
FINAL_TAG = os.environ.get('SC_FINAL_TAG', 'a2')   # final wrap tag (fit5/wrap_r<tag>_*)

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
