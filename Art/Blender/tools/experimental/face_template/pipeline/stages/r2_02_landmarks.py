# mediapipe (run in facekeys env): detect 478 landmarks on a render. usage: img out.json   -> {"idx": [px,py]} for all points
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json, numpy as np, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1)
lm = vision.FaceLandmarker.create_from_options(opt)
im = mp.Image.create_from_file(sys.argv[1]); r = lm.detect(im)
print('faces', len(r.face_landmarks))
if r.face_landmarks:
    L = r.face_landmarks[0]; json.dump({str(i): [L[i].x * im.width, L[i].y * im.height] for i in range(len(L))}, open(sys.argv[2], 'w'))
