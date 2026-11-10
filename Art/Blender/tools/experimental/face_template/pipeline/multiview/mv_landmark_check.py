"""Identity-consistency check of generated turnaround views by MediaPipe landmarks (front + both 3/4 views).
usage (facekeys python): python mv_landmark_check.py <views_dir> <tag>
Yaw-invariant vertical ratios (eye line to mouth / chin-to-brow height, nose length / face height) and the yaw-corrected inter-ocular ratio (IOD / cos(yaw) / face height) must agree
between views of one person. Writes <views_dir>/<tag>_landmarks.json and prints a table."""
import sys, json, math, numpy as np, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
vd, tag = sys.argv[1], sys.argv[2]
opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'), num_faces=1, output_facial_transformation_matrixes=True)
lm = vision.FaceLandmarker.create_from_options(opt); out = {}
for v in ('front', 'q34l', 'q34r', 'left', 'right', 'back'):
    im = mp.Image.create_from_file(f'{vd}/{tag}_{v}.png'); r = lm.detect(im)
    if not r.face_landmarks: out[v] = None; print(v, 'no face'); continue
    L = r.face_landmarks[0]; W, H = im.width, im.height; P = lambda i: np.array([L[i].x * W, L[i].y * H])
    R = np.array(r.facial_transformation_matrixes[0])[:3, :3]; R = R / np.linalg.norm(R[:, 0]); yaw = math.degrees(math.atan2(R[0, 2], R[2, 2]))
    fh = np.linalg.norm(P(152) - P(10)); iod = np.linalg.norm(P(33) - P(263)); eye_mid = (P(33) + P(263)) / 2; mouth = (P(61) + P(291)) / 2
    out[v] = dict(yaw=yaw, iod_over_fh_corrected=float(iod / max(math.cos(math.radians(yaw)), 0.3) / fh), eye_mouth_over_fh=float(np.linalg.norm(mouth - eye_mid) / fh),
                  nose_len_over_fh=float(np.linalg.norm(P(168) - P(2)) / fh), chin_mouth_over_fh=float(np.linalg.norm(P(152) - P(17)) / fh), face_h_px=float(fh))
    print(v, {k: round(x, 3) for k, x in out[v].items()})
json.dump(out, open(f'{vd}/{tag}_landmarks.json', 'w'), indent=1)
