"""Distribution of face proportions of real-ish frontal faces (SDXL RealVisXL V5 portraits) -> human_prior.json.
Interpreters: gen = any python (stdlib), measure + stats = the facekeys python (mediapipe + numpy + cv2 + scipy: C:/AI/envs/facekeys/Scripts/python.exe).
  python gen_prior_faces.py --n 200                        # 200 bald frontal portraits, varied sex/age/ethnicity (about 30 min, ComfyUI 8190, GPU must be idle)
  python human_prior.py measure [--faces DIR]              # MediaPipe 478 landmarks (same model + same detector call as the pipeline) + proportions.measure() per face -> DIR/measures.json
  python human_prior.py stats   [--faces DIR]              # robust stats -> vision/human_prior.json (also DIR/human_prior.json)
  python human_prior.py all                                # measure + stats
Robust estimate = median and 1.4826 x MAD (sigma); also mean/std, 5/95 %, n, per-sex medians, and a trimmed (|z|<3) covariance for a Mahalanobis distance.
Pose filter: |yaw| <= 10 deg and |pitch| <= 15 deg (facial transformation matrix, as in stages/photo_landmarks.py). Silhouette features need a clean bald crown (proportions.measure ok_sil).
Honest limits: the prior is what SDXL draws as a bald adult face, not anthropometric survey data: it is a smooth, symmetric, slightly idealised population (use it for 'is this proportion in the human range', not for fine likeness)."""
import os, sys, json, math, argparse, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proportions as PR
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
FACE_MODEL = 'C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task'
DEFAULT_FACES = 'C:/AI/test_out/heroes/vision_prior/faces'
PRIOR_JSON = HERE + '/human_prior.json'

def detector():
    import mediapipe as mp
    from mediapipe.tasks import python as mpp
    from mediapipe.tasks.python import vision
    opt = vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=FACE_MODEL), num_faces=1, output_facial_transformation_matrixes=True)
    return mp, vision.FaceLandmarker.create_from_options(opt)

def detect(mp, lm, path):
    """-> (landmarks px (478,2), yaw, pitch) or None"""
    im = mp.Image.create_from_file(path); r = lm.detect(im)
    if not r.face_landmarks: return None
    L = r.face_landmarks[0]; P = np.array([[l.x * im.width, l.y * im.height] for l in L])
    M = np.array(r.facial_transformation_matrixes[0]); R = M[:3, :3] / np.linalg.norm(M[:3, 0])
    return P, math.degrees(math.atan2(R[0, 2], R[2, 2])), math.degrees(math.atan2(-R[1, 2], math.hypot(R[0, 2], R[2, 2])))

def cmd_measure(faces):
    mp, lm = detector(); meta = json.load(open(faces + '/meta.json')) if os.path.exists(faces + '/meta.json') else {}
    rows = {}; rej = {'noface': 0, 'pose': 0}
    for fn in sorted(f for f in os.listdir(faces) if f.startswith('face_') and f.endswith('.png')):
        r = detect(mp, lm, faces + '/' + fn)
        if r is None: rej['noface'] += 1; continue
        P, yaw, pitch = r
        if abs(yaw) > 10 or abs(pitch) > 15: rej['pose'] += 1; continue
        m = PR.measure(cv2.imread(faces + '/' + fn), P); g = m.pop('geom'); ok = m.pop('ok_sil')
        rows[fn] = dict(m, ok_sil=ok, yaw=yaw, pitch=pitch, sex=meta.get(fn, {}).get('sex'), age=meta.get(fn, {}).get('age'), eth=meta.get(fn, {}).get('eth'))
    json.dump(dict(rows=rows, rejected=rej), open(faces + '/measures.json', 'w'), indent=0)
    print('measured', len(rows), 'rejected', rej, 'silhouette ok', sum(r['ok_sil'] for r in rows.values()))

MAH_KEYS = ['face_len_over_width', 'eye_line_in_face', 'nose_base_in_face', 'interocular_over_fw', 'nose_width_over_fw', 'mouth_width_over_fw', 'jaw_width_over_fw', 'head_aspect', 'crown_taper', 'eye_line_in_head', 'dome_over_cheek']


def robust(a):
    a = np.asarray(a, float); med = float(np.median(a)); mad = float(np.median(np.abs(a - med))) * 1.4826
    return dict(median=med, sigma=max(mad, 1e-6), mean=float(a.mean()), std=float(a.std()), p5=float(np.percentile(a, 5)), p95=float(np.percentile(a, 95)), n=int(len(a)))

def cmd_stats(faces):
    rows = json.load(open(faces + '/measures.json'))['rows']; keys = list(PR.FEATURES)
    feats = {}; by_sex = {}
    for k in keys:
        v = [r[k] for r in rows.values() if k in r and (k not in PR.SIL_KEYS or r['ok_sil'])]
        if len(v) < 10: continue
        feats[k] = dict(robust(v), label=PR.FEATURES[k][0], low=PR.FEATURES[k][1], high=PR.FEATURES[k][2])
        for s in ('man', 'woman'):
            vs = [r[k] for r in rows.values() if k in r and r.get('sex') == s and (k not in PR.SIL_KEYS or r['ok_sil'])]
            if len(vs) >= 10: by_sex.setdefault(s, {})[k] = dict(median=float(np.median(vs)), sigma=max(float(np.median(np.abs(np.array(vs) - np.median(vs)))) * 1.4826, 1e-6), n=len(vs))
    ks = [k for k in MAH_KEYS if k in feats]; full = [r for r in rows.values() if all(k in r for k in ks) and r['ok_sil']]
    X = np.array([[r[k] for k in ks] for r in full]); mu = np.median(X, 0); sg = np.array([feats[k]['sigma'] for k in ks])
    sg = np.array([feats[k]['sigma'] for k in ks]); inl = np.all(np.abs((X - mu) / sg) < 3, axis=1); C = np.cov(X[inl].T); mu2 = X[inl].mean(0)
    C = 0.8 * C + 0.2 * np.diag(np.diag(C))   # shrink towards the diagonal: few samples, correlated features
    d = dict(source='SDXL RealVisXL V5 bald frontal portraits', n_faces=len(rows), n_with_silhouette=len(full), n_cov_inliers=int(inl.sum()), features=feats, by_sex=by_sex,
             mahalanobis=dict(keys=ks, mean=mu2.tolist(), cov=C.tolist()),
             side_literature={'note': 'UNCALIBRATED: no side-view data in the SDXL prior; approximate adult anthropometry from the literature (mean, sd), rounded; use only as a sanity flag',
                              'head_width_over_depth': dict(median=0.79, sigma=0.045, low='skull too deep front-to-back (long head)', high='skull too wide / short front-to-back', label='skull dome width / dome depth (cephalic index)'),
                              'head_depth_over_height': dict(median=0.83, sigma=0.06, low='head too shallow front-to-back for its height', high='head too deep front-to-back for its height (football from the side)', label='dome depth / crown-to-chin height'),
                              'head_height_mm': dict(median=225.0, sigma=11.0, low='head too small (absolute)', high='head too big (absolute)', label='crown to chin height in mm')})
    json.dump(d, open(PRIOR_JSON, 'w'), indent=1); json.dump(d, open(faces + '/human_prior.json', 'w'), indent=1)
    print('wrote', PRIOR_JSON); print('%-24s %8s %8s %8s %8s %5s' % ('feature', 'median', 'sigma', 'p5', 'p95', 'n'))
    for k, v in feats.items(): print('%-24s %8.3f %8.3f %8.3f %8.3f %5d' % (k, v['median'], v['sigma'], v['p5'], v['p95'], v['n']))

if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('cmd', choices=['measure', 'stats', 'all']); ap.add_argument('--faces', default=DEFAULT_FACES); a = ap.parse_args()
    if a.cmd in ('measure', 'all'): cmd_measure(a.faces)
    if a.cmd in ('stats', 'all'): cmd_stats(a.faces)
