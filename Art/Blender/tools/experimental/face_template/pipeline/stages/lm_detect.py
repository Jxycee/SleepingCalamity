"""MediaPipe face-landmark detection shared by r6_01c_batch_lm.py (one launch per call) and r6_lm_server.py (persistent worker). facekeys venv.
SCP_LM_JIT=5 (cfg fit.lm_jitter): detector-noise denoising for the fit loop. Each render is also detected under 4 re-framings (+-1 % scale / shift); landmarks are mapped back to the
original frame and the per-coordinate MEDIAN over the framings is returned. 0/1 = plain single detection."""
import os, numpy as np, mediapipe as mp, cv2
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
MODEL = 'C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'
JIT_ALL = [(1.0, 0, 0), (1.01, 0, 0), (0.99, 0, 0), (1.0, 0.01, -0.01), (1.0, -0.01, 0.01)]
class Detector:
    def __init__(s): s.lm = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=MODEL), num_faces=1))
    def _det(s, img):
        r = s.lm.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.ascontiguousarray(img[..., ::-1])))
        return np.array([[l.x * img.shape[1], l.y * img.shape[0]] for l in r.face_landmarks[0]]) if r.face_landmarks else None
    def detect_files(s, paths, jit):
        JIT = JIT_ALL[:max(1, int(jit or 0))]; out = {}
        for p in paths:
            if len(JIT) == 1:
                im = mp.Image.create_from_file(p); r = s.lm.detect(im)
                out[p] = [[l.x * im.width, l.y * im.height] for l in r.face_landmarks[0]] if r.face_landmarks else None; continue
            img = cv2.imread(p); h0, w0 = img.shape[:2]; bg = tuple(int(x) for x in img[2, 2]); L = []
            for js, jx, jy in JIT:
                M = np.float32([[js, 0, (1 - js) * w0 / 2 + jx * w0], [0, js, (1 - js) * h0 / 2 + jy * h0]])
                P = s._det(img if js == 1.0 and jx == 0 and jy == 0 else cv2.warpAffine(img, M, (w0, h0), flags=cv2.INTER_LANCZOS4, borderValue=bg))
                if P is not None: L.append((P - M[:, 2]) / js)
            out[p] = np.median(np.stack(L), 0).tolist() if L else None
        return out
