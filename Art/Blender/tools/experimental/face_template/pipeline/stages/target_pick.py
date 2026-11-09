"""Stage 2 helper (facekeys python: cv2 mediapipe numpy). Score every Hunyuan candidate by its front clay render against the photo and pick one.
usage: target_pick.py <cfg.json>
Inputs: target/glb/shape_s<seed>.glb with target/glb/clay_s<seed>_front.png (cloud_render_clay.py), target/crop_src.png, target/mask_raw.png.
Score per candidate = IoU(similarity-aligned clay silhouette above the chin, photo cutout) - 2 * MAE(11 landmark ratios clay vs photo, as a fraction).
Writes target/pick.json and copies the winner to target/shape.glb."""
import os, sys, json, shutil
import numpy as np, cv2, mediapipe as mp
from mediapipe.tasks import python as mpp
from mediapipe.tasks.python import vision
cfg = json.load(open(sys.argv[1])); D = os.environ['SCP_RUN'].rstrip('/') + '/target/'
lm = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=os.environ['SCP_FACE_MODEL']), num_faces=1))
def get(p):
    r = lm.detect(mp.Image.create_from_file(p)); im = cv2.imread(p)
    return None if not r.face_landmarks else np.array([[l.x * im.shape[1], l.y * im.shape[0]] for l in r.face_landmarks[0]])
def ratios(P):
    d = lambda a, b: np.linalg.norm(P[a] - P[b]); io = d(33, 263); ec = (P[33] + P[263]) / 2
    return np.array([d(129, 358) / io, d(61, 291) / io, d(234, 454) / io, d(172, 397) / io, ((abs(P[105][1] - P[159][1]) + abs(P[334][1] - P[386][1])) / 2) / io,
                     (P[2][1] - ec[1]) / io, (P[13][1] - P[2][1]) / io, (P[152][1] - P[13][1]) / io, (P[152][1] - ec[1]) / io, (ec[1] - P[10][1]) / io, d(0, 17) / io])
src = get(D + 'crop_src.png'); rs = ratios(src); mask = cv2.imread(D + 'mask_raw.png', 0) > 127
chin = int(src[152][1]) + 15; res = {}
for seed in cfg['hunyuan']['seeds']:
    png = D + 'glb/clay_s%s_front.png' % seed
    if not os.path.exists(png): continue
    cl = get(png)
    if cl is None: res[str(seed)] = dict(score=-9, note='no face detected on the clay render'); continue
    ids = [33, 263, 1, 152, 61, 291, 10]
    M, _ = cv2.estimateAffinePartial2D(cl[ids].astype(np.float32), src[ids].astype(np.float32))
    img = cv2.imread(png); bgc = img[2, 2].astype(int); h, w = mask.shape
    cm = (np.abs(img.astype(int) - bgc).sum(2) > 25).astype(np.uint8) * 255
    cm = cv2.warpAffine(cm, M, (w, h), flags=cv2.INTER_NEAREST) > 127
    a, b = cm.copy(), mask.copy(); a[chin:] = False; b[chin:] = False
    iou = float((a & b).sum() / max((a | b).sum(), 1)); mae = float(np.abs(ratios(cl) / rs - 1).mean())
    res[str(seed)] = dict(iou=iou, ratio_mae=mae, score=iou - 2 * mae)
    print('seed', seed, res[str(seed)])
best = max(res, key=lambda k: res[k]['score']); json.dump(dict(candidates=res, chosen=best), open(D + 'pick.json', 'w'), indent=1)
shutil.copy(D + 'glb/shape_s%s.glb' % best, D + 'shape.glb'); print('chosen seed', best)
