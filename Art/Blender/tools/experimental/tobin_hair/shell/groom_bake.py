# Bake groom strands into the hair card atlas (trellis venv python: numpy + cv2). Called by groom_cards.py:
#   python groom_bake.py <bake.json> <out_dir>
# bake.json: [{tile, strands: [[(u 0..1 across, v 0..1 along root->tip, depth across the card normal), ...], ...]}, ...]
# Atlas 2048, 16 x 4 tiles of 128 x 512 px, drawn at 2x with anti-aliased strands, then downsampled. Strands deeper in the clump are
# darker (fake self-shadow), roots darker, tips a little lighter; empty texels get the neighbouring colour so mips do not go black.
import sys, os, json, numpy as np, cv2
jp, out = sys.argv[1], sys.argv[2]; data = json.load(open(jp)); os.makedirs(out, exist_ok=True)
S = 2; N = 2048 * S; rng = np.random.default_rng(5)
ROOT = np.array([46, 32, 23], np.float32); MID = np.array([124, 92, 67], np.float32); HI = np.array([178, 142, 108], np.float32)
col = np.zeros((N, N, 3), np.float32); alpha = np.zeros((N, N), np.float32); height = np.zeros((N, N), np.float32)
TW, TH = N // 16, N // 4
for tile in data:
    t = tile['tile']; tx, ty = t % 16, t // 16
    x0 = tx * TW + 2 * S; x1 = (tx + 1) * TW - 2 * S
    y_root = N - (ty + 1) * TH + 2 * S; y_tip = N - ty * TH - 2 * S        # image row 0 = v 1: the root (v1) is the tile's top row
    strands = sorted(tile['strands'], key=lambda st: np.mean([p[2] for p in st]))   # back to front
    for st in strands:
        a = np.array(st, np.float32)
        if len(a) < 2: continue
        # densify (smooth curve through the card rows)
        k = np.linspace(0, len(a) - 1, len(a) * 6); a = np.stack([np.interp(k, np.arange(len(a)), a[:, j]) for j in range(3)], 1)
        xs = x0 + np.clip(a[:, 0], 0.02, 0.98) * (x1 - x0); ys = y_root + a[:, 1] * (y_tip - y_root)
        depth = float(np.clip(np.mean(a[:, 2]), -0.6, 0.6)); bright = rng.uniform(0.82, 1.18) * (0.86 + 0.28 * (depth + 0.6) / 1.2)
        th = max(1, int(round(rng.uniform(1.6, 3.0) * S)))
        n = len(xs)
        for i in range(n - 1):
            s = a[i, 1]
            c = MID * bright * (0.66 + 0.34 * min(1.0, s * 2.2)) + (HI - MID) * max(0.0, bright - 1.0) * 1.5 + (HI - MID) * 0.25 * s
            p = np.array([[xs[i], ys[i]], [xs[i + 1], ys[i + 1]]], np.int32)
            cv2.polylines(col, [p], False, np.clip(c, 0, 255).tolist(), th, cv2.LINE_AA)
            cv2.polylines(alpha, [p], False, 255, th, cv2.LINE_AA)
            cv2.polylines(height, [p], False, float(bright), th + 1, cv2.LINE_AA)
col = cv2.resize(col, (2048, 2048), interpolation=cv2.INTER_AREA); alpha = cv2.resize(alpha, (2048, 2048), interpolation=cv2.INTER_AREA)
h = cv2.GaussianBlur(cv2.resize(height, (2048, 2048), interpolation=cv2.INTER_AREA), (0, 0), 1.0)
w = np.clip(alpha / 255.0, 0, 1)[..., None]
blur = cv2.blur(col * w, (25, 25)); ws = cv2.blur(w[..., 0], (25, 25))[..., None]
bg = np.where(ws > 1e-3, blur / np.maximum(ws, 1e-3), MID * 0.75)
cm = np.minimum(1, w * 2.5); col = col / np.maximum(w, 1e-3) * (w > 0.02) ; col = np.where(w > 0.02, col, bg) * cm + bg * (1 - cm)
cv2.imwrite(os.path.join(out, 'Hair_Cards_BaseColor.png'), np.dstack([np.clip(col, 0, 255).astype(np.uint8)[..., ::-1], np.clip(alpha, 0, 255).astype(np.uint8)]))
gx = cv2.Sobel(h, cv2.CV_32F, 1, 0, ksize=3); gy = cv2.Sobel(h, cv2.CV_32F, 0, 1, ksize=3)
nrm = np.dstack([-gx * 1.5, gy * 1.5, np.ones_like(h)]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
cv2.imwrite(os.path.join(out, 'Hair_Cards_Normal.png'), ((nrm * 0.5 + 0.5) * 255).astype(np.uint8)[..., ::-1])
sm = np.clip(0.18 + 0.14 * h / max(h.max(), 1e-3), 0, 1) * 255
cv2.imwrite(os.path.join(out, 'Hair_Cards_MetallicSmooth.png'), np.dstack([np.zeros_like(sm)] * 3 + [sm]).astype(np.uint8)[..., [2, 1, 0, 3]])
print('BAKED tiles', len(data), 'coverage %.2f' % float((alpha > 96).mean()))
