"""Round-3 shared helpers: round-3 paths (fit3/bake3/out3). Inputs that round 3 only reads stay in fit/ (FIT2). numpy/scipy only."""
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
FIT = _P_FIT                           # round-3 data (written)
TGT = _P_TGT
B3 = _P_BAKE; O3 = _P_OUT
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
MP = 'C:/AI/envs/facekeys/Scripts/python.exe'
REPO = _P_REPO + ''
TEX = REPO + 'Assets/_Project/Content/Characters/TobinBase/Textures/'
GLB = REPO + 'Art/Source/MetaPerson/Tobin_Base_v2_sliders.glb'
for p in (FIT, B3, O3): os.makedirs(p, exist_ok=True)
from fit_common import Tpl, weld_faces, edges_of, umeyama, Surf, vertex_normals, p2s_stats
FINAL_TAG = '45'   # round-3 final wrap (fit3/wrap_r23_*)
