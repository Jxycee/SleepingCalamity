"""Slider-authoring shared helpers (sl_*). numpy/scipy only. Units metres, Z up, face toward -Y, char-left = +X."""
import numpy as np, scipy.sparse as sp, scipy.sparse.csgraph as csg
from fit_common import Tpl, edges_of, vertex_normals
OUT = 'C:/AI/test_out/heroes/face_spike2/sliders/'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
LMJSON = 'C:/AI/test_out/heroes/face_spike/template/landmarks_template.json'

def split_groups(t):
    f = t.S('split_groups_flat'); i = t.S('split_groups_id')
    g = {}
    for a, b in zip(f, i): g.setdefault(int(b), []).append(int(a))
    return list(g.values())

def smoothstep(x):
    x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)

def mirror_map(t, tol=2.5e-4):
    """index of mirrored-by-x vertex (nearest to (-x,y,z)), per vertex; plus distance."""
    from scipy.spatial import cKDTree
    b = t.basis; m = b * np.array([-1, 1, 1])
    d, j = cKDTree(b).query(m); return j, d
