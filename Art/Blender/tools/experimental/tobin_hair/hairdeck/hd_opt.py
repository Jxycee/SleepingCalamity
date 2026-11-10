# Fit + volume search for HairDeck hair on TobinBase against the reference silhouettes (trellis venv python: numpy, scipy, cv2).
#   python hd_opt.py <head.npz> <strands.npz | dir of strands*.npz> <hull_dir> <out_dir> [--rounds 8] [--w 0.5,0.3,0.2[,frame]]
# Same mapping as hd_fit.py (fit=ell): HairDeck frame -> Blender (x, -z, y) -> head-proxy ellipsoid onto the TobinBase cranium ellipsoid,
# times per-axis multipliers sx sy sz and offsets dy dz; then the volume deformation (vol_deform, also in hd_fit.py):
#   crown lift: radial push lt * (0.35 + 0.65 smoothstep(0, 0.45, t)) * max(0, d_z)^1.5      (t = card fraction root..tip, d = unit dir from skull centre)
#   side flare: radial push fs * t^1.5 * |d_x| * (1 - max(0, d_z))
# then a clearance push (2 mm above AvatarHead, nearest head vertex + its normal). Score = weighted IoU (front, side, back) from hd_sil.py.
# A 4th weight scores the front face-framing band (hd_sil.py 'frame': cheek-length side locks). Coordinate descent over (sx, sy, sz, dy, dz, lt, fs); writes best.json, overlay_best.png and prints the trail.
import sys, os, json, glob, numpy as np
from scipy.spatial import cKDTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from hd_sil import score, load_mesh   # noqa: E402
a = sys.argv[1:]; HEAD, SRC, HULL, OUT = a[:4]; os.makedirs(OUT, exist_ok=True)
opt = lambda k, d: a[a.index(k) + 1] if k in a else d
ROUNDS = int(opt('--rounds', 8)); WV = [float(x) for x in opt('--w', '0.5,0.3,0.2').split(',')]
PC, PR = np.array([0.0, -0.0003, 0.422]), np.array([0.0517, 0.0638, 0.0638])
hv, hf = load_mesh(HEAD, 'AvatarHead'); hn = np.load(HEAD)['AvatarHead_n'].astype(float)
x, y, z = hv.T; m = (z > 1.70) & ~((np.abs(x) > 0.068) & (z < 1.765)) & ~((y < -0.075) & (z < 1.785))
X = hv[m]; coef = np.linalg.lstsq(np.c_[X ** 2, X], np.ones(len(X)), rcond=None)[0]
a2, d1 = coef[:3], coef[3:]; CEN = -d1 / (2 * a2); RAD = np.sqrt((1 + (d1 ** 2 / (4 * a2)).sum()) / a2)
KD = cKDTree(hv)
def vol_deform(P, t, lt, fs, cen):
    d = P - cen; d /= np.linalg.norm(d, axis=1, keepdims=True) + 1e-9
    s = np.clip(t / 0.45, 0, 1); s = s * s * (3 - 2 * s)
    up = np.clip(d[:, 2], 0, 1)
    amt = lt * (0.35 + 0.65 * s) * up ** 1.5 + fs * np.clip(t, 0, 1) ** 1.5 * np.abs(d[:, 0]) * (1 - up)
    return P + d * amt[:, None]
def build(V0, t, p):
    V = np.c_[V0[:, 0], -V0[:, 2], V0[:, 1]]
    S = RAD / PR * np.array([p['sx'], p['sy'], p['sz']]); V = CEN + (V - PC) * S + np.array([0, p['dy'], p['dz']])
    V = vol_deform(V, t, p['lt'], p['fs'], CEN)
    _, i = KD.query(V); n = hn[i]; sd = ((V - hv[i]) * n).sum(1); low = sd < 0.002
    V[low] += n[low] * (0.002 - sd[low])[:, None]
    return V
def evaluate(V, F, overlay=None):
    r = score([(hv, hf), (V, F)], HULL, overlay=overlay)
    return sum(w * r[v]['iou'] for w, v in zip(WV, ('front', 'side', 'back', 'frame'))), r
files = sorted(glob.glob(os.path.join(SRC, '*.npz'))) if os.path.isdir(SRC) else [SRC]
P0 = dict(sx=1.0, sy=1.0, sz=1.0, dy=0.0, dz=0.0, lt=0.0, fs=0.0)
STEP = dict(sx=0.04, sy=0.04, sz=0.04, dy=0.004, dz=0.004, lt=0.006, fs=0.006)
LIM = dict(sx=(0.85, 1.2), sy=(0.8, 1.15), sz=(0.95, 1.08), dy=(-0.02, 0.02), dz=(-0.015, 0.02), lt=(0.0, 0.04), fs=(0.0, 0.03))
results = []
for fpath in files:
    dd = np.load(fpath); V0, F, t = dd['v'].astype(float), dd['f'], dd['uv'][:, 1].astype(float)
    p = dict(P0); best, r = evaluate(build(V0, t, p), F); st = dict(STEP)
    print(os.path.basename(fpath), 'start %.4f' % best, flush=True)
    for rd in range(ROUNDS):
        moved = False
        for k in P0:
            for sgn in (1, -1):
                q = dict(p); q[k] = float(np.clip(p[k] + sgn * st[k], *LIM[k]))
                if q[k] == p[k]: continue
                sc, rr = evaluate(build(V0, t, q), F)
                if sc > best + 1e-4: best, p, r, moved = sc, q, rr, True; break
        if not moved: st = {k: v * 0.5 for k, v in st.items()}
        print('  round %d %.4f %s' % (rd, best, {k: round(v, 4) for k, v in p.items()}), flush=True)
    results.append(dict(file=fpath, score=best, params=p, sil={v: r[v] for v in ('front', 'side', 'back', 'frame')}))
results.sort(key=lambda e: -e['score'])
for e in results: print('RESULT %.4f %s' % (e['score'], os.path.basename(e['file'])), {v: round(e['sil'][v]['iou'], 3) for v in ('front', 'side', 'back', 'frame')})
b = results[0]; dd = np.load(b['file'])
evaluate(build(dd['v'].astype(float), dd['uv'][:, 1].astype(float), b['params']), dd['f'], overlay=os.path.join(OUT, 'overlay_best.png'))
json.dump(dict(best=b, all=results, cranium=dict(cen=CEN.tolist(), rad=RAD.tolist())), open(os.path.join(OUT, 'best.json'), 'w'), indent=1)
print('BEST', b['file'], b['params'])
