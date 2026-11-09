# facekeys env: mediapipe landmarks (px) for many images. usage: out.json img1.png img2.png ...  -> {img: [[x,y]*468]}
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json, os, numpy as np, mediapipe as mp, cv2
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1)
lm = vision.FaceLandmarker.create_from_options(opt); out = {}
# SCP_LM_JIT=5 (cfg fit.lm_jitter): detector-noise denoising for the fit loop. Each render is also detected under 4 re-framings (+-1 % scale / shift); landmarks are mapped back to the
# original frame and the per-coordinate MEDIAN over the framings is returned. 0/1 = plain single detection.
JIT = [(1.0, 0, 0), (1.01, 0, 0), (0.99, 0, 0), (1.0, 0.01, -0.01), (1.0, -0.01, 0.01)][:max(1, int(os.environ.get('SCP_LM_JIT', '0') or 0))]
def det(img):
    r = lm.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(img[..., ::-1])))
    return np.array([[l.x * img.shape[1], l.y * img.shape[0]] for l in r.face_landmarks[0]]) if r.face_landmarks else None
for p in sys.argv[2:]:
    if len(JIT) == 1:
        im = mp.Image.create_from_file(p); r = lm.detect(im)
        out[p] = [[l.x * im.width, l.y * im.height] for l in r.face_landmarks[0]] if r.face_landmarks else None; continue
    img = cv2.imread(p); h0, w0 = img.shape[:2]; bg = tuple(int(x) for x in img[2, 2]); L = []
    for js, jx, jy in JIT:
        M = np.float32([[js, 0, (1 - js) * w0 / 2 + jx * w0], [0, js, (1 - js) * h0 / 2 + jy * h0]])
        P = det(img if js == 1.0 and jx == 0 and jy == 0 else cv2.warpAffine(img, M, (w0, h0), flags=cv2.INTER_LANCZOS4, borderValue=bg))
        if P is not None: L.append((P - M[:, 2]) / js)
    out[p] = np.median(np.stack(L), 0).tolist() if L else None
json.dump(out, open(sys.argv[1], 'w'))
