"""Round-2 shared helpers (copy of fit_common with round-2 paths). numpy/scipy only."""
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = 'C:/AI/test_out/heroes/face_spike/'      # template data (unchanged)
D2 = 'C:/AI/test_out/heroes/face_spike2/'    # round-2 data
FIT = D2 + 'fit/'
TGT = D2 + 'target/'
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
MP = 'C:/AI/envs/facekeys/Scripts/python.exe'
from fit_common import Tpl, weld_faces, edges_of, umeyama, Surf, vertex_normals, p2s_stats  # unchanged utilities
