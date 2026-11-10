"""Procedural hair-card atlas: V variants side by side (each W px wide, H px tall), root at the top (v=1 in Blender/Unity UV, image row 0).
Writes <out>/Hair_Cards_BaseColor.png (RGBA: colour + alpha cutout with tapered, uneven tips), <out>/Hair_Cards_Normal.png (flat + fine strand ripple), <out>/Hair_Cards_MetallicSmooth.png.
usage (trellis2 venv python):  python hair_atlas.py <out_dir> [variants=8] [W=256] [H=2048] [seed=3]"""
import sys, os, numpy as np, cv2
out = sys.argv[1]; V = int(sys.argv[2]) if len(sys.argv) > 2 else 8; W = int(sys.argv[3]) if len(sys.argv) > 3 else 256; H = int(sys.argv[4]) if len(sys.argv) > 4 else 2048
rng = np.random.default_rng(int(sys.argv[5]) if len(sys.argv) > 5 else 3); os.makedirs(out, exist_ok=True)
BASE = np.array([0.34, 0.205, 0.115])            # chestnut (linear-ish sRGB values 0..1)
col = np.zeros((H, W * V, 3), np.float32); alp = np.zeros((H, W * V), np.float32); hgt = np.zeros((H, W * V), np.float32)
for k in range(V):
    x_off = k * W; n = int(os.environ.get('HA_N', 16))
    for s in range(n):
        x0 = rng.uniform(0.06, 0.94) * W; edge = abs(x0 / W - 0.5) * 2                       # 0 centre .. 1 edge
        L = int(H * rng.uniform(0.55, 0.98) * (1 - 0.28 * edge ** 1.5))                       # edge strands are shorter -> pointed clump tip
        amp = rng.uniform(2.0, 7.0); fr = rng.uniform(0.002, 0.006); ph = rng.uniform(0, 6.28); drift = rng.uniform(-0.03, 0.03) * W
        ys = np.arange(0, L, 3); xs = x0 + amp * np.sin(ys * fr + ph) + drift * ys / H
        pts = np.stack([xs + x_off, ys], 1).astype(np.float32).reshape(-1, 1, 2)
        shade = rng.uniform(0.65, 1.3); tint = BASE * shade * np.array([1.0, rng.uniform(0.92, 1.08), rng.uniform(0.9, 1.12)])
        m = np.zeros((H, W * V), np.uint8); cv2.polylines(m, [np.round(pts * 16).astype(np.int32)], False, 255, int(rng.integers(int(os.environ.get('HA_T0', 9)), int(os.environ.get('HA_T1', 16)))), cv2.LINE_AA, shift=4)
        mf = m.astype(np.float32) / 255
        t = np.clip(np.arange(H)[:, None] / max(L, 1), 0, 1)                                  # 0 root .. 1 tip of this strand
        shaded = tint[None, None, :] * (0.72 + 0.5 * np.exp(-((np.arange(H)[:, None] / H - 0.38) / 0.14) ** 2))[:, :, None]   # root darker, gloss band
        sel = mf > alp                                                                        # painter: newer strand wins where it is more opaque
        col[sel] = np.broadcast_to(shaded, (H, W * V, 3))[sel]; alp = np.maximum(alp, mf)
        hgt = np.maximum(hgt, mf * (0.5 + 0.5 * rng.random()))
# soften and taper the card tip: alpha fades over the last 12 percent of each strand length is already implicit; add a global tip fade
yy = np.arange(H)[:, None] / H; alp = alp * np.clip((1.0 - yy) * 14, 0, 1) ** 0.6
alp = cv2.GaussianBlur(alp, (0, 0), 0.8); alp = np.clip(alp * 1.25, 0, 1)
# fill colour under transparent texels with a local average so mips do not bleed grey
fill = cv2.GaussianBlur(col, (0, 0), 6) / np.maximum(cv2.GaussianBlur(alp, (0, 0), 6)[..., None], 1e-3); col = np.where(alp[..., None] > 0.05, col / np.maximum(alp[..., None], 1e-3) * 1.0, fill)
col = np.clip(col, 0, 1); rgba = np.dstack([col[..., ::-1], alp]); cv2.imwrite(os.path.join(out, 'Hair_Cards_BaseColor.png'), (rgba * 255).astype(np.uint8))
gx = cv2.Sobel(cv2.GaussianBlur(hgt, (0, 0), 0.7), cv2.CV_32F, 1, 0, ksize=3); nz = np.ones_like(gx); nrm = np.dstack([-gx * 2.2, np.zeros_like(gx), nz]); nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
cv2.imwrite(os.path.join(out, 'Hair_Cards_Normal.png'), ((nrm * 0.5 + 0.5)[..., ::-1] * 255).astype(np.uint8))
ms = np.zeros((H, W * V, 4), np.uint8); ms[..., 2] = 0; ms[..., 3] = 120                    # metallic 0 (R), smoothness 0.47 (A); BGRA
cv2.imwrite(os.path.join(out, 'Hair_Cards_MetallicSmooth.png'), ms)
prev = (np.dstack([col, alp])[:, :, :3] * alp[..., None] + 0.55 * (1 - alp[..., None])); cv2.imwrite(os.path.join(out, 'atlas_preview.jpg'), cv2.resize((prev[..., ::-1] * 255).astype(np.uint8), (W * V // 2, H // 4)))
print('atlas', W * V, H)
