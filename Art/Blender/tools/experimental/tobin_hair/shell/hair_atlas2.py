# Strand textures for Tobin's shell + lock-card hair (run with any python that has numpy + cv2, e.g. C:/AI/trellis2/venv/Scripts/python.exe).
#   python hair_atlas2.py <out_dir>
# Writes 2048 px PNGs (drawn at 2x with anti-aliased strands, then downsampled, so no stair-steps):
#   Hair_Cards_{BaseColor (RGBA, alpha-cut tips), Normal, MetallicSmooth}: 8 lock variants side by side (u), root at the top (v=1), pointed tip at the bottom (v=0).
#   Hair_Shell_{BaseColor (opaque), Normal, MetallicSmooth}: tileable in u, LOCKS_PER_TILE clumps per tile with dark gaps between, strands run along v (root v=1 -> ends v=0).
import sys, os, numpy as np, cv2
out = sys.argv[1]; os.makedirs(out, exist_ok=True)
S = 2                      # supersample
N = 2048 * S
rng = np.random.default_rng(11)
ROOT = np.array([44, 31, 23], np.float32)      # sRGB, dark root / gap
MID = np.array([124, 92, 67], np.float32)      # chestnut
HI = np.array([178, 142, 108], np.float32)      # warm highlight
LOCKS_PER_TILE = 4

ROOTK = 0.62
def strand_color(v_rel, bright):
    # v_rel 0 = root, 1 = tip; darker at the root, a little lighter toward the tip
    base = MID * bright
    c = base * (ROOTK + (1 - ROOTK) * np.clip(v_rel * 1.6, 0, 1)) + (HI - MID) * max(0.0, bright - 1.0) * 1.2
    return np.clip(c + (HI - MID) * 0.25 * v_rel, 0, 255)

def draw_strand(col, alpha, height, pts, bright, thick, wrapW=None):
    # pts: (n,2) pixel coords from root to tip; drawn in short segments so colour follows v
    n = len(pts)
    for i in range(n - 1):
        v = i / (n - 1)
        c = strand_color(v, bright)
        t = max(1, int(round(thick * (1.0 - 0.55 * v ** 2))))
        a = pts[i:i + 2].astype(np.int32)
        offs = [0] if wrapW is None else [-wrapW, 0, wrapW]
        for o in offs:
            q = a.copy(); q[:, 0] += o
            cv2.polylines(col, [q], False, c.tolist(), t, cv2.LINE_AA)
            cv2.polylines(alpha, [q], False, 255, t, cv2.LINE_AA)
            cv2.polylines(height, [q], False, float(bright), t + 2, cv2.LINE_AA)

def finish(col, alpha, height, name, opaque):
    col = cv2.resize(col, (2048, 2048), interpolation=cv2.INTER_AREA)
    alpha = cv2.resize(alpha, (2048, 2048), interpolation=cv2.INTER_AREA)
    h = cv2.resize(height, (2048, 2048), interpolation=cv2.INTER_AREA)
    h = cv2.GaussianBlur(h, (0, 0), 1.2)
    gx = cv2.Sobel(h, cv2.CV_32F, 1, 0, ksize=3); gy = cv2.Sobel(h, cv2.CV_32F, 0, 1, ksize=3)
    k = 2.2
    nrm = np.dstack([-gx * k, gy * k, np.ones_like(h)]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
    nrm_img = ((nrm * 0.5 + 0.5) * 255).astype(np.uint8)
    # bleed colour into transparent texels so mips / alpha edges do not pick up black
    if not opaque:
        m = (alpha > 8).astype(np.uint8)
        fill = cv2.inpaint(col.astype(np.uint8), 1 - m, 3, cv2.INPAINT_TELEA) if False else None
        blur = cv2.blur(col * (alpha[..., None] / 255.0), (31, 31)); wsum = cv2.blur(alpha / 255.0, (31, 31))[..., None]
        bg = np.where(wsum > 1e-3, blur / np.maximum(wsum, 1e-3), MID * 0.8)
        a3 = (alpha / 255.0)[..., None]; col = col * np.minimum(1, a3 * 3) + bg * (1 - np.minimum(1, a3 * 3))
    rgb = np.clip(col, 0, 255).astype(np.uint8)
    if opaque:
        cv2.imwrite(os.path.join(out, name + '_BaseColor.png'), rgb[..., ::-1])
    else:
        cv2.imwrite(os.path.join(out, name + '_BaseColor.png'), np.dstack([rgb[..., ::-1], np.clip(alpha, 0, 255).astype(np.uint8)]))
    cv2.imwrite(os.path.join(out, name + '_Normal.png'), nrm_img[..., ::-1])
    # metallic R = 0, smoothness A: strands a little glossier on their crests, gaps matte
    sm = np.clip(0.16 + 0.14 * (h / max(h.max(), 1e-3)), 0, 1)   # low: glossier maps picked up blue-grey sky Fresnel on side locks in Unity
    ms = np.dstack([np.zeros_like(sm), np.zeros_like(sm), np.zeros_like(sm), sm]) * 255
    cv2.imwrite(os.path.join(out, name + '_MetallicSmooth.png'), ms.astype(np.uint8)[..., [2, 1, 0, 3]])
    print('wrote', name)

ROOTK = 0.86
# ---------------- cards: 8 lock variants, root at top of image (v=1), tip at bottom ----------------
col = np.zeros((N, N, 3), np.float32); alpha = np.zeros((N, N), np.float32); height = np.zeros((N, N), np.float32)
CW = N // 8
for k in range(8):
    x0 = k * CW; cx = x0 + CW / 2; half = CW * 0.40
    ns = 170
    tip_len = rng.uniform(0.86, 0.97)                 # where the longest strands end (fraction of height)
    lean = rng.uniform(-0.10, 0.10) * CW              # tip drifts sideways a little
    # solid lock body under the strands (cards read as locks, not combs): tapered polygon, softened edge
    body = np.zeros((N, N), np.float32); yy = np.linspace(0, tip_len * 0.9 * N, 60); hw = half * 0.62 * (1 - yy / (tip_len * 0.9 * N)) ** 0.8
    poly = np.concatenate([np.stack([cx - hw + lean * (yy / N) ** 1.5, yy], 1), np.stack([cx + hw + lean * (yy / N) ** 1.5, yy], 1)[::-1]]).astype(np.int32)
    cv2.fillPoly(body, [poly], 1.0, cv2.LINE_AA); body = cv2.GaussianBlur(body, (0, 0), 6 * S)
    col[:] = col + (MID * 0.78 - col) * body[..., None] * (alpha[..., None] < 1); alpha = np.maximum(alpha, body * 255); height += body * 0.4
    for s in range(ns):
        f = rng.uniform(-1, 1)                         # across-lock position at the root
        endf = tip_len * (1.0 - 0.38 * abs(f) ** 1.5) * rng.uniform(0.86, 1.0)   # edge strands end earlier: pointed tip
        n = 40; ys = np.linspace(0, endf * N, n)
        t = ys / N
        conv = (1.0 - t / max(endf, 1e-3) * 0.85)      # strands converge toward the tip
        wob = np.sin(t * rng.uniform(5, 11) + rng.uniform(0, 6.3)) * rng.uniform(2, 7) * S
        xs = cx + f * half * conv + lean * t ** 1.5 + wob
        pts = np.stack([xs, ys], 1)
        bright = rng.choice([rng.uniform(0.82, 1.0), rng.uniform(1.0, 1.22)], p=[0.6, 0.4])
        draw_strand(col, alpha, height, pts, bright, rng.uniform(7, 13) * S / 2)
# rounded-lock shading: lighter down the middle of each lock, darker toward its edges (reads as a clump, not flat straw)
xx = (np.arange(N) % CW - CW / 2) / (CW * 0.40); prof = np.clip(0.80 + 0.32 * np.cos(np.clip(xx, -1, 1) * np.pi / 2) ** 2, 0, 1.15)
col *= prof[None, :, None]
# image row 0 is the top = v 1 (root)
finish(col, alpha, height, 'Hair_Cards', opaque=False)

ROOTK = 0.70
# ---------------- shell: tileable in u, LOCKS_PER_TILE clumps with dark gaps ----------------
col = np.zeros((N, N, 3), np.float32); col[:] = ROOT * 2.3; alpha = np.zeros((N, N), np.float32); height = np.zeros((N, N), np.float32)
LW = N / LOCKS_PER_TILE
for L in range(LOCKS_PER_TILE):
    cx = (L + 0.5) * LW
    for s in range(420):
        f = np.clip(rng.normal(0, 0.55), -1.1, 1.1)       # dense at the lock centre, sparse at its edges -> dark gaps
        n = 48; ys = np.linspace(0, N, n); t = ys / N
        conv = 1.0 - 0.45 * t ** 2                     # lock narrows toward its end (shell tips are pointed in geometry)
        wob = np.sin(t * rng.uniform(4, 9) + rng.uniform(0, 6.3)) * rng.uniform(3, 10) * S
        xs = cx + f * LW * 0.46 * conv + wob
        pts = np.stack([xs, ys], 1)
        bright = rng.choice([rng.uniform(0.8, 1.0), rng.uniform(1.0, 1.22)], p=[0.62, 0.38]) * (1.0 - 0.18 * abs(f))
        draw_strand(col, alpha, height, pts, bright, rng.uniform(8, 15) * S / 2, wrapW=N)
xx = (np.arange(N) % LW - LW / 2) / (LW / 2); prof = 0.86 + 0.24 * np.cos(xx * np.pi / 2) ** 2
col *= prof[None, :, None]
finish(col, alpha, height, 'Hair_Shell', opaque=True)
