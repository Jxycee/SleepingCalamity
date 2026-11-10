# Strand texture for SC_HairCards (trellis venv python: numpy + cv2).
#   python hd_strand_tex.py <out.png> [--variants 8] [--tile 256] [--height 1024] [--strands 30] [--seed 11]
# One linear RGBA atlas, `variants` tiles side by side; a tile spans one strand card: x = across the card (card u 0..1), y = along it
# (row 0 = tip, bottom row = root, like Unity's v 0 at the bottom). Each tile draws ~`strands` hairs at 2x and downsamples:
#   R  brightness: per-hair 0.84..1.12 x a lengthwise flicker, gaps between hairs darker (fake depth / self-shadow)
#   G  hair id 0..1 (the shader jitters the highlight shift per hair, so highlights break into streaks)
#   B  across-hair slope 0..1 (0.5 flat): the shader bends the normal around the hair direction (rounded hairs)
#   A  coverage: near 1 at the root (no see-through at the scalp), hairs end at their own lengths and split apart toward the tip
import sys, argparse, numpy as np, cv2
ap = argparse.ArgumentParser(); ap.add_argument('out'); ap.add_argument('--variants', type=int, default=8); ap.add_argument('--tile', type=int, default=256)
ap.add_argument('--height', type=int, default=1024); ap.add_argument('--strands', type=int, default=30); ap.add_argument('--seed', type=int, default=11)
a = ap.parse_args(); rng = np.random.default_rng(a.seed); S = 2
TW, TH = a.tile * S, a.height * S
tiles = []
for k in range(a.variants):
    bright = np.zeros((TH, TW), np.float32); hid = np.zeros((TH, TW), np.float32); slope = np.full((TH, TW), 0.5, np.float32)
    cover = np.zeros((TH, TW), np.float32); depth = np.full((TH, TW), -1.0, np.float32)
    n = int(a.strands * rng.uniform(0.85, 1.2)); ys = np.arange(TH, dtype=np.float32); t = 1.0 - ys / (TH - 1)   # t: 0 root (bottom) .. 1 tip (top)
    order = rng.permutation(n)
    for j in order:
        x0 = (j + rng.uniform(0.1, 0.9)) / n * TW
        L = rng.uniform(0.62, 1.0) if rng.random() > 0.12 else rng.uniform(0.35, 0.62)        # where this hair ends (fraction of the card)
        drift = rng.uniform(-0.06, 0.06) * TW; wav = rng.uniform(0.004, 0.012) * TW; ph = rng.uniform(0, 6.28); fq = rng.uniform(1.5, 4.0)
        xc = x0 + drift * t ** 1.6 + wav * np.sin(t * fq * 6.28 + ph)                         # centre line along the card
        w0 = rng.uniform(2.0, 3.4) * TW / a.strands / 4.2                                     # hair half-width at the root (px at 2x)
        b = rng.uniform(0.84, 1.12); idv = rng.random(); z = rng.random()
        for yi in range(TH):
            tt = t[yi]
            if tt > L: continue
            taper = 1.0 - (tt / L) ** 3                                                       # pointed tip
            hw = max(0.6, w0 * (0.55 + 0.45 * taper)); c = xc[yi]
            xa, xb = int(max(0, np.floor(c - hw - 1))), int(min(TW - 1, np.ceil(c + hw + 1)))
            if xb <= xa: continue
            xs = np.arange(xa, xb + 1, dtype=np.float32); d = (xs - c) / hw
            cov = np.clip(1.2 - np.abs(d), 0, 1) * (0.35 + 0.65 * taper)
            m = (cov > 0.02) & (z > depth[yi, xa:xb + 1])                                     # random hair order: front hairs win
            if not m.any(): continue
            sl = slice(xa, xb + 1)
            cover[yi, sl] = np.maximum(cover[yi, sl], cov)
            bright[yi, sl] = np.where(m, b * (0.85 + 0.15 * np.cos(np.clip(d, -1, 1) * 1.57)), bright[yi, sl])
            hid[yi, sl] = np.where(m, idv, hid[yi, sl]); slope[yi, sl] = np.where(m, np.clip(0.5 + 0.5 * d, 0, 1), slope[yi, sl])
            depth[yi, sl] = np.where(m, z, depth[yi, sl])
    # root fill: the card body stays solid near the root, opening up toward the tip
    rootfill = np.clip(1.0 - t / 0.45, 0, 1)[:, None] ** 1.5
    gapdark = 0.76
    bright = np.where(cover > 0.05, bright, gapdark)
    A = np.clip(np.maximum(cover * 1.15, rootfill * 0.95), 0, 1)
    tile = np.dstack([bright / 1.2, hid, slope, A])
    tiles.append(cv2.resize(tile, (a.tile, a.height), interpolation=cv2.INTER_AREA))
atlas = np.hstack(tiles)
img = (np.clip(atlas, 0, 1) * 255 + 0.5).astype(np.uint8)
cv2.imwrite(a.out, img[..., [2, 1, 0, 3]])                                                    # cv2 wants BGRA
prev = np.dstack([img[..., 0]] * 3).astype(np.float32) * (img[..., 3:4] / 255.0) + 60 * (1 - img[..., 3:4] / 255.0)
cv2.imwrite(a.out.replace('.png', '_preview.png'), cv2.resize(prev.astype(np.uint8), (atlas.shape[1] // 2, atlas.shape[0] // 2)))
print('STRAND_TEX', a.out, img.shape, 'mean alpha %.2f' % (img[..., 3].mean() / 255))
