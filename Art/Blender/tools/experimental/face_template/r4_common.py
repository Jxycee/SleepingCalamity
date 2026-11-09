"""Round-3 shared helpers: round-3 paths (fit4/bake4/out4). Inputs that round 3 only reads stay in fit/ (FIT2). numpy/scipy only."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = 'C:/AI/test_out/heroes/face_spike/'      # template data (unchanged)
D2 = 'C:/AI/test_out/heroes/face_spike2/'
FIT2 = D2 + 'fit/'                           # round-2 data (read only)
FIT = D2 + 'fit4/'                           # round-3 data (written)
TGT = D2 + 'target/'
B4 = D2 + 'bake4/'; O4 = D2 + 'out4/'
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
PY = 'C:/AI/trellis2/venv/Scripts/python.exe'
MP = 'C:/AI/envs/facekeys/Scripts/python.exe'
REPO = 'C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/'
TEX = REPO + 'Assets/_Project/Content/Characters/TobinBase/Textures/'
GLB = REPO + 'Art/Source/MetaPerson/Tobin_Base_v2_sliders.glb'
for p in (FIT, B4, O4): os.makedirs(p, exist_ok=True)
from fit_common import Tpl, weld_faces, edges_of, umeyama, Surf, vertex_normals, p2s_stats
FINAL_TAG = 'a2'   # round-4 final wrap (fit4/wrap_ra2_*)
