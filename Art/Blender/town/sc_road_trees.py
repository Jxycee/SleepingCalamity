"""Far-tree impostor atlas for the Saltwake road (Q15): 4 broad storybook deciduous trees, orthographic side view.

Imported by sc_saltwake_road.py under --trees (run line there). Original design: a short trunk with a root flare and two limbs, a rounded
crown of overlapping B9 ivy leaf-mass cards (Assets/_Project/Content/Art/Town/B9/Grass_A/T_B9_Foliage_BaseColor.png cells), lumpy outline, soft top-light AO.
The cards are placed in 3D on a union of ellipsoids, projected orthographically (viewer on -Y) and composited back to front by depth, so the
atlas is the same card-crown method the rest of the town uses, seen from the side. Output (bottom-up rows, like Blender images):
  T_Road_FarTrees_BaseColor.png   2048 x 1024, 4 cells of 512 x 1024, sRGB, alpha = coverage, colour bled under transparent pixels
  T_Road_FarTrees_Normal.png      tangent-space crown normal seen from the front (R x right, G z up, B toward the viewer), OpenGL y-up
Cell layout: tree base at the cell's bottom centre (row 16, column 256 + 512 k); 64 px per metre (a cell is 8 x 16 m); the visible height
and crown width of each tree are printed and written to Road/SOURCES.md. Greens are the B9 atlas greens times a warm grey tint: never teal.
Unity use: crossed billboard cards seen at 30-600 m, scaled so a cell is 8 m wide x 16 m tall at scale 1.
"""
import os, math, random, zlib
import numpy as np
import bpy
import sc_b9_lib as L

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
B9 = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9', 'Grass_A', 'T_B9_Foliage_BaseColor.png')
PPM = 64.0                     # pixels per metre
CW, CH = 512, 1024
CELLS = [(0.0, 0.25, 0.25, 0.5), (0.25, 0.25, 0.5, 0.5)]   # B9 green leaf masses (u0, v0, u1, v1), bottom-up
DARK = (0.0, 0.0, 0.25, 0.25)
LIGHT = np.array((-0.25, -0.35, 0.9)); LIGHT = LIGHT / np.linalg.norm(LIGHT)

# name, trunk height, crown half width, crown half height, lumps, tint (warm grey multiply), lean
TREES = [
    ('round_oak',   2.5, 2.9, 3.7, 10, (1.00, 0.97, 0.90), 0.0),
    ('tall_oval',   3.0, 2.4, 4.4, 9, (0.96, 0.98, 0.88), 0.2),
    ('wide_spread', 2.1, 3.2, 2.9, 11, (1.02, 0.96, 0.88), -0.3),
    ('tall_round',  2.8, 2.7, 4.0, 9, (0.98, 0.96, 0.86), 0.1),
]

def smooth(x):
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)

class Canvas:
    def __init__(self):
        self.c = np.zeros((CH, CW, 3), np.float32)      # premultiplied colour
        self.n = np.zeros((CH, CW, 3), np.float32)      # premultiplied normal
        self.a = np.zeros((CH, CW), np.float32)

    def sprite(self, cell, cx, cy, s, ang, shade, nvec, alpha_mul=0.98):
        half = int(s * 0.72) + 2
        x0, x1 = max(0, int(cx) - half), min(CW, int(cx) + half + 1)
        y0, y1 = max(0, int(cy) - half), min(CH, int(cy) + half + 1)
        if x1 <= x0 or y1 <= y0:
            return
        Y, X = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        dx, dy = X - cx, Y - cy
        ca, sa = math.cos(-ang), math.sin(-ang)
        u = (dx * ca - dy * sa) / s + 0.5; v = (dx * sa + dy * ca) / s + 0.5
        ok = (u >= 0) & (u < 1) & (v >= 0) & (v < 1)
        ui = np.clip((u * (cell.shape[1] - 1)).astype(int), 0, cell.shape[1] - 1); vi = np.clip((v * (cell.shape[0] - 1)).astype(int), 0, cell.shape[0] - 1)
        px = cell[vi, ui]
        al = np.where(ok, px[..., 3] * alpha_mul, 0.0).astype(np.float32)
        col = px[..., :3] * np.asarray(shade, np.float32)
        nv = np.broadcast_to(np.asarray(nvec, np.float32), col.shape)
        self._over_box(y0, y1, x0, x1, col, nv, al)

    def _over_box(self, y0, y1, x0, x1, col, nrm, al):
        a = al[..., None]
        self.c[y0:y1, x0:x1] = col * a + self.c[y0:y1, x0:x1] * (1 - a)
        self.n[y0:y1, x0:x1] = nrm * a + self.n[y0:y1, x0:x1] * (1 - a)
        self.a[y0:y1, x0:x1] = al + self.a[y0:y1, x0:x1] * (1 - al)

    def limb(self, p0, p1, r0, r1, bark, R):
        """A tapered limb from p0 to p1 (px, px), radii r0 -> r1 (px): bark colour, cylinder normals."""
        (ax, ay), (bx, by) = p0, p1
        pad = int(max(r0, r1)) + 3
        x0, x1 = max(0, int(min(ax, bx)) - pad), min(CW, int(max(ax, bx)) + pad)
        y0, y1 = max(0, int(min(ay, by)) - pad), min(CH, int(max(ay, by)) + pad)
        Y, X = np.mgrid[y0:y1, x0:x1].astype(np.float32)
        d = np.array((bx - ax, by - ay), np.float32); ln2 = float(d @ d)
        t = np.clip(((X - ax) * d[0] + (Y - ay) * d[1]) / ln2, 0, 1)
        qx, qy = ax + t * d[0], ay + t * d[1]
        dist = np.hypot(X - qx, Y - qy); r = r0 + (r1 - r0) * t
        al = np.clip(r - dist + 0.5, 0, 1).astype(np.float32)
        side = np.clip((X - qx) / np.maximum(r, 1e-3), -1, 1)
        nz = np.sqrt(np.clip(1 - side * side, 0, 1))
        streak = 0.82 + 0.18 * np.sin(X * 0.9 + t * 7.0 + R.random() * 6)
        lit = 0.55 + 0.45 * np.clip(-side * 0.5 + nz * 0.8, 0, 1)
        col = np.stack([bark[0] * streak * lit, bark[1] * streak * lit, bark[2] * streak * lit], -1)
        nrm = np.stack([side * 0.9, np.zeros_like(side), nz], -1)
        self._over_box(y0, y1, x0, x1, col.astype(np.float32), nrm.astype(np.float32), al)

def surface_cards(blobs, trunk_h, R, density):
    out = []
    for bi, (c, r) in enumerate(blobs):
        area = 4 * math.pi * ((r[0] * r[1]) ** 1.6 + (r[0] * r[2]) ** 1.6 + (r[1] * r[2]) ** 1.6) / 3 ** (1 / 1.6)
        for _ in range(int(area * density)):
            d = np.array((R.gauss(0, 1), R.gauss(0, 1), R.gauss(0, 1))); d /= np.linalg.norm(d)
            p = np.array((c[0] + d[0] * r[0], c[1] + d[1] * r[1], c[2] + d[2] * r[2]))
            if p[2] < trunk_h * 0.9:
                continue
            if any(((p[0] - c2[0]) / r2[0]) ** 2 + ((p[1] - c2[1]) / r2[1]) ** 2 + ((p[2] - c2[2]) / r2[2]) ** 2 < 0.8
                   for bj, (c2, r2) in enumerate(blobs) if bj != bi):
                continue
            out.append(p)
    return out

def draw_tree(cell_img, spec, seed, B):
    name, trunk_h, W, Hh, lumps, tint, lean = spec
    R = random.Random(seed)
    cv = Canvas()
    zc = trunk_h + Hh * 0.82            # crown centre height (the crown dips low round the trunk)
    C = np.array((0.0, 0.0, zc))
    blobs = [((0.0, 0.0, zc), (W * 0.82, W * 0.78, Hh * 0.82))]
    for k in range(lumps):
        ang = 2 * math.pi * (k + R.uniform(-0.3, 0.3)) / lumps
        up = R.uniform(-0.15, 0.7)
        dxy = W * R.uniform(0.5, 0.78)
        blobs.append(((math.cos(ang) * dxy + lean * W * 0.1, math.sin(ang) * dxy * 0.8, zc + up * Hh * 0.8),
                      (W * R.uniform(0.34, 0.46), W * R.uniform(0.32, 0.44), Hh * R.uniform(0.32, 0.46))))
    for k in range(3):                  # crowning lumps on top
        blobs.append(((R.uniform(-0.4, 0.4) * W, R.uniform(-0.3, 0.3) * W, zc + Hh * R.uniform(0.55, 0.8)), (W * R.uniform(0.3, 0.42), W * 0.32, Hh * R.uniform(0.26, 0.36))))
    px = lambda p: (256.0 + p[0] * PPM, 16.0 + p[2] * PPM)
    def shade_of(p, back_dim=1.0):
        n = (p - C) / np.array((W, W, Hh)); n = n / (np.linalg.norm(n) + 1e-6)
        diff = max(0.0, float(n @ LIGHT))
        h = (p[2] - trunk_h) / (2 * Hh + 1e-6)
        ao = 0.62 + 0.38 * smooth(h * 1.15)
        k = (0.52 + 0.55 * diff) * ao * back_dim
        return n, np.array(tint) * k
    # inner dark layer (fills the see-through gaps with shadow green), then the trunk, then the shell back to front
    inner = []
    for _ in range(int(W * Hh * 9)):
        p = np.array((R.uniform(-0.8, 0.8) * W, R.uniform(-0.6, 0.6) * W, zc + R.uniform(-0.75, 0.6) * Hh))
        if sum(((p[0] - c[0]) / r[0]) ** 2 + ((p[1] - c[1]) / r[1]) ** 2 + ((p[2] - c[2]) / r[2]) ** 2 < 0.85 for c, r in blobs) and p[2] > trunk_h * 0.8:
            inner.append(p)
    for p in sorted(inner, key=lambda q: -q[1]):
        x, y = px(p)
        cv.sprite(B[2], x, y, R.uniform(1.0, 1.5) * PPM, R.uniform(0, 6.28), np.array(tint) * 0.42, (0, 0, 0.6))
    bark = (0.30, 0.23, 0.17)
    xb = 256.0
    cv.limb((xb + lean * 6, 10.0), (xb + lean * 10, 16 + trunk_h * PPM * 0.6), 0.52 * PPM * 0.5, 0.36 * PPM * 0.5, bark, R)          # trunk
    cv.limb((xb - 20, 6.0), (xb + 4, 16 + 0.7 * PPM), 0.55 * PPM * 0.5, 0.4 * PPM * 0.5, bark, R)                                     # root flare
    cv.limb((xb + lean * 10, 16 + trunk_h * PPM * 0.55), (xb - W * 0.35 * PPM, 16 + (trunk_h + Hh * 0.9) * PPM), 0.3 * PPM * 0.5, 0.16 * PPM * 0.5, bark, R)
    cv.limb((xb + lean * 10, 16 + trunk_h * PPM * 0.55), (xb + W * 0.3 * PPM, 16 + (trunk_h + Hh * 0.8) * PPM), 0.28 * PPM * 0.5, 0.15 * PPM * 0.5, bark, R)
    shell = surface_cards(blobs, trunk_h, R, 11.0)
    for p in sorted(shell, key=lambda q: -q[1]):
        n, sh = shade_of(p, 0.78 if p[1] > 0 else 1.0)
        x, y = px(p)
        cell = B[R.randrange(2)]
        cv.sprite(cell, x, y, R.uniform(0.7, 1.15) * PPM, R.uniform(-1.4, 1.4), sh, (n[0], n[2], max(0.2, -n[1])))
    return cv

def main(out_dir, exp_dir, export):
    atlas = L.load(B9)
    B = []
    for (u0, v0, u1, v1) in CELLS + [DARK]:
        B.append(atlas[int(v0 * 2048):int(v1 * 2048), int(u0 * 2048):int(u1 * 2048)].copy())
    rgb = np.zeros((CH, CW * 4, 3), np.float32); nrm = np.zeros_like(rgb); alp = np.zeros((CH, CW * 4), np.float32)
    info = []
    for k, spec in enumerate(TREES):
        cv = draw_tree(None, spec, 0x7EE5 ^ zlib.crc32(spec[0].encode()), B)
        a = np.clip(cv.a, 0, 1); den = np.maximum(a, 1e-4)[..., None]
        sl = slice(k * CW, (k + 1) * CW)
        rgb[:, sl] = np.where(a[..., None] > 1e-3, cv.c / den, 0.0); alp[:, sl] = a
        nv = np.where(a[..., None] > 1e-3, cv.n / den, np.array((0, 0, 1), np.float32))
        nv = nv / np.maximum(np.linalg.norm(nv, axis=-1, keepdims=True), 1e-4)
        nrm[:, sl] = nv * 0.5 + 0.5
        ys, xs = np.where(a > 0.5)
        info.append((spec[0], (ys.max() - 16) / PPM, (xs.max() - xs.min() + 1) / PPM, float((a > 0.5).mean())))
        # crown width only (rows above the trunk)
        crown = a[int(16 + spec[1] * PPM):] > 0.5
        cx = np.where(crown.any(0))[0]
        info[-1] = info[-1][:2] + ((cx.max() - cx.min() + 1) / PPM,) + info[-1][3:]
    rgb = np.clip(rgb, 0, 1); rgb_b = L.bleed(rgb, alp, sigma=5, passes=3)
    rgb_b = np.where(alp[..., None] > 0.5, rgb, rgb_b)
    for n_, h, w, cov in info:
        print('FARTREE %-12s visible height %.1f m  crown width %.1f m  coverage %.2f' % (n_, h, w, cov))
    # review preview: over a pale sky
    prev = rgb * alp[..., None] + np.array((0.78, 0.84, 0.92), np.float32) * (1 - alp[..., None])
    L.save(prev, os.path.join(out_dir, 'fartrees_preview.png'))
    if export:
        d = os.path.join(exp_dir, 'FarTrees'); os.makedirs(d, exist_ok=True)
        L.save(rgb_b, os.path.join(d, 'T_Road_FarTrees_BaseColor.png'), alpha=alp)
        L.save(nrm, os.path.join(d, 'T_Road_FarTrees_Normal.png'))
        print('EXPORT FarTrees', d)
    return info
