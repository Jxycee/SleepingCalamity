# facekeys env: FaceLandmarker on ortho front render -> pixel coords for named template landmarks.
import sys, json, numpy as np, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
img_path, out = sys.argv[1], sys.argv[2]
MAP = {'eye_outer_R':33,'eye_inner_R':133,'eye_inner_L':362,'eye_outer_L':263,'nose_tip':1,'subnasale':2,
       'sellion':168,'chin_tip':152,'mouth_corner_R':61,'mouth_corner_L':291,'nostril_wing_R':129,'nostril_wing_L':358,
       'upper_lip_center':0,'forehead_top':10}
opt=vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'),num_faces=1)
lm=vision.FaceLandmarker.create_from_options(opt)
im=mp.Image.create_from_file(img_path); r=lm.detect(im)
print('faces',len(r.face_landmarks))
if r.face_landmarks:
    L=r.face_landmarks[0]; W,H=im.width,im.height
    d={k:[L[i].x*W,L[i].y*H] for k,i in MAP.items()}
    json.dump(d,open(out,'w'),indent=1); print(d)
