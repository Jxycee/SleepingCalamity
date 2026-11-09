# facekeys env: compare front clay render of the target mesh with the cropped source: MediaPipe landmark ratios + eye-aligned overlay.
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json, numpy as np, mediapipe as mp, cv2, sys
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
D=_P_TGT
opt=vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'),num_faces=1)
lm=vision.FaceLandmarker.create_from_options(opt)
def get(p):
    im=mp.Image.create_from_file(p); r=lm.detect(im)
    if not r.face_landmarks: return None
    return np.array([[l.x*im.width,l.y*im.height] for l in r.face_landmarks[0]])
src=get(D+'crop_src.png'); cl=get(sys.argv[1] if len(sys.argv)>1 else D+'r_clay_front.png')
print('src',src is not None,'clay',cl is not None)
if cl is None: sys.exit()
def R(P):
    d=lambda a,b:np.linalg.norm(P[a]-P[b]); iod=d(133,362)  # inner eye corners ... use outer for scale
    io=d(33,263); eyec=(P[33]+P[263])/2
    return {'nose_width/IOD':d(129,358)/io,'mouth_width/IOD':d(61,291)/io,'face_width(234-454)/IOD':d(234,454)/io,
     'jaw_width(172-397)/IOD':d(172,397)/io,'brow_above_eye(105-159, 334-386)/IOD':((abs(P[105][1]-P[159][1])+abs(P[334][1]-P[386][1]))/2)/io,
     'eye_to_nose_base(2)/IOD':(P[2][1]-eyec[1])/io,'nose_to_mouth(2->13)/IOD':(P[13][1]-P[2][1])/io,'mouth_to_chin(13->152)/IOD':(P[152][1]-P[13][1])/io,
     'eye_to_chin/IOD':(P[152][1]-eyec[1])/io,'forehead_top_to_eye/IOD':(eyec[1]-P[10][1])/io,'lip_thickness(0-17)/IOD':d(0,17)/io}
a,b=R(src),R(cl)
for k in a: print(f'{k:42s} src {a[k]:.3f} clay {b[k]:.3f} diff {100*(b[k]/a[k]-1):+.1f}%')
json.dump({'src':a,'clay':b},open(D+'compare_ratios.json','w'),indent=1)
# overlay: similarity transform from clay->src using eye corners, nose tip, chin, mouth corners
ids=[33,263,1,152,61,291,10]
M,_=cv2.estimateAffinePartial2D(cl[ids].astype(np.float32),src[ids].astype(np.float32))
img=cv2.imread(sys.argv[1] if len(sys.argv)>1 else D+'r_clay_front.png'); s=cv2.imread(D+'crop_src.png')
w=cv2.warpAffine(img,M,(s.shape[1],s.shape[0]),borderValue=(140,140,140))
ov=cv2.addWeighted(s,0.5,w,0.5,0)
# silhouette edge overlay
g=cv2.cvtColor(w,cv2.COLOR_BGR2GRAY); e=cv2.Canny(g,40,120); o2=s.copy(); o2[e>0]=(0,255,255)
cv2.imwrite(D+'overlay_clay_vs_src.png',np.hstack([s,ov,o2]))
