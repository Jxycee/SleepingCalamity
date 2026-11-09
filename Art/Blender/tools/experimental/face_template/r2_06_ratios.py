# facekeys env: front-view feature ratios of renders vs the PHOTO (crop_src.png landmarks).  usage: out.json label=img.png [label=img.png ...]
# Also writes <img>_overlay.png (render eye-aligned onto crop_src.png, 50% blend) when label starts with '+'.
import sys, json, numpy as np, mediapipe as mp, cv2
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
TGT = 'C:/AI/test_out/heroes/face_spike2/target/'
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1)
lm = vision.FaceLandmarker.create_from_options(opt)
def get(p):
    im = mp.Image.create_from_file(p); r = lm.detect(im)
    return np.array([[l.x * im.width, l.y * im.height] for l in r.face_landmarks[0]]) if r.face_landmarks else None
def R(P):
    d = lambda a, b: np.linalg.norm(P[a] - P[b]); io = d(33, 263); eyec = (P[33] + P[263]) / 2
    return {'nose_width': d(129, 358) / io, 'mouth_width': d(61, 291) / io, 'face_width': d(234, 454) / io, 'jaw_width': d(172, 397) / io,
            'brow_eye_gap': ((abs(P[105][1] - P[159][1]) + abs(P[334][1] - P[386][1])) / 2) / io, 'eye_to_nose_base': (P[2][1] - eyec[1]) / io,
            'nose_to_mouth': (P[13][1] - P[2][1]) / io, 'mouth_to_chin': (P[152][1] - P[13][1]) / io, 'eye_to_chin': (P[152][1] - eyec[1]) / io,
            'forehead_eye': (eyec[1] - P[10][1]) / io, 'lip_thickness': d(0, 17) / io}
src = get(TGT + 'crop_src.png'); a = R(src); out = {'photo': a}
for arg in sys.argv[2:]:
    lab, p = arg.split('=', 1); ov = lab.startswith('+'); lab = lab.lstrip('+'); P = get(p)
    if P is None: print(lab, 'NO FACE'); continue
    b = R(P); out[lab] = b
    if ov:
        ids = [33, 263, 1, 152, 61, 291, 10]
        M, _ = cv2.estimateAffinePartial2D(P[ids].astype(np.float32), src[ids].astype(np.float32))
        img = cv2.imread(p); s = cv2.imread(TGT + 'crop_src.png'); w = cv2.warpAffine(img, M, (s.shape[1], s.shape[0]), borderValue=(140, 140, 140))
        cv2.imwrite(p.replace('.png', '_overlay.png'), np.hstack([s, cv2.addWeighted(s, 0.5, w, 0.5, 0)]))
json.dump(out, open(sys.argv[1], 'w'), indent=1)
labs = [k for k in out if k != 'photo']
print('%-18s %8s ' % ('ratio/IOD', 'photo') + ' '.join('%14s' % l for l in labs))
for k in a: print('%-18s %8.3f ' % (k, a[k]) + ' '.join('%7.3f(%+5.1f%%)' % (out[l][k], 100 * (out[l][k] / a[k] - 1)) for l in labs))
