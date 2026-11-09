"""usage: mesh.npz out.npz  -> adds the template eyeballs (for the front-ortho Workbench render)"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
TPL6 = _P_FIT + 'tobin_head_template6.npz'
import numpy as np, sys
z = np.load(TPL6); d = np.load(sys.argv[1])
ev = np.concatenate([z['eyeL/basis'], z['eyeR/basis']]); ef = np.concatenate([z['eyeL/faces'], z['eyeR/faces'] + 512])
if 'ev' in d.files: ev, ef = d['ev'], d['ef']
np.savez(sys.argv[2], v=d['v'].astype(float), f=d['f'], ev=ev, ef=ef)
