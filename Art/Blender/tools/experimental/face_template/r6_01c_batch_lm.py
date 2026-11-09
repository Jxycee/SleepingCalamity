# facekeys env: mediapipe landmarks (px) for many images. usage: out.json img1.png img2.png ...  -> {img: [[x,y]*468]}
import sys, json, numpy as np, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1)
lm = vision.FaceLandmarker.create_from_options(opt); out = {}
for p in sys.argv[2:]:
    im = mp.Image.create_from_file(p); r = lm.detect(im)
    out[p] = [[l.x * im.width, l.y * im.height] for l in r.face_landmarks[0]] if r.face_landmarks else None
json.dump(out, open(sys.argv[1], 'w'))
