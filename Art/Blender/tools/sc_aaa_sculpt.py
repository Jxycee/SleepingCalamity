"""sc_aaa_sculpt - headless sculpt-brush emulation for sc_aaa_kit highs (numpy + KD-tree).

Blender's real sculpt brushes do not run in `blender -b` (sculpt.brush_stroke polls for a 3D viewport and
sizes brushes in screen pixels), so these brushes reproduce the five behaviours artists use, with true
falloff curves, in object space and metres:
  trim   planar flatten toward a plane (Trim / Flatten / Scrape): dressed-stone facets, planished copper
  crease pull toward a stroke line and sink it (Crease + Pinch): cut lines, seams, folds
  clay   add a flat-topped layer along the normal (Clay Strips): built-up form, glaze pooling
  draw   sharp-falloff inward stroke (Draw Sharp): scratches, tool cuts
Each recipe returns an erosion callback for make_part (combine with sc_aaa_kit.chain).
"""
import numpy as np
from mathutils import kdtree
import sc_aaa_kit as K

def falloff(t, kind='smooth'):
    t = np.clip(t, 0.0, 1.0)
    if kind == 'smooth': return 1 - t * t * (3 - 2 * t)
    if kind == 'sharp': return (1 - t) ** 3
    if kind == 'constant': return (t < 1).astype(float)
    if kind == 'sphere': return np.sqrt(np.clip(1 - t * t, 0, 1))
    return 1 - t

class Sculpt:
    def __init__(self, ob):
        self.ob = ob
        self.co, self.no = K.mesh_arrays(ob)
        self.kd = kdtree.KDTree(len(self.co))
        for i, c in enumerate(self.co): self.kd.insert(c, i)
        self.kd.balance()
    def near(self, c, r):
        h = self.kd.find_range(c, r)
        if not h: return np.zeros(0, int), np.zeros(0)
        return np.array([x[1] for x in h]), np.array([x[2] for x in h])
    def trim(self, c, n, r, depth, fall='smooth', hard=0.6):
        """Flatten verts above the plane through (c - n*depth) with normal n, inside radius r.
        hard 0..1: how much of the radius is fully flattened before the falloff starts (crisper facet edge)."""
        idx, d = self.near(c, r)
        if not len(idx): return
        n = n / (np.linalg.norm(n) + 1e-9); p0 = c - n * depth
        h = (self.co[idx] - p0) @ n
        w = falloff(np.clip((d / r - hard) / max(1 - hard, 1e-3), 0, 1), fall)
        m = h > 0
        self.co[idx[m]] -= (n[None, :] * (h[m] * w[m])[:, None])
    def crease(self, a, b, r, depth, pinch=0.4):
        """Sink a groove along segment a-b and pull its flanks toward the line."""
        a, b = np.asarray(a, float), np.asarray(b, float); L = np.linalg.norm(b - a) + 1e-9; t_ = (b - a) / L
        idx, _ = self.near((a + b) / 2, L / 2 + r)
        if not len(idx): return
        v = self.co[idx] - a; s = np.clip(v @ t_, 0, L); q = a + s[:, None] * t_
        off = self.co[idx] - q; dist = np.linalg.norm(off, axis=1)
        w = falloff(dist / r, 'sharp'); w[dist > r] = 0
        self.co[idx] -= self.no[idx] * (depth * w)[:, None]
        self.co[idx] -= off * (pinch * w)[:, None]
    def clay(self, c, r, height):
        idx, d = self.near(c, r)
        if not len(idx): return
        w = np.minimum(1.0, 1.6 * falloff(d / r, 'smooth'))
        self.co[idx] += self.no[idx] * (height * w)[:, None]
    def draw(self, a, b, r, depth):
        self.crease(a, b, r, depth, pinch=0.0)
    def done(self):
        K.set_co(self.ob, self.co)

def _pick(sc, count, rng, where=None):
    cand = np.arange(len(sc.co)) if where is None else np.nonzero(where)[0]
    return rng.choice(cand, size=min(count, len(cand)), replace=False)

def chisel(count=60, r=(0.012, 0.03), depth=(0.0008, 0.002), tilt=0.35, seed=0, rim=None):
    """Dressed stone: overlapping planar facets over the surface (coarse form break-up instead of fine noise).
    rim=(z, count): extra tilted facets along a rim height (a chiselled, faceted rim edge)."""
    def f(ob):
        sc = Sculpt(ob); rng = np.random.default_rng(seed)
        for i in _pick(sc, count, rng):
            n = sc.no[i] + rng.normal(size=3) * tilt
            sc.trim(sc.co[i], n, rng.uniform(*r), rng.uniform(*depth), hard=rng.uniform(0.4, 0.75))
        if rim:
            z, k = rim
            band = np.abs(sc.co[:, 2] - z) < 0.006
            for i in _pick(sc, k, rng, band):
                out = sc.no[i].copy(); out[2] = 0; out /= np.linalg.norm(out) + 1e-9
                n = out + np.array([0, 0, 1.0]) * rng.uniform(0.6, 1.4)
                sc.trim(sc.co[i], n, rng.uniform(0.01, 0.02), rng.uniform(0.0006, 0.0015), hard=0.6)
        sc.done()
    return f

def planish(count=1400, r=(0.006, 0.011), depth=(0.00005, 0.00014), seed=0):
    """Planished copper: hundreds of overlapping flat hammer facets (a planishing hammer flattens,
    it does not dent), each a crisp-edged plane slightly off the surface normal."""
    def f(ob):
        sc = Sculpt(ob); rng = np.random.default_rng(seed)
        for i in _pick(sc, count, rng):
            n = sc.no[i] + rng.normal(size=3) * 0.08
            sc.trim(sc.co[i], n, rng.uniform(*r), rng.uniform(*depth), hard=0.3)
        sc.done()
    return f


def handbuilt(dabs=120, r=(0.008, 0.02), height=(-0.0012, 0.0016), drags=60, drag_len=(0.015, 0.04), seed=0, zr=None):
    """Hand-built / coil-and-slab clay: uneven clay dabs (pressed and added) plus short vertical
    finger drags, only on outward-facing walls (and within zr=(zmin, zmax) when given)."""
    def f(ob):
        sc = Sculpt(ob); rng = np.random.default_rng(seed)
        side = np.abs(sc.no[:, 2]) < 0.6
        if zr: side &= (sc.co[:, 2] > zr[0]) & (sc.co[:, 2] < zr[1])
        for i in _pick(sc, dabs, rng, side):
            sc.clay(sc.co[i].copy(), rng.uniform(*r), rng.uniform(*height))
        for i in _pick(sc, drags, rng, side):
            a = sc.co[i].copy(); L = rng.uniform(*drag_len)
            b = a + np.array([rng.normal() * 0.004, rng.normal() * 0.004, -L])
            sc.crease(a, b, rng.uniform(0.003, 0.006), rng.uniform(0.0003, 0.0008), pinch=0.15)
        sc.done()
    return f
