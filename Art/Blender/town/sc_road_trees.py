"""Far-tree impostor atlas for the Saltwake road (Q15): 4 broad storybook deciduous trees, orthographic side view.

Imported by sc_saltwake_road.py under --trees (run line there). Round 2 (2026-10-31): four distinct trees, each a stack of ragged lumpy leaf clumps
(B9 leaf-mass cells give leaf-scale alpha and detail) drawn low to high over a dark interior layer, with trunk and limbs and a few sky gaps:
  0 hedgerow oak   8.5 m high, crown 7.6 m wide, crown base 3.2 m
  1 field maple    9.1 m high, crown 6.1 m wide, crown base 2.6 m
  2 poplar column 13.8 m high, crown 4.8 m wide, crown base 1.5 m
  3 thorn clump    4.6 m high, crown 7.9 m wide, crown base 0 m
Each clump is shaded as a dome: top-left light, darker lower edge (AO) and halo on the clumps behind, darker lower crown; dome normals.
Output (bottom-up rows, like Blender images):
  T_Road_FarTrees_BaseColor.png   2048 x 1024, 4 cells of 512 x 1024, sRGB, alpha = coverage, colour bled under transparent pixels
  T_Road_FarTrees_Normal.png      tangent-space crown normal seen from the front (R x right, G z up, B toward the viewer), OpenGL y-up
Cell layout: tree base at the cell's bottom centre (row 16, column 256 + 512 k); 64 px per metre (a cell is 8 x 16 m); the visible height
and crown width of each tree are printed and written to Road/SOURCES.md. Greens are a yellow-green to olive ramp (g > r > b): never teal.
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

def smooth(x):
    x = min(1.0, max(0.0, x)); return x * x * (3 - 2 * x)

class Canvas:
    def __init__(self):
        self.c = np.zeros((CH, CW, 3), np.float32)      # premultiplied colour
        self.n = np.zeros((CH, CW, 3), np.float32)      # premultiplied normal
        self.a = np.zeros((CH, CW), np.float32)
        self.fa = np.zeros((CH, CW), np.float32)       # foliage-only coverage

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

# --- palette (sRGB file values, B9 greens; never teal: g > r > b everywhere)
RAMP = [(0.0, (0.15, 0.22, 0.08)), (0.35, (0.26, 0.37, 0.12)), (0.62, (0.40, 0.52, 0.17)), (0.85, (0.55, 0.65, 0.21)), (1.05, (0.68, 0.73, 0.27))]

def ramp(s):
    s = np.clip(s, 0, 1.05); out = np.zeros(s.shape + (3,), np.float32)
    for (a, ca), (b, cb) in zip(RAMP[:-1], RAMP[1:]):
        t = np.clip((s - a) / (b - a), 0, 1)[..., None]; m = ((s >= a) & (s <= b))[..., None]
        out = np.where(m, np.asarray(ca, np.float32) * (1 - t) + np.asarray(cb, np.float32) * t, out)
    return out

def clump(cv, B, R, cx, cz, rx, rz, tone=0.0, dark=1.0, rag=1.0, front=True, zspan=(0.0, 1.0)):
    """One leaf clump: a lumpy dome seen from the front. (cx, cz, rx, rz) in metres from the base. Alpha is ragged at leaf scale,
    shaded by a top-left light, a darker lower edge (AO) and a darker halo cast on the clumps behind it."""
    X0, Y0 = 256.0 + cx * PPM, 16.0 + cz * PPM
    ax, az = rx * PPM, rz * PPM
    x0, x1 = max(0, int(X0 - ax * 1.5)), min(CW, int(X0 + ax * 1.5) + 1)
    y0, y1 = max(0, int(Y0 - az * 1.5)), min(CH, int(Y0 + az * 1.5) + 1)
    Y, X = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    u = (X - X0) / ax; v = (Y - Y0) / az
    rn = np.sqrt(u * u + v * v); th = np.arctan2(v, u)
    ph = [R.uniform(0, 6.28) for _ in range(5)]
    rr = (1 + rag * (0.10 * np.sin(3 * th + ph[0]) + 0.07 * np.sin(5 * th + ph[1]) + 0.05 * np.sin(9 * th + ph[2])
                     + 0.035 * np.sin(17 * th + ph[3]) + 0.025 * np.sin(29 * th + ph[4])))
    cell = B[R.randrange(2)]; ch_, cw_ = cell.shape[:2]
    sc = 92.0 * R.uniform(0.9, 1.25); ox, oy = R.uniform(0, cw_), R.uniform(0, ch_)
    ix = ((X * cw_ / sc + ox) % cw_).astype(int).clip(0, cw_ - 1)
    iy = ((Y * ch_ / sc + oy) % ch_).astype(int).clip(0, ch_ - 1)
    tx = cell[iy, ix]
    lum = tx[..., :3] @ np.array((0.3, 0.55, 0.15), np.float32); lum = np.clip(lum / (lum.mean() + 1e-4), 0.55, 1.5)
    edge = 1 - rn / rr
    cov = np.clip(edge * 11 + (tx[..., 3] - 0.55) * 2.2, 0, 1)
    cov = cov * cov * (3 - 2 * cov)
    # cast shadow / AO on what is already there, strongest below and beside the clump
    halo = np.clip(1 - (rn / rr - 1.0) / 0.38, 0, 1) * (rn / rr >= 0.95) * (0.55 + 0.45 * np.clip(-v + 0.3, 0, 1))
    cur = cv.a[y0:y1, x0:x1]
    k = (1 - 0.42 * halo * (cur > 0.1))[..., None]
    cv.c[y0:y1, x0:x1] *= k
    # dome shading
    nz = np.sqrt(np.clip(1 - rn * rn, 0, 1)); nx, ny = np.clip(u, -1, 1) * 0.8, np.clip(v, -1, 1) * 0.8
    nn = np.sqrt(nx * nx + ny * ny + nz * nz + 1e-6); nx, ny, nz = nx / nn, ny / nn, nz / nn
    diff = np.clip(nx * LIGHT[0] + ny * 0.55 + nz * LIGHT[2], 0, 1)
    ao = 0.42 + 0.58 * np.clip((v + 1.0) / 1.7, 0, 1) ** 0.8          # darker at the lower edge
    ao = ao * (0.80 + 0.20 * np.clip(nz * 1.6, 0, 1))                  # and a little toward the silhouette
    rim = np.clip((-u * 0.7 + v * 0.8) * 1.2, 0, 1) * np.clip((rn / rr - 0.6) * 3, 0, 1) * 0.28
    sv = (0.20 + 0.62 * diff) * ao + rim + tone
    zt = np.clip(((Y - 16.0) / PPM - zspan[0]) / (zspan[1] - zspan[0]), 0, 1)
    sv = sv * (0.74 + 0.30 * zt) * dark * (0.88 + 0.22 * (lum - 1.0) + 0.12)
    col = ramp(sv)
    col = col * (0.9 + 0.1 * lum[..., None])
    nvec = np.stack([nx, ny, np.maximum(0.25, nz)], -1).astype(np.float32)
    nvec = nvec * 0.7 + np.stack([np.clip(u, -1, 1) * 0.5, np.clip(v, -1, 1) * 0.5, np.full_like(u, 0.7)], -1) * 0.3
    cv._over_box(y0, y1, x0, x1, col.astype(np.float32), nvec.astype(np.float32), cov.astype(np.float32))
    if front:
        cv.fa[y0:y1, x0:x1] = np.maximum(cv.fa[y0:y1, x0:x1], cov)

def hole(cv, R, cx, cz, rx, rz):
    """A small gap of sky through the crown (alpha knocked out with a ragged ellipse)."""
    X0, Y0 = 256.0 + cx * PPM, 16.0 + cz * PPM
    ax, az = rx * PPM, rz * PPM
    x0, x1 = int(X0 - ax * 1.4), int(X0 + ax * 1.4) + 1; y0, y1 = int(Y0 - az * 1.4), int(Y0 + az * 1.4) + 1
    Y, X = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    u = (X - X0) / ax; v = (Y - Y0) / az; th = np.arctan2(v, u)
    rr = 1 + 0.18 * np.sin(4 * th + R.uniform(0, 6)) + 0.1 * np.sin(11 * th + R.uniform(0, 6))
    m = np.clip((1 - np.sqrt(u * u + v * v) / rr) * 7, 0, 1)
    cv.a[y0:y1, x0:x1] *= (1 - m); cv.fa[y0:y1, x0:x1] *= (1 - m)

# name, [stems (x0,z0 -> x1,z1, r0 m, r1 m)], back clumps, front clumps, holes; clump = (cx, cz, rx, rz, tone)
TREES = {
    'hedgerow_oak': dict(xs=0.9,
        stems=[(0.0, 0.0, 0.1, 3.6, 0.46, 0.36),                (0.05, 3.0, -2.3, 5.0, 0.30, 0.14), (0.1, 3.2, 0.3, 5.9, 0.28, 0.14), (0.1, 3.0, 2.4, 4.7, 0.28, 0.13)],
        back=[(0.0, 5.7, 3.5, 1.8, -0.05), (-1.9, 4.7, 2.0, 1.2, -0.08), (2.0, 4.8, 2.0, 1.2, -0.08)],
        front=[(-3.0, 4.2, 0.95, 0.85, -0.02), (3.0, 4.4, 1.0, 0.85, 0.0), (-2.4, 5.4, 1.55, 1.25, 0.04), (2.5, 5.6, 1.5, 1.2, 0.08),
               (-0.9, 4.3, 1.4, 0.85, -0.03), (1.1, 4.1, 1.3, 0.8, 0.0),
               (-1.3, 6.8, 1.7, 1.2, 0.10), (0.9, 7.1, 1.6, 1.2, 0.14), (-3.1, 6.3, 0.9, 0.75, 0.06), (2.9, 6.8, 0.9, 0.7, 0.10)],
        holes=[(-0.3, 5.5, 0.28, 0.2), (1.9, 6.1, 0.22, 0.3), (-2.0, 6.0, 0.2, 0.22), (0.2, 6.3, 0.18, 0.2)]),
    'field_maple': dict(
        stems=[(0.0, 0.0, 0.1, 2.0, 0.30, 0.24), (0.1, 2.0, 0.45, 3.9, 0.24, 0.14), (0.15, 2.4, -0.8, 4.4, 0.14, 0.09)],
        back=[(0.35, 5.5, 2.4, 3.0, -0.08)],
        front=[(-0.7, 3.6, 1.7, 0.95, -0.03), (1.4, 3.9, 1.5, 0.9, 0.0), (0.2, 4.5, 1.3, 0.8, -0.04),
               (-1.2, 5.5, 1.6, 1.05, 0.05), (1.7, 5.7, 1.45, 1.0, 0.08), (0.3, 6.0, 1.2, 0.8, 0.03),
               (0.0, 7.4, 1.5, 1.0, 0.12), (1.0, 8.2, 1.0, 0.8, 0.16), (-0.8, 8.0, 0.9, 0.7, 0.13)],
        holes=[(0.1, 5.0, 0.2, 0.18)]),
    'poplar': dict(
        stems=[(0.0, 0.0, 0.0, 2.6, 0.26, 0.2)],
        back=[(0.0, 6.5, 1.5, 5.3, -0.08)],
        front=[(-0.2, 2.6, 1.1, 0.9, -0.05), (0.3, 3.5, 1.35, 1.0, -0.02), (-0.3, 4.6, 1.55, 1.1, 0.0), (0.35, 5.7, 1.65, 1.2, 0.03),
               (-0.35, 6.9, 1.7, 1.2, 0.05), (0.3, 8.0, 1.6, 1.2, 0.08), (-0.25, 9.1, 1.45, 1.1, 0.10),
               (0.2, 10.2, 1.25, 1.0, 0.12), (-0.1, 11.2, 1.0, 0.9, 0.14), (0.05, 12.2, 0.7, 0.9, 0.16), (0.0, 13.0, 0.4, 0.6, 0.18)],
        holes=[], rag=1.5),
    'thorn_clump': dict(xs=0.92,
        stems=[(-0.7, 0.0, -1.6, 2.2, 0.17, 0.1), (0.0, 0.0, 0.2, 2.8, 0.2, 0.12), (0.7, 0.0, 1.8, 2.0, 0.16, 0.1)],
        back=[(0.0, 2.3, 3.4, 1.8, -0.08)],
        front=[(-2.6, 1.2, 1.4, 1.1, -0.04), (2.7, 1.3, 1.4, 1.1, -0.02), (0.0, 1.0, 2.0, 0.95, -0.06),
               (-1.5, 2.5, 1.55, 1.2, 0.03), (1.6, 2.7, 1.5, 1.2, 0.06), (0.0, 3.4, 1.5, 1.0, 0.12),
               (-3.1, 2.2, 0.8, 0.8, 0.0), (3.1, 2.6, 0.75, 0.8, 0.02), (-0.2, 0.55, 1.2, 0.5, -0.08)],
        holes=[(-0.1, 2.2, 0.2, 0.2)], rag=1.3),
}
ORDER = ['hedgerow_oak', 'field_maple', 'poplar', 'thorn_clump']

def draw_tree(spec, seed, B):
    R = random.Random(seed)
    cv = Canvas()
    rag = spec.get('rag', 1.0); xs = spec.get('xs', 1.0)
    zs = (min(c[1] - c[3] for c in spec['front']), max(c[1] + c[3] for c in spec['front']))
    X_ = lambda c: (c[0] * xs, c[1], c[2] * xs) + tuple(c[3:])
    bark = (0.31, 0.24, 0.17)
    for (cx, cz, rx, rz, tone) in spec['back']:                       # dark interior mass, fills the gaps with shadow green
        clump(cv, B, R, cx * xs, cz, rx * xs, rz, tone, dark=0.62, rag=rag * 0.8, front=False, zspan=zs)
    for (x0, z0, x1, z1, r0, r1) in spec['stems']:
        cv.limb((256.0 + x0 * xs * PPM, 12.0 + z0 * PPM), (256.0 + x1 * xs * PPM, 16.0 + z1 * PPM), r0 * PPM, r1 * PPM, bark, R)
    for (cx, cz, rx, rz, tone) in sorted(spec['front'], key=lambda c: c[1]):    # low clumps first, upper ones overlap them
        clump(cv, B, R, cx * xs, cz, rx * xs, rz, tone, dark=1.0, rag=rag, zspan=zs)
    for h in spec['holes']:
        hole(cv, R, h[0] * xs, h[1], h[2], h[3])
    return cv

def main(out_dir, exp_dir, export):
    atlas = L.load(B9)
    B = []
    for (u0, v0, u1, v1) in CELLS + [DARK]:
        B.append(atlas[int(v0 * 2048):int(v1 * 2048), int(u0 * 2048):int(u1 * 2048)].copy())
    rgb = np.zeros((CH, CW * 4, 3), np.float32); nrm = np.zeros_like(rgb); alp = np.zeros((CH, CW * 4), np.float32)
    info = []
    for k, name in enumerate(ORDER):
        cv = draw_tree(TREES[name], 0x7EE5 ^ zlib.crc32(name.encode()), B)
        a = np.clip(cv.a, 0, 1); den = np.maximum(a, 1e-4)[..., None]
        sl = slice(k * CW, (k + 1) * CW)
        rgb[:, sl] = np.where(a[..., None] > 1e-3, cv.c / den, 0.0); alp[:, sl] = a
        nv = np.where(a[..., None] > 1e-3, cv.n / den, np.array((0, 0, 1), np.float32))
        nv = nv / np.maximum(np.linalg.norm(nv, axis=-1, keepdims=True), 1e-4)
        nrm[:, sl] = nv * 0.5 + 0.5
        ys, xs = np.where(a > 0.5); fy, fx = np.where(cv.fa > 0.5)
        info.append((name, (ys.max() - 16) / PPM, (fx.max() - fx.min() + 1) / PPM, (fy.min() - 16) / PPM))
    rgb = np.clip(rgb, 0, 1); rgb_b = L.bleed(rgb, alp, sigma=5, passes=3)
    rgb_b = np.where(alp[..., None] > 0.5, rgb, rgb_b)
    for n_, h, w, cb in info:
        print('FARTREE %-14s visible height %.1f m  crown width %.1f m  crown base %.1f m' % (n_, h, w, cb))
    # review preview: over a pale sky
    prev = rgb * alp[..., None] + np.array((0.78, 0.84, 0.92), np.float32) * (1 - alp[..., None])
    L.save(prev, os.path.join(out_dir, 'fartrees_preview.png'))
    if export:
        d = os.path.join(exp_dir, 'FarTrees'); os.makedirs(d, exist_ok=True)
        L.save(rgb_b, os.path.join(d, 'T_Road_FarTrees_BaseColor.png'), alpha=alp)
        L.save(nrm, os.path.join(d, 'T_Road_FarTrees_Normal.png'))
        print('EXPORT FarTrees', d)
    return info
