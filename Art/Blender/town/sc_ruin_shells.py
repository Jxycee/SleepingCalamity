"""RuinShells: taller ruin shells (Mid, Tall) for Hollowmere's 36 house lots, plus three small props (Hollowmere world build, Area 1 Run 7).

Why: the low ruin kit (sc_ruin_kit.py) leaves the square reading as empty plaza with trees. Design DH1: "the chimneys stayed: every ruined lot
keeps its hearth and chimney stack; the square is a ring of lonely chimneys and doorsteps". These shells are whole replacement ruins in the same
family, so they keep the low kit's contract (see its docstring): root empty Ruin_<C>_<V>_<Level>, children Footing, Chimney, FOOTPRINT (flat S x S
x 0.1 m box, hidden by the builder), SOCKET_Doorstep, SOCKET_Smoke; slots FK_Sandstone / FK_ChimneyStone / FK_PlinthJoint / FK_Oak; UV0 metre box
UVs at 2 m per tile; CORNER colour attribute 'wear' (R grime/char, G moss, B polish); Blender z up, front (doorstep) -Y, export -Z forward / Y up.
It re-uses sc_ruin_kit.py by executing its source without the trailing main() call (the kit runs main() at import), so Ruin, Geo, soft_box, the
doorstep, the chimney stack builder, the materials and the wear function are the kit's own. Door position and chimney parameters come from the same
seeds as the base variant (Ruin_<C>_<V>).

Base variant A / B / C changes which end holds the gable (and the chimney) and which wall stands:
  A: gable on the back (+Y), tall long wall on the left (-X), door in the low front end wall (jambs and lintel stand)
  B: gable on the left (-X), tall long wall on the back (+Y), door in the low front long wall
  C: gable on the right (+X), tall long wall is the front (-Y) with the door opening in it, low back wall
Tall: gable to its apex (S 5.5 / M 6.5 / L 7.5 m) with the stack built in and 1.2-1.8 m above it, hearth at its foot, L: second-storey window in
the gable; long wall 3-3.6 m with 1-2 windows; opposite wall low; door frame with lintel; 2-3 charred beams leaning to the floor; rubble.
Mid: stack and hearth alone (S 4.6 / M 5.2 / L 5.9 m) with a 2-3 m gable stub either side, other walls 0.8-2.2 m with one window sill, one corner
pier at about 3 m, one fallen beam, rubble.
Collision: the low kit has none (its FOOTPRINT box is the only collider shape), so the shells add simple box meshes FOOTPRINT_COL_<n> (every wall
section, jamb, pier and the stack). The prefix FOOTPRINT is what HollowmereBuilder.PlaceRuin hides (renderer disabled), so they never draw.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_ruin_shells.py -- --out <scratch> [--only Ruin_M_B_Tall,Headstone_A]
      [--export] [--no-render] [--samples 20]
  --export writes Art/Staging/area1-world/shells/<Name>.fbx (path_mode STRIP). Renders <out>/shells_sheet.png (all 18, three columns A B C, rows
  S Tall, S Mid, M Tall, M Mid, L Tall, L Mid, 1000 px wide) and <out>/props_sheet.png.
"""
import sys, os, math, random, zlib
HERE = os.path.dirname(os.path.abspath(__file__))
KIT = os.path.join(HERE, 'sc_ruin_kit.py')
_src = open(KIT, encoding='utf-8').read().rstrip()
assert _src.endswith('main()'), 'sc_ruin_kit.py no longer ends with main()'
# Round 2 chimney: proud and chunky (W 1.05-1.2 m at the base tapering to 0.85-0.95, two blocks wide per course, corbelled cap on all). These are
# text patches of the kit source held in memory only; the kit file on disk and its own variants are untouched.
for _a, _b in (("W1, W2 = W0 * 0.72, W0 * 0.54; D1, D2 = D0 * 0.82, D0 * 0.68", "W1, W2 = W0 * 0.80, c['Wtop']; D1, D2 = D0 * 0.90, D0 * 0.74"),
               ("cap = v in 'AC'", "cap = True"),
               ("tu = {'S': 0.17, 'M': 0.19, 'L': 0.21}[self.cls]", "tu = {'S': 0.27, 'M': 0.27, 'L': 0.28}[self.cls]"),
               # round 3: smaller courses on the stack (0.25-0.35 m tall, 0.3-0.55 m long), drip course proud of the shaft
               ("h = min(R.uniform(0.27, 0.40) * (0.9 + 0.1 * B), z1 - z)", "h = min(R.uniform(0.25, 0.35), z1 - z)"),
               ("seg(x1 - x0, 0.38 * B, 0.68 * B, cuts) if arch else seg(x1 - x0, 0.22 * B, 0.36 * B, cuts)", "seg(x1 - x0, 0.30, 0.52, cuts) if arch else seg(x1 - x0, 0.30, 0.50, cuts)"),
               ("seg(y1 - y0, 0.34 * B, 0.6 * B) if arch else seg(y1 - y0, 0.2 * B, 0.34 * B)", "seg(y1 - y0, 0.30, 0.50) if arch else seg(y1 - y0, 0.30, 0.48)"),
               ("            g.add(vs, fs, mat)\n\n        def course(", "            if not last:\n                fs = fs[1:]            # soft_box(cuts=0) face 0 is the top, hidden by the next course\n            g.add(vs, fs, mat)\n\n        def course("),
               ("soft_box((sx - 0.035, sy - R.uniform(0.0, 0.03), h - 0.02)", "soft_box((sx + 0.03, sy - R.uniform(0.0, 0.03), h + 0.03)"),
               ("gr = 0.05 if j == n - 2 else 0.10", "gr = 0.07 if j == n - 2 else 0.15"),
               # round 3b: the per-course jitter moved side-wall stones in their thickness direction (+-7 cm), so neighbouring stones stood at different depths and the low
               # morning sun shadowed every other one (the checker of dark holes on the stacks). Jitter now runs along the wall, and the yaw is smaller.
               ("xf = place(cx + xl + tl * zc + jx, y, zc, math.radians(R.uniform(-2.5, 2.5)), math.radians(R.uniform(-1.5, 1.5)), tilt)",
                "xf = place(cx + xl + tl * zc + (jx if sx >= sy else 0.0), y + (jx if sx < sy else 0.0), zc, math.radians(R.uniform(-0.4, 0.4)), math.radians(R.uniform(-0.3, 0.3)), tilt)"),
               # ... and the sun grazes the stack faces in the morning, so per-stone orientation noise (yaw, tilt, 1.8 cm vertex wobble = a 3 degree face normal spread)
               # swung the shading stone by stone. The stack keeps its relief from the normal map; the stones are nearly flat-faced.
               ("tilt = c['lean'] + math.radians(R.uniform(-1.5, 1.5))", "tilt = c['lean'] + math.radians(R.uniform(-0.4, 0.4))"),
               ("soft=R.uniform(0.07, 0.15), wob=0.018,", "soft=R.uniform(0.07, 0.15), wob=0.005,")):
    assert _a in _src, _a
    _src = _src.replace(_a, _b)
K = {'__name__': 'sc_ruin_kit', '__file__': KIT}
exec(compile(_src[:-len('main()')], KIT, 'exec'), K)          # the kit's functions without running its main()

import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix, noise
Ruin, Geo, soft_box, place, L = K['Ruin'], K['Geo'], K['soft_box'], K['place'], K['L']
# plaster slot: FK_Plaster is the facade kit's own slot name (BeautyCornerArt.BuildFacadeMaterials already maps it), B1 plaster tileable, no new texture
K['SLOTS'].append('plaster')
_old_slot_mats = K['slot_mats']
K['slot_mats'] = lambda: _old_slot_mats() + [K['tile_mat']('FK_Plaster', 'B1_town/plaster', tint=(1.0, 0.97, 0.90), normal=0.55)]
FOOT_W, BLOCK, CLASSES, SLOTS = K['FOOT_W'], K['BLOCK'], K['CLASSES'], K['SLOTS']
OUT, SAMPLES, EXPORT, RENDER, REPO = K['OUT'], K['SAMPLES'], K['EXPORT'], K['RENDER'], K['REPO']
STAGE_DIR = os.path.join(REPO, 'Art', 'Staging', 'area1-world', 'shells')
only_arg = K['arg']('--only')
DSCALE = float(K['arg']('--dscale', '1.0')); CAMZ = float(K['arg']('--camz', '1.75')); ANG = float(K['arg']('--ang', '35'))
SHEETNAME = K['arg']('--sheet', 'shells_sheet.png')
rad = math.radians
SIZES = ['S', 'M', 'L']
SHELL_NAMES = ['Ruin_%s_%s_%s' % (c, v, lv) for lv in ('Tall', 'Mid') for c in SIZES for v in 'ABC']
PROP_NAMES = ['Headstone_A', 'Headstone_B', 'Headstone_C', 'Signpost_A', 'Hatch_Oak']
WANT = only_arg.split(',') if only_arg else SHELL_NAMES + PROP_NAMES


def pick_faces(vs, fs, keep):
    """Keep only faces by index (and drop the verts nothing uses)."""
    fs = [fs[i] for i in keep]; used = sorted({i for f in fs for i in f}); m = {o: n for n, o in enumerate(used)}
    return [vs[i] for i in used], [tuple(m[i] for i in f) for f in fs]


def sstep(x):
    x = max(0.0, min(1.0, x)); return x * x * (3 - 2 * x)


# ============================================================================================ the shell
class Shell(Ruin):
    def __init__(self, base, level):
        super().__init__(base)
        self.level = level; self.base = base
        self.Q = random.Random(self.seed ^ (0x7A11 if level == 'Tall' else 0x31D))
        self.wall_info = {}; self.cols = []; self.rot = 0.0
        self.hearth = (1e3, 1e3); self.hearth_c = (1e3, 1e3)
        self.open_rects = []; self.pot_xy = None; self.panels = []

    # ---- wear: the kit's own, with the chimney's soot and the hearth mapped through the gable rotation, plus fire-black high on the walls
    def wear_at(self, p, n, slot):
        rot = self.rot; sup0 = super().wear_at
        # round 3b: the shells' own WearLit materials draw the mask strongly (_SC_WEARGB), so the kit's block-to-block grime (piece_v) is cut to a third: at full strength it
        # made a checker of dark and light stones.
        pv = self.piece_v
        def sup(*a_):
            self.piece_v = pv * 0.33
            try: return sup0(*a_)
            finally: self.piece_v = pv
        if rot:
            Mi = Matrix.Rotation(-rot, 3, 'Z'); pu = Mi @ p; nu = Mi @ n
            ch = self.chim; cxz = ch['cx'] + math.tan(ch['lean']) * max(pu.z, 0.0)
            inreg = abs(pu.x - cxz) < ch['W0'] / 2 + 0.3 and pu.y > ch['front'] - 0.45 and pu.z > -0.1
            if inreg:
                saved = self.hearth; self.hearth = self.hearth_c
                try: res = sup(pu, nu, slot)
                finally: self.hearth = saved
            else:
                sv = ch['front']; ch['front'] = 1e9
                try: res = sup(p, n, slot)
                finally: ch['front'] = sv
        else:
            res = sup(p, n, slot)
        r, gm = res[0], res[1] * 0.3
        if slot == 'oak':
            gm = 0.0
        if slot != 'oak' and self.chim_soot(p)[0] is not None and p.z > 1.5:
            gm *= 0.25                                    # the shaft of a stack is sooted, not mossed
        if slot == 'plaster':                             # warm off-white limewash: no moss; soot and char only at the panel edges (against the timbers)
            r = 0.05 + 0.05 * (0.5 + 0.5 * noise.noise(p * 2.1 + self.nofs)); gm = 0.0
            for (c_, d_, hw, hh) in self.panels:
                q = p - c_
                if abs(q.z) > hh + 0.06: continue
                dt = abs(q.dot(d_))
                if dt > hw + 0.06: continue
                if abs(q.x * d_.y - q.y * d_.x) > 0.3: continue            # not in this panel's plane
                de = min(hw - dt, hh - abs(q.z))
                r = max(r, 0.8 * max(0.0, 1.0 - max(de, 0.0) / 0.24) * (0.8 + 0.4 * noise.noise(p * 4.0 + self.nofs)))
                break
        if slot == 'oak':                                 # charred: the strong wear shader (smoothstep 0.12-0.55) turns 0.38 into about 70% char, a dark warm brown with the grain showing through
            r = 0.38
        if n.z > 0.5 and p.z > 1.0 and slot in ('oak', 'stone', 'stone_dk'):     # broken tops take the fire hardest
            r = max(r, min(0.92, 0.45 + 0.10 * (p.z - 1.0)))
        if slot != 'plaster':
            r = max(r, min(0.60, 0.14 * max(0.0, p.z - 0.9)) * (0.8 + 0.4 * noise.noise(p * 1.7 + self.nofs)))
        # char around window and door openings (the fire drew through them)
        if slot not in ('core', 'plaster'):
            for (a_, d_, o_, oa, ob, z0, z1) in self.open_rects:
                q = p - a_
                if abs(q.dot(o_)) > 0.55:
                    continue
                t_ = q.dot(d_)
                dt = max(oa - t_, 0.0, t_ - ob); dz = max(z0 - p.z, 0.0, (p.z - z1) if z1 < 1e8 else 0.0)
                dd = math.hypot(dt, dz)
                if dd < 0.6:
                    r = max(r, 0.85 * (1.0 - dd / 0.6) * (0.8 + 0.3 * noise.noise(p * 3.0 + self.nofs)))
        # moss and damp stain on the lower walls: strong, low-frequency, highest at the foot, fading by 1.5-2 m, broken up by noise
        if slot in ('stone', 'stone_dk') and n.z < 0.6:
            fade = max(0.0, 1.0 - max(0.0, p.z - 0.1) / (1.5 + 0.5 * noise.noise(p * 0.5 + self.nofs)))
            lf = 0.5 + 0.5 * noise.noise(p * Vector((0.45, 0.45, 0.9)) + self.nofs + Vector((11.0, 3.0, 0.0)))
            hf = 0.5 + 0.5 * noise.noise(p * 2.6 + self.nofs)
            gm = max(gm, min(1.0, fade * (0.25 + 1.0 * lf) * (0.65 + 0.5 * hf)))
        # the clay pot on the stack: not sooted like the shaft under it
        if self.pot_xy is not None and p.z > self.pot_xy[2] - 0.05 and (p.x - self.pot_xy[0]) ** 2 + (p.y - self.pot_xy[1]) ** 2 < 0.4 ** 2:
            r = min(r, 0.2); gm = min(gm, 0.1)
        elif self.chim_soot is not None:                 # soot-dark band over the top 1.5 m of the stack (wear R)
            pu, ztop = self.chim_soot(p)
            if pu is not None and pu.z > ztop - 2.4:
                ramp = min(1.0, (pu.z - (ztop - 2.4)) / 1.4)
                r = max(r, 0.75 * ramp * (0.97 + 0.06 * noise.noise(p * 0.7 + self.nofs)))    # smooth in z: no per-stone checker through the shader's remap
                gm *= 1.0 - 0.8 * ramp
        return (min(1.0, r), min(1.0, gm), 0.7 if slot == 'plaster' else res[2], res[3])     # plaster: polish 0.7 lifts the limewash to a warm off-white

    def chim_soot(self, p):
        """(point in the unrotated stack frame, rim height) if p is on the stack above the shoulder, else (None, 0)."""
        ch = self.chim
        pu = Matrix.Rotation(-self.rot, 3, 'Z') @ p if self.rot else p
        if pu.z < ch['zb'] + 0.1:
            return None, 0.0
        cxz = ch['cx'] + math.tan(ch['lean']) * max(pu.z, 0.0)
        if abs(pu.x - cxz) < ch['W0'] / 2 + 0.2 and pu.y > ch['front'] - 0.3:
            return pu, ch.get('zt', ch['H'])
        return None, 0.0

    # ---- coursed walls with openings: every course snaps to a global edge list so windows and doors cut cleanly
    def edges(self, hmax, ch):
        e = [-0.04]
        while e[-1] < hmax - 0.05:
            e.append(e[-1] + self.R.uniform(*ch))
        return e

    def fill(self, a, b, j, blen, B, k=0):
        """Stone lengths along a span: 0.3-0.6 m, with a long quoin (0.7-0.95 m) at the span ends on alternating courses."""
        R = self.R; total = b - a
        if total < 1.0:
            n = max(1, int(round(total / 0.45))); cut = [a + total * i / n for i in range(n + 1)]
            return list(zip(cut[:-1], cut[1:]))
        quoin = lambda: R.uniform(0.70, 0.95)
        small = lambda: R.uniform(0.30, 0.42)
        fl = quoin() if (j + k) % 2 == 0 else small()
        ll = quoin() if (j + k) % 2 == 1 else small()
        rest = total - fl - ll
        if rest < 0.2:
            n = max(2, int(round(total / 0.5))); cut = [a + total * i / n for i in range(n + 1)]
            return list(zip(cut[:-1], cut[1:]))
        out = [(a, a + fl)]; t = a + fl; end = b - ll
        while end - t > 0.02:
            ln = R.uniform(*blen)
            if end - (t + ln) < 0.2:
                ln = end - t
            out.append((t, t + ln)); t += ln
        out.append((end, b))
        return out

    def wall_run(self, k, t0, t1, hfn, ch=(0.25, 0.35), openings=(), excl=None, blen=(0.30, 0.60), E=None):
        R = self.R; g = self.g; B = 1.0
        a, b, d, out = self.side_frame(k); yaw = math.atan2(d.y, d.x)
        hmax = max(hfn(t0 + (t1 - t0) * i / 40) for i in range(41))
        E = E if E is not None else self.edges(hmax + 0.2, ch)
        ops = []
        for (oa, ob, oz0, oz1) in openings:
            z0 = min(E, key=lambda e: abs(e - oz0))
            z1 = 1e9 if oz1 > 1e8 else min(E, key=lambda e: abs(e - oz1))
            if z1 <= z0 + 0.1 and z1 < 1e8:
                z1 = E[E.index(z0) + 2] if E.index(z0) + 2 < len(E) else z0 + 0.7
            ops.append((oa, ob, z0, z1))
        self.wall_info[k] = dict(t0=t0, t1=t1, hfn=hfn, ops=ops)
        for j in range(len(E) - 1):
            z = E[j]; h = E[j + 1] - z
            cuts = sorted((oa, ob) for (oa, ob, z0, z1) in ops if z >= z0 - 1e-3 and z + h <= z1 + 1e-3)
            spans = []; cur = t0
            for oa, ob in cuts:
                if oa > cur: spans.append((cur, min(oa, t1)))
                cur = max(cur, ob)
            if cur < t1: spans.append((cur, t1))
            for (sa, sb) in spans:
                if sb - sa < 0.15:
                    continue
                pieces = self.fill(sa, sb, j, blen, B, k); added = []
                for pi_, (ta, tb) in enumerate(pieces):
                    tc = (ta + tb) / 2; top = hfn(tc)
                    if z + h * 0.55 >= top:
                        continue
                    if excl and excl(tc, z, h):
                        continue
                    last = z + h * 1.6 > top
                    p = a + d * tc + out * R.uniform(-0.03, 0.03)
                    xf = place(p.x, p.y, z + h / 2, yaw + rad(R.uniform(-3, 3)), rad(R.uniform(-1.8, 1.8)), rad(R.uniform(-1.8, 1.8)))
                    vs, fs = soft_box((tb - ta + 0.03, FOOT_W - R.uniform(0.02, 0.1), h + 0.04), xf, R, soft=R.uniform(0.04, 0.10), wob=0.012,
                                      bulge=0.008, cuts=0, top_break=0.07 if (last and R.random() < 0.7) else 0.0)
                    # soft_box(cuts=0) face order: top, -y, +y, -x, +x. Interior blocks keep the two wall faces only (top and ends are hidden by
                    # the course above and the neighbours); span ends (jambs, openings) and the broken top course keep theirs.
                    keep = [1, 2]
                    if last or (z + h * 1.5 > top): keep.append(0)
                    if pi_ == 0 or pi_ == len(pieces) - 1 or (last and pi_ % 2 == 0): keep += [3, 4]
                    vs, fs = pick_faces(vs, fs, keep)
                    g.add(vs, fs, 'stone_dk' if R.random() < 0.5 else 'stone')
                    if not last: added.append((ta, tb))
                # dark backer inside the wall behind the joints: a gap between stones shows dark core, never sky
                run = None
                for it in added + [None]:
                    ta, tb = it if it else (None, None)
                    if run is not None and (ta is None or ta - run[1] > 0.02):
                        c0 = a + d * ((run[0] + run[1]) / 2)
                        bv, bf = soft_box((run[1] - run[0] + 0.02, 0.26, h * 0.92), place(c0.x, c0.y, z + h / 2, yaw), random.Random(1), soft=0.0, wob=0.0, bulge=0.0, cuts=0)
                        bv, bf = pick_faces(bv, bf, [1, 2]); g.add(bv, bf, 'core'); run = None
                    if ta is not None:
                        run = (ta, tb) if run is None else (run[0], tb)
        return E

    def sill_and_lintel(self, k, ta, tb, z0, z1, lintel=True):
        R = self.R; g = self.g
        a, b, d, out = self.side_frame(k); yaw = math.atan2(d.y, d.x); tm = (ta + tb) / 2; p = a + d * tm
        E = self.wall_info[k]['E']
        z0 = min(E, key=lambda e: abs(e - z0))
        vs, fs = soft_box((tb - ta + 0.3, FOOT_W + 0.06, 0.14), place(p.x, p.y, z0 + 0.03, yaw + rad(R.uniform(-1.5, 1.5)), 0, rad(R.uniform(-1.5, 1.5))), R,
                          soft=0.12, wob=0.012, bulge=0.01, cuts=0)
        g.add(vs, fs, 'stone')
        if lintel and z1 < 1e8:
            z1 = min(E, key=lambda e: abs(e - z1))
            vs, fs = soft_box((tb - ta + 0.5, FOOT_W - 0.06, 0.2), place(p.x, p.y, z1 - 0.08, yaw + rad(R.uniform(-1.5, 1.5)), 0, rad(R.uniform(-1.5, 1.5))), R,
                              soft=0.08, wob=0.01, bulge=0.0, cuts=0)
            g.add(vs, fs, 'oak')

    # ---- door frame: stone jambs on the kit's jamb bases, an oak lintel across (Tall: both; Mid: one tall jamb, one short)
    def door_frame(self, Hj, lintel):
        R = self.R; g = self.g; x = self.door_x; c = self.rc
        for sgn in (-1, 1):
            hj = Hj if (lintel or sgn < 0) else Hj * 0.6
            z = 0.40; ji = 0
            while z < hj - 0.1:
                h = min(R.uniform(0.25, 0.35), hj - z)
                wq = 0.36 if ji % 2 else R.uniform(0.62, 0.78)                      # jamb quoins: long stones alternate with short, reaching outward
                xo = x + sgn * (self.door_w / 2 + 0.2 + (wq - 0.36) / 2) + R.uniform(-0.03, 0.03)
                vs, fs = soft_box((wq + R.uniform(-0.03, 0.04), FOOT_W + 0.04, h + 0.03), place(xo, -c, z + h / 2,
                                  rad(R.uniform(-3, 3)), rad(R.uniform(-1.5, 1.5)), rad(R.uniform(-2, 2))), R, soft=0.12, wob=0.012, bulge=0.008, cuts=0,
                                  top_break=0.06 if z + h >= hj - 0.05 and R.random() < 0.5 else 0.0)
                g.add(vs, fs, 'stone_dk' if R.random() < 0.5 else 'stone'); z += h; ji += 1
            if lintel or sgn < 0:
                self.cols.append((x + sgn * (self.door_w / 2 + 0.2), -c, 0.36, FOOT_W, hj - 0.1, 'x'))
        if lintel:
            ln = 2 * (self.door_w / 2 + 0.2 + 0.19) + 0.46          # overhangs each jamb by 0.23 m, the full wall thickness deep
            vs, fs = soft_box((ln, FOOT_W, 0.30), place(x, -c, Hj + 0.15, rad(R.uniform(-1, 1)), rad(R.uniform(-1.2, 1.2)), rad(R.uniform(-1, 1))), R,
                              soft=0.10, wob=0.012, bulge=0.0, cuts=1)
            g.add(vs, fs, 'oak')

    # ---- charred beam from a wall top down to the floor
    def beam_shape(self, ln, sec, xf):
        """A charred beam: sec = (y, z) section, ends tapered and splintered (jagged, shortened at random)."""
        R = self.R
        vs, fs = soft_box((ln, sec[0], sec[1]), xf, R, soft=0.10, wob=0.015, bulge=0.0, cuts=1)
        inv = xf.inverted()
        for i, vv in enumerate(vs):
            l = inv @ vv; e = ln / 2 - abs(l.x)
            if e < 0.01:
                f = R.uniform(0.38, 0.55)
                l.y *= f; l.z = l.z * f + R.uniform(-0.05, 0.05); l.x -= math.copysign(R.uniform(0.0, 0.18), l.x)
                vs[i] = xf @ l
        return vs, fs

    def oak_member(self, P0, P1, yaw, w=0.30, dep=0.28, break_top=0.10):
        R = self.R; v = P1 - P0; ln = v.length
        if ln < 0.25:
            return
        mid = (P0 + P1) / 2
        if abs(v.x) + abs(v.y) < 0.04 * ln:                              # a post
            xf = place(mid.x, mid.y, mid.z, yaw + rad(R.uniform(-1.5, 1.5)), rad(R.uniform(-2, 2)), rad(R.uniform(-2, 2)))
            vs, fs = soft_box((w, dep, ln), xf, R, soft=0.08, wob=0.012, bulge=0.0, cuts=0, top_break=break_top)
        else:
            q = v.normalized().to_track_quat('X', 'Z'); xf = Matrix.Translation(mid) @ q.to_matrix().to_4x4()
            vs, fs = self.beam_shape(ln, (dep, w), xf)
        self.g.add(vs, fs, 'oak')

    def gable_frame(self, gs, tcg, pf, ZS, W1, open_bay=None):
        """Charred half-timber frame above the stone ground storey of the gable: sill, posts, tie beam, braces, rafters along the rake and plaster
        infill left in about half of the bays, ragged at the top. The stack stands in the middle."""
        Q = self.Q
        a, b, d, out = self.side_frame(gs); yaw = math.atan2(d.y, d.x)
        info = self.wall_info[gs]
        P = lambda t, z: Vector((a.x + d.x * t, a.y + d.y * t, z))
        for half in (-1, 1):
            t_in = tcg + half * (W1 / 2 + 0.12)
            t_end = (info['t1'] - 0.2) if half > 0 else (info['t0'] + 0.2)
            if (t_end - t_in) * half < 0.8:
                continue
            tm = (t_end + t_in) / 2
            z0 = ZS + 0.11
            self.oak_member(P(t_end, z0), P(t_in, z0), yaw, w=0.30)                             # sill plate on the stone
            base = ZS + 0.22
            posts = {}
            for tt in (t_end, tm, t_in):
                top = pf(tt) - 0.12
                if top - base > 0.45:
                    self.oak_member(P(tt, base), P(tt, top), yaw, w=0.32); posts[tt] = top
            z_tie = base + 0.5 * (pf(tm) - 0.2 - base)
            if z_tie > base + 0.5:
                ta_ = t_end if pf(t_end) - 0.15 > z_tie else tm
                fr = Q.uniform(0.6, 1.0); tb_ = t_in + (ta_ - t_in) * fr
                self.oak_member(P(tb_, z_tie), P(t_in, z_tie), yaw, w=0.30)                       # tie beam, broken at the outer end
                if t_end in posts and Q.random() < 0.9:
                    self.oak_member(P(t_end, base + 0.1), P(tm, z_tie - 0.1), yaw, w=0.28)       # diagonal brace
                if t_in in posts and Q.random() < 0.7:
                    self.oak_member(P(t_in, base + 0.1), P(tm, z_tie - 0.1), yaw, w=0.28)
            if pf(t_end) > ZS + 0.3:
                pa = P(t_in, pf(t_in) - 0.12); pb = P(t_end, pf(t_end) - 0.08); fr2 = Q.uniform(0.45, 1.0)
                self.oak_member(pa, pa + (pb - pa) * fr2, yaw, w=0.28)                              # rafter along the rake, outer end gone
            for bi, (ta_, tb_) in enumerate(((t_end, tm), (tm, t_in))):
                topz = min(pf(ta_), pf(tb_)) - 0.28
                if open_bay == (half, bi) or topz - base < 0.4:
                    continue
                lo, hi = min(ta_, tb_) + 0.15, max(ta_, tb_) - 0.15
                if hi - lo < 0.3:
                    continue
                self.plaster_bay(P, yaw, lo, hi, base, topz, Q)

    def plaster_bay(self, P, yaw, lo, hi, base, topz, Q):
        """A plaster-and-lath infill panel between the posts: a grid of lime-wash cells (about 0.8 x 0.7 m), the upper ones more likely gone; where a
        cell has fallen out the oak laths show (thin horizontal strips) or the bay is just open. Plaster is 0.12 m thick, recessed in the frame."""
        nc = max(1, int(round((hi - lo) / 0.85))); nr = max(1, int(round((topz - base) / 0.7)))
        cw = (hi - lo) / nc; chh = (topz - base) / nr
        gone = {(ci, ri): Q.random() < 0.12 + 0.45 * (ri + 0.5) / nr for ci in range(nc) for ri in range(nr)}
        for ci in range(nc):
            for ri in range(nr):
                z0 = base + ri * chh; tc_ = lo + (ci + 0.5) * cw; pc = P(tc_, z0 + chh / 2)
                if gone[(ci, ri)]:
                    if Q.random() < 0.6:                                   # lath showing: 2 thin strips across the hole
                        for li in range(2):
                            zl = z0 + chh * (li + 0.5) / 2
                            pl = P(tc_, zl)
                            vs, fs = soft_box((cw - 0.02, 0.05, 0.05), place(pl.x, pl.y, pl.z, yaw + rad(Q.uniform(-1, 1)), rad(Q.uniform(-2, 2)), rad(Q.uniform(-3, 3))),
                                              self.R, soft=0.2, wob=0.006, bulge=0.0, cuts=0, top_break=0.02)
                            vs, fs = pick_faces(vs, fs, [1, 2]); self.g.add(vs, fs, 'oak')
                    continue
                vs, fs = soft_box((cw - 0.03, 0.12, chh - 0.03), place(pc.x, pc.y, pc.z, yaw + rad(Q.uniform(-1.5, 1.5)), rad(Q.uniform(-1, 1)), rad(Q.uniform(-1, 1))),
                                  self.R, soft=0.10, wob=0.014, bulge=0.012, cuts=0, top_break=0.10 if ri == nr - 1 else 0.03)
                keep = [1, 2]                                              # faces: top, -y, +y, -x, +x; edges show only next to a hole or the bay border
                if ri == nr - 1 or gone[(ci, ri + 1)]: keep.append(0)
                if ci == 0 or gone[(ci - 1, ri)]: keep.append(3)
                if ci == nc - 1 or gone[(ci + 1, ri)]: keep.append(4)
                vs, fs = pick_faces(vs, fs, keep); self.g.add(vs, fs, 'plaster')
                self.panels.append((Vector(pc), (P(1.0, 0.0) - P(0.0, 0.0)).normalized(), cw / 2, chh / 2))

    def wall_posts(self, k, t, n, gap, hgt):
        """Mid: one or two charred posts standing from a wall top, a broken top plate between them."""
        R = self.R; info = self.wall_info[k]
        a, b, d, out = self.side_frame(k); yaw = math.atan2(d.y, d.x)
        tops = []
        for i in range(n):
            tt = t + i * gap
            top = min(info['hfn'](tt + s) for s in (-0.25, 0.0, 0.25))
            P0 = Vector((a.x + d.x * tt, a.y + d.y * tt, top - 0.12)); P1 = P0 + Vector((0, 0, 0.12 + hgt * R.uniform(0.8, 1.15)))
            self.oak_member(P0, P1, yaw, w=0.22, break_top=0.16); tops.append(P1.z)
        z = min(tops) - 0.06
        tt1 = t + (gap * 0.65 if n > 1 else 0.9)
        self.oak_member(Vector((a.x + d.x * t, a.y + d.y * t, z)), Vector((a.x + d.x * tt1, a.y + d.y * tt1, z - 0.12)), yaw, w=0.2)

    def rafter_pair(self, gs, tm, apex_z):
        """Two rafters still joined at the apex, standing inside the gable (a surviving truss pair)."""
        a, b, d, out = self.side_frame(gs); inward = -out; yaw = math.atan2(d.y, d.x)
        base = a + inward * 1.05
        apex = Vector((base.x + d.x * tm, base.y + d.y * tm, apex_z))
        for sgn in (-1, 1):
            foot = Vector((base.x + d.x * (tm + sgn * 1.15), base.y + d.y * (tm + sgn * 1.15), 0.17))
            v = apex - foot; q = v.normalized().to_track_quat('X', 'Z')
            xf = Matrix.Translation((apex + foot) / 2) @ q.to_matrix().to_4x4()
            vs, fs = self.beam_shape(v.length + 0.2, (0.20, 0.25), xf)
            self.g.add(vs, fs, 'oak')
        vs, fs = soft_box((0.36, 0.34, 0.3), place(apex.x, apex.y, apex.z - 0.05, yaw), self.R, soft=0.1, wob=0.01, bulge=0.0, cuts=0)
        self.g.add(vs, fs, 'oak')

    def chimney_pot(self):
        R = self.R; sx, sy, sz = self.smoke.x, self.smoke.y, self.smoke.z
        rings = [(0.0, 0.25), (0.20, 0.31), (0.55, 0.25), (0.72, 0.29), (0.72, 0.18), (0.60, 0.17)]
        n = 8; vs = []; fs = []
        for (z, r_) in rings:
            for i in range(n):
                a_ = 2 * math.pi * i / n
                vs.append((sx + math.cos(a_) * r_ + R.uniform(-0.01, 0.01), sy + math.sin(a_) * r_ + R.uniform(-0.01, 0.01), sz + z - 0.02))
        for j in range(len(rings) - 1):
            for i in range(n):
                i2 = (i + 1) % n
                fs.append((j * n + i, j * n + i2, (j + 1) * n + i2, (j + 1) * n + i))
        fs.append(tuple((len(rings) - 1) * n + i for i in range(n)))
        self.cg.add(vs, orient(vs, fs), 'stone_dk'); self.pot_xy = (sx, sy, sz)

    def lean_beam(self, k, t, ang, skew, sec=(0.20, 0.25)):
        R = self.R; g = self.g; info = self.wall_info[k]
        a, b, d, out = self.side_frame(k); inward = -out
        top = min((info.get('btop') or info['hfn'])(t + s) for s in (-0.5, 0.0, 0.5))
        zhi = top + 0.03
        hi = a + d * t + inward * 0.02
        lh = (zhi - 0.17) / math.tan(rad(ang))
        dirh = (inward * math.cos(rad(skew)) + d * math.sin(rad(skew))).normalized()
        lim = self.h - 0.55
        lo = hi + dirh * lh
        if abs(lo.x) > lim or abs(lo.y) > lim:
            s = min(lim - 1e-3, abs(hi.x) + 1e-3) if False else 1.0
            for ax in (0, 1):
                if abs(lo[ax]) > lim and abs(dirh[ax]) > 1e-3:
                    s = min(s, (math.copysign(lim, lo[ax]) - hi[ax]) / (dirh[ax] * lh))
            lh *= max(0.2, s); lo = hi + dirh * lh
        P_hi = Vector((hi.x, hi.y, zhi)); P_lo = Vector((lo.x, lo.y, 0.17))
        v = P_hi - P_lo; ln = v.length + 0.25
        mid = (P_hi + P_lo) / 2 + v.normalized() * 0.05
        q = v.normalized().to_track_quat('X', 'Z')
        xf = Matrix.Translation(mid) @ q.to_matrix().to_4x4()
        vs, fs = self.beam_shape(ln, sec, xf)
        g.add(vs, fs, 'oak')
        return P_lo

    def heap(self, x, y, n, rad_, big=0.40):
        R = self.R; g = self.g
        for i in range(n):
            ang = R.uniform(0, 6.283); r_ = rad_ * math.sqrt(R.random())
            q = Vector((x + math.cos(ang) * r_, y + math.sin(ang) * r_, 0.0))
            lim = self.h - 0.12
            q.x = max(-lim, min(lim, q.x)); q.y = max(-lim, min(lim, q.y))
            if abs(q.x - self.door_x) < self.door_w / 2 + 0.3 and q.y < -self.rc + 0.5:
                continue
            s = R.uniform(0.18, big)
            size = (s * R.uniform(0.9, 1.5), s * R.uniform(0.8, 1.2), s * R.uniform(0.5, 0.8))
            zc = size[2] * 0.35 + max(0.0, 1 - r_ / rad_) * R.uniform(0.0, 0.4)
            xf = place(q.x, q.y, zc, R.uniform(0, math.pi), rad(R.uniform(-18, 18)), rad(R.uniform(-18, 18)))
            vs, fs = soft_box(size, xf, R, soft=0.3, wob=0.03 * s / 0.3, bulge=0.01, cuts=0, top_break=0.03 if s > 0.3 else 0.0)
            g.add(vs, fs, 'stone_dk' if R.random() < 0.5 else 'stone')

    def hearth_build(self):
        """Open hearth at the gable's foot: sooted flags and a kerb of stones, in front of the stack (kit's interior, rotated with the gable)."""
        R = self.R; g = self.g; ch = self.chim
        hx, hy = ch['cx'], ch['front'] - 0.42
        self.hearth_c = (hx, hy)
        Rz = Matrix.Rotation(self.rot, 3, 'Z')
        def rp(x, y):
            v = Rz @ Vector((x, y, 0)); return v.x, v.y
        for i in range(3):
            qx, qy = rp(hx + R.uniform(-0.45, 0.45), hy + R.uniform(-0.3, 0.3))
            vs, fs = soft_box((R.uniform(0.45, 0.65), R.uniform(0.4, 0.55), 0.07), place(qx, qy, 0.0, R.uniform(0, 3.1) + self.rot, rad(R.uniform(-2, 2)), rad(R.uniform(-2, 2))),
                              R, soft=0.2, wob=0.012, bulge=0.004, cuts=0)
            g.add(vs, fs, 'core')
        for i in range(6):
            ang = math.pi * (0.1 + 0.8 * i / 5)
            qx, qy = rp(hx + math.cos(ang) * 0.9, hy - math.sin(ang) * 0.65 + 0.25)
            sz = R.uniform(0.24, 0.34)
            vs, fs = soft_box((sz * 1.3, sz, sz * 0.8), place(qx, qy, sz * 0.3, R.uniform(0, 3.1), rad(R.uniform(-10, 10)), 0), R, soft=0.3, wob=0.02, bulge=0.01, cuts=0)
            g.add(vs, fs, 'stone_dk')
        qx, qy = rp(hx, hy)
        self.hearth = (qx, qy)

    # ---- the whole shell
    def build(self):
        Q = self.Q; R = self.R; cls = self.cls; v = self.base[-1]; S = self.S; h = self.h
        tall = self.level == 'Tall'
        gs, kt, kl, ke = {'A': (2, 3, 1, 0), 'B': (3, 2, 0, 1), 'C': (1, 0, 2, 3)}[v]
        self.rot = {2: 0.0, 3: math.pi / 2, 1: -math.pi / 2}[gs]
        ch = self.chim
        Ha = {'S': 5.5, 'M': 6.5, 'L': 7.5}[cls]
        if tall:
            ch['H'] = Ha + Q.uniform(1.2, 1.8)
            ch['zb'] = {'S': 2.4, 'M': 2.7, 'L': 3.0}[cls]
        else:
            ch['H'] = {'S': 4.6, 'M': 5.2, 'L': 5.9}[cls] + Q.uniform(-0.25, 0.25)
            ch['zb'] = {'S': 2.0, 'M': 2.2, 'L': 2.5}[cls]
        ch['W0'] = {'S': 1.60, 'M': 1.70, 'L': 1.80}[cls]; ch['D0'] = {'S': 1.25, 'M': 1.30, 'L': 1.35}[cls]; ch['front'] = ch['ymax'] - ch['D0']
        ch['Wtop'] = {'S': 0.90, 'M': 1.00, 'L': 1.10}[cls]            # shaft width: a clear taper from the 1.6-1.8 m breast
        self.build_chimney()
        if self.rot:
            Rz = Matrix.Rotation(self.rot, 3, 'Z')
            self.cg.v = [Rz @ vv for vv in self.cg.v]; self.smoke = Rz @ self.smoke
        if random.Random(self.seed ^ (0x907 if tall else 0x9A1)).random() < 0.5:       # clay pot on about half of the stacks (seeded)
            self.chimney_pot()
        self.build_doorstep()
        self.hearth_build()
        # chimney position along the gable side
        Pc = Matrix.Rotation(self.rot, 3, 'Z') @ Vector((ch['cx'], ch['ymax'] - ch['D0'] / 2, 0))
        a, b, d, out = self.side_frame(gs); tcg = (Pc - a).dot(d)
        W0, W1, W2, zb = ch['W0'], ch['W0'] * 0.80, ch['W2'], ch['zb']
        def excl_g(tc, z, hh):
            return abs(tc - tcg) < ((W0 / 2 + 0.08) if z + hh <= zb + 0.2 else (W1 / 2 + 0.12))
        Ls = {k: (self.side_frame(k)[1] - self.side_frame(k)[0]).length for k in range(4)}
        span = {k: ((-FOOT_W / 2, Ls[k] + FOOT_W / 2) if k in (0, 2) else (FOOT_W / 2, Ls[k] - FOOT_W / 2)) for k in range(4)}
        g_start = (kt == (gs + 1) % 4)
        def u_of(t):                                                  # 0 at the tall wall's gable end, 1 at its far end
            u = (t - span[kt][0]) / (span[kt][1] - span[kt][0]); return u if g_start else 1 - u
        def t_of(u):
            return span[kt][0] + (span[kt][1] - span[kt][0]) * (u if g_start else 1 - u)
        nz = {k: Q.random() * 9 for k in range(4)}
        sl, sr = Q.uniform(0.70, 0.86), Q.uniform(0.70, 0.86)
        sag_f, sag_side = Q.uniform(0.7, 1.3), Q.choice([-1, 1])
        isC = (v == 'C')
        # ---- wall height functions
        stub = {'S': 2.0, 'M': 2.5, 'L': 3.0}[cls]
        sL, sR = stub * Q.uniform(0.9, 1.15), stub * Q.uniform(0.9, 1.15)
        def hg(t):
            dt = t - tcg
            if tall and isC:                                   # C: the gable is broken off at half height, the stack rises clear
                return max(0.5 * Ha - 0.45 * abs(dt) + 0.35 * noise.noise(Vector((t * 1.3 + nz[gs], 2.0, 0))), 1.2)
            if tall:
                s_ = sl if dt < 0 else sr
                hh = Ha - s_ * abs(dt) + 0.35 * noise.noise(Vector((t * 1.3 + nz[gs], 2.0, 0)))
                if (dt * sag_side) > 0:
                    hh -= sag_f * sstep((abs(dt) - 1.0) / 2.2)
                return max(hh, 1.5)
            sd = sL if dt < 0 else sR
            if abs(dt) < sd:
                return 0.7 + 2.8 * (1 - abs(dt) / sd) ** 0.9 + 0.2 * noise.noise(Vector((t * 1.6 + nz[gs], 2.0, 0)))
            return 0.45 + 0.25 * noise.noise(Vector((t * 2.0 + nz[gs], 2.0, 0)))
        Ht = {'A': 3.5, 'B': 3.1, 'C': 3.6}[v] * Q.uniform(0.97, 1.03) if tall else Q.uniform(1.7, 2.2)
        sag_u = Q.uniform(0.72, 0.88)
        def ht(t):
            if tall and isC:                                   # C: a two-wall corner (front-left) standing to about 3.5 m
                return 1.3 + 2.2 * max(0.0, 1 - (t - span[kt][0]) / 3.4) ** 0.9 + 0.25 * noise.noise(Vector((t * 1.4 + nz[kt], 5.0, 0)))
            u = max(0.0, min(1.0, u_of(t)))
            lo_ = 1.9 if tall else 0.9
            hh = lo_ + (Ht - lo_) * (1 - u) ** 0.8 + 0.25 * noise.noise(Vector((t * 1.4 + nz[kt], 5.0, 0)))
            if tall:
                hh -= 0.9 * math.exp(-((u - sag_u) / 0.1) ** 2)
            return max(hh, 0.7)
        low_hi = Q.uniform(0.9, 1.4) if tall else Q.uniform(0.8, 1.25)
        gapk, gapt = Q.random(), Q.uniform(0.3, 0.7)
        def hl(t):
            u = (t - span[kl][0]) / (span[kl][1] - span[kl][0])
            hh = 0.6 + (low_hi - 0.6) * (0.5 + 0.5 * noise.noise(Vector((t * 1.1 + nz[kl], 7.0, 0)))) + 0.2 * noise.noise(Vector((t * 2.7 + nz[kl], 8.0, 0)))
            return max(0.45, hh - 0.5 * math.exp(-((u - gapt) / 0.08) ** 2))
        end_hi = Q.uniform(0.9, 1.5)
        def he(t):
            if tall and isC:
                return 1.2 + 2.3 * max(0.0, 1 - (span[ke][1] - t) / 2.8) ** 0.9 + 0.25 * noise.noise(Vector((t * 1.2 + nz[ke], 9.0, 0)))
            return max(0.5, 0.7 + (end_hi - 0.7) * (0.5 + 0.5 * noise.noise(Vector((t * 1.2 + nz[ke], 9.0, 0)))) + 0.2 * noise.noise(Vector((t * 2.9 + nz[ke], 4.0, 0))))
        # stone ground storey only on the Tall gable; above it the charred half-timber frame (gable_frame)
        E_g = self.edges(Ha + 0.5, (0.30, 0.44)); ZS = next(e for e in E_g if e >= 2.5)
        base_fn = {gs: ((lambda t: min(hg(t), ZS + 0.1)) if tall else hg), kt: ht, kl: hl, ke: he}
        guards = []                                            # (k, ta, tb, min height): wall above windows, stubs beside the jambs
        pier = None
        if not tall:
            pc = {'A': 1, 'B': 2, 'C': 0}[v]
            pier = (pc, 0.95)
        def wrap(k):
            f0 = base_fn[k]
            def f(t):
                hh = f0(t)
                if pier:
                    pc, P = pier
                    if (k == pc and t < P) or (k == (pc - 1) % 4 and t > Ls[k] - P):
                        hh = max(hh, 3.0 + 0.25 * noise.noise(Vector((t * 2.0 + nz[k], 6.0, 0))))
                for (gk, ga, gb, gh) in guards:
                    if gk == k and ga < t < gb:
                        hh = max(hh, gh + 0.1 * noise.noise(Vector((t * 2.0, 3.0 + k, 0))))
                return hh
            return f
        fn = {k: wrap(k) for k in range(4)}
        # ---- openings
        dxt = self.rc + self.door_x
        door_cut = (dxt - 0.95, dxt + 0.95, -1.0, 2.1 + 0.4 if tall else 1.5)
        wwid = {'S': 0.95, 'M': 1.0, 'L': 1.15}[cls]
        for (ga, gb) in ((dxt - 1.9, dxt - 0.95), (dxt + 0.95, dxt + 1.9)):      # at least 0.5 m of wall stub either side of the jambs
            guards.append((0, ga, gb, 1.15 if tall else 1.0))
        ops = {k: [] for k in range(4)}
        wins = []                                                    # (k, ta, tb, z0, z1)
        if tall:
            nwin = {'S': 1, 'M': 2, 'L': 2}[cls]
            for i in range(nwin):
                u = [0.38, 0.68][i] if nwin == 2 else 0.42
                tm = t_of(u + Q.uniform(-0.04, 0.04))
                if isC:
                    if i > 0:
                        break
                    tm = span[0][0] + 1.5                       # in the standing front corner, clear of the door
                wins.append((kt, tm - wwid / 2, tm + wwid / 2, 0.95, 2.15))
        else:
            tm = t_of(0.55)
            if kt == 0:
                tm = span[0][0] + 1.5
            wins.append((kt, tm - wwid / 2, tm + wwid / 2, 0.95, 1e9))
        for (k, ta, tb, z0, z1) in wins:
            ops[k].append((ta, tb, z0, z1))
            if z1 < 1e8:
                guards.append((k, ta - 0.45, tb + 0.45, z1 + 0.55))
        ops[0].append(door_cut)
        # ---- walls
        for k in range(4):
            ex = excl_g if k == gs else None
            E = self.wall_run(k, span[k][0], span[k][1], fn[k], openings=ops[k], excl=ex, E=(E_g if (k == gs and tall) else None))
            self.wall_info[k]['E'] = E
            fa_, fb_, fd_, fo_ = self.side_frame(k)
            self.open_rects += [(fa_, fd_, fo_, oa, ob, z0, z1) for (oa, ob, z0, z1) in self.wall_info[k]['ops']]
            if k == gs and tall:
                self.wall_info[k]['btop'] = lambda t: hg(t) - 0.4
        for (k, ta, tb, z0, z1) in wins:
            self.sill_and_lintel(k, ta, tb, z0, z1, lintel=(z1 < 1e8))
        self.door_frame(2.1 if tall else 1.85, lintel=tall)
        if tall:
            self.gable_frame(gs, tcg, hg, ZS, W1, open_bay=((Q.choice([-1, 1]), 1) if cls == 'L' and not isC else None))
            if v == 'B':                                            # one variant per size keeps a rafter pair joined at the apex
                room = [(sg, (self.wall_info[gs]['t1'] - tcg - 1.3) if sg > 0 else (tcg - self.wall_info[gs]['t0'] - 1.3)) for sg in (-1, 1)]
                sg = max(room, key=lambda r_: r_[1])[0]
                self.rafter_pair(gs, tcg + sg * {'S': 2.2, 'M': 2.6, 'L': 3.0}[cls], {'S': 3.3, 'M': 4.0, 'L': 4.6}[cls])
        else:
            wp = {'A': (kl, 0.35, 2, 1.4, 1.4), 'B': (ke, 0.5, 2, 1.3, 1.6), 'C': (kl, 0.4, 1, 1.0, 1.5)}[v]
            wk = wp[0]; tw = span[wk][0] + (span[wk][1] - span[wk][0]) * wp[1]
            self.wall_posts(wk, tw, wp[2], wp[3], wp[4])
        # ---- beams
        if tall:
            plan = {'A': [('g', 1.4, 50, 15), ('g', -1.6, 34, -22), ('t', 0.45, 60, 5)],
                    'B': [('t', 0.25, 40, 10), ('t', 0.6, 58, -12), ('g', -1.3, 46, 0)],
                    'C': [('g', 1.2, 62, -8), ('t', 0.35, 44, 18), ('t', 0.7, 30, -15)]}[v]
            if cls == 'S':
                plan = plan[:3] if v != 'C' else plan[:3]
        else:
            plan = {'A': [('t', 0.5, 24, 25)], 'B': [('g', -2.2, 26, -20)], 'C': [('t', 0.35, 22, 15)]}[v]
        for (w, off, ang, skew) in plan:
            if w == 'g':
                self.lean_beam(gs, tcg + off * (1.0 if cls != 'L' else 1.25), ang, skew)
            else:
                self.lean_beam(kt, t_of(off), ang, skew)
        # ---- rubble heaps inside at the foot of the fallen walls
        nh = {'S': 6, 'M': 8, 'L': 10}[cls]
        def inside(k, t, depth=0.8):
            a_, b_, d_, o_ = self.side_frame(k); p = a_ + d_ * t - o_ * depth; return p.x, p.y
        sites = [(gs, tcg + 2.0), (gs, tcg - 2.0), (kt, t_of(0.3)), (kt, t_of(0.7)), (ke, (span[ke][0] + span[ke][1]) / 2), (kl, (span[kl][0] + span[kl][1]) * 0.4)]
        if not tall:
            sites = sites[:4] + [sites[5]]
        for (k, t) in sites:
            x, y = inside(k, t)
            self.heap(x, y, nh, 0.75)
        # ---- collision boxes (section per ~1.6 m of wall, jambs, the stack)
        for k in range(4):
            info = self.wall_info[k]; t0_, t1_ = info['t0'], info['t1']
            cutsd = [(oa, ob) for (oa, ob, z0, z1) in info['ops'] if z0 < 0]
            segs = []; cur = t0_
            for oa, ob in sorted(cutsd):
                if oa > cur: segs.append((cur, oa))
                cur = max(cur, ob)
            segs.append((cur, t1_))
            a_, b_, d_, o_ = self.side_frame(k)
            for (sa, sb) in segs:
                n_ = max(1, int(round((sb - sa) / 1.6)))
                for i in range(n_):
                    ta = sa + (sb - sa) * i / n_; tb = sa + (sb - sa) * (i + 1) / n_
                    hmin = min(info['hfn'](ta + (tb - ta) * s / 4) for s in range(5)) - 0.2
                    if hmin < 0.4: continue
                    c_ = a_ + d_ * (ta + tb) / 2
                    ax = 'x' if abs(d_.x) > 0.5 else 'y'
                    self.cols.append((c_.x, c_.y, (tb - ta), FOOT_W - 0.1, hmin, ax))
        xs = [vv.x for vv in self.cg.v]; ys = [vv.y for vv in self.cg.v]; zs = [vv.z for vv in self.cg.v]
        self.cols.append(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, max(xs) - min(xs) - 0.1, max(ys) - min(ys) - 0.1, max(zs) - 0.2, 'stack'))
        self.tcg = tcg


def box_mesh(name, cx, cy, sx, sy, hz, coll):
    vs = [(cx - sx / 2, cy - sy / 2, 0), (cx + sx / 2, cy - sy / 2, 0), (cx + sx / 2, cy + sy / 2, 0), (cx - sx / 2, cy + sy / 2, 0),
          (cx - sx / 2, cy - sy / 2, hz), (cx + sx / 2, cy - sy / 2, hz), (cx + sx / 2, cy + sy / 2, hz), (cx - sx / 2, cy + sy / 2, hz)]
    fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    me = bpy.data.meshes.new(name); me.from_pydata(vs, [], fs)
    ob = bpy.data.objects.new(name, me); coll.objects.link(ob); ob.hide_render = True
    return ob


def build_shell(name):
    base, level = name.rsplit('_', 1)
    coll = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(coll)
    sh = Shell(base, level)
    sh.build()
    foot = sh.to_object(coll)
    chim = sh.to_object(coll, sh.cg, 'Chimney')
    fp = K['footprint_box'](sh.S, coll)
    sock = bpy.data.objects.new('SOCKET_Doorstep', None); coll.objects.link(sock)
    sock.empty_display_type = 'ARROWS'; sock.empty_display_size = 0.3; sock.location = sh.socket
    smoke = bpy.data.objects.new('SOCKET_Smoke', None); coll.objects.link(smoke)
    smoke.empty_display_type = 'ARROWS'; smoke.empty_display_size = 0.25; smoke.location = sh.smoke
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    cols = []
    for i, (cx, cy, sx, sy, hz, ax) in enumerate(sh.cols):
        if ax == 'y': sx, sy = sy, sx
        elif ax == 'stack': pass
        elif ax == 'x': pass
        cols.append(box_mesh('FOOTPRINT_COL_%d' % i, cx, cy, sx, sy, hz, coll))
    objs = [root, foot, chim, fp, sock, smoke] + cols
    for o in objs[1:]:
        o.parent = root; o['base'] = o.name.split('.')[0]; o.name = name + '__' + o['base']
    tris = L.tri_count(foot) + L.tri_count(chim)
    zmax = max(vv.co.z for o in (foot, chim) for vv in o.data.vertices)
    ext = max(max(abs(vv.co.x), abs(vv.co.y)) for o in (foot, chim) for vv in o.data.vertices)
    print('SHELL %s: tris %d (footing %d, chimney %d), max z %.2f, chimney top %.2f, max |xy| %.3f (half %.1f), cols %d, door_x %.2f'
          % (name, tris, L.tri_count(foot), L.tri_count(chim), zmax, max(vv.co.z for vv in chim.data.vertices), ext, sh.h, len(cols), sh.door_x))
    return dict(name=name, root=root, objs=objs, coll=coll, tris=tris, zmax=zmax, ext=ext, S=sh.S, kind='shell', sh=sh)


# ============================================================================================ props
class PropHost(Ruin):
    """A host for the kit's to_object / wear_at that has no ruin around it (no chimney, hearth, door)."""
    def __init__(self, name):
        self.name = name; self.cls = 'S'; self.S = 6.0; self.h = 100.0
        self.seed = zlib.crc32(name.encode()) & 0xFFFF; self.R = random.Random(self.seed); self.nofs = Vector((self.seed % 97, self.seed % 61, 0.0))
        self.g = Geo(99.0, 0.0); self.rc = 1e3; self.door_x = 1e3; self.step = (0.0, 0.0); self.hearth = (1e3, 1e3)
        self.chim = dict(cx=0, W0=0.0, front=1e9, H=0, lean=0.0, W2=0.0, zb=0)

    def wear_at(self, p, n, slot):
        res = super().wear_at(p, n, slot)
        if slot == 'oak':                               # weathered oak, not burnt: the kit's char ramp is for the ruin's beams
            r = 0.12 + 0.12 * (0.5 + 0.5 * noise.noise(p * 3.1 + self.nofs)) + 0.25 * max(0.0, 1 - p.z / 0.25)
            return (min(1.0, r), res[1] * 0.5, res[2], res[3])
        return res


def orient(vs, fs):
    c = sum((Vector(p) for p in vs), Vector()) / len(vs); out = []
    for f in fs:
        pts = [Vector(vs[i]) for i in f]; n = (pts[1] - pts[0]).cross(pts[2] - pts[0]); ctr = sum(pts, Vector()) / len(pts)
        out.append(tuple(reversed(f)) if n.dot(ctr - c) < 0 else tuple(f))
    return out


def slab(prof, thick, R, wob=0.012, bulge=0.025, inset=0.07):
    """A pillowed slab: a convex profile in (x, z), a chamfer ring and a bulged face front and back (centred at y = 0)."""
    n = len(prof); cx = sum(p[0] for p in prof) / n; cz = sum(p[1] for p in prof) / n
    V = []
    for yy, k, sh in ((-thick / 2, 1.0, -1), (-thick / 2 - bulge, 1 - inset, -1), (thick / 2, 1.0, 1), (thick / 2 + bulge, 1 - inset, 1)):
        for (x, z) in prof:
            V.append((cx + (x - cx) * k + R.uniform(-wob, wob), yy + R.uniform(-wob, wob) * 0.5, cz + (z - cz) * k + R.uniform(-wob, wob)))
    F = []
    F.append(tuple(n + i for i in range(n)))
    F.append(tuple(3 * n + i for i in range(n)))
    for i in range(n):
        j = (i + 1) % n
        F.append((i, j, n + j, n + i)); F.append((2 * n + i, 2 * n + j, 3 * n + j, 3 * n + i)); F.append((i, j, 2 * n + j, 2 * n + i))
    return V, orient(V, F)


def xf_pts(vs, xf):
    return [xf @ Vector(v) for v in vs]


def build_prop(name):
    coll = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(coll)
    host = PropHost(name); R = host.R; g = host.g
    if name.startswith('Headstone'):
        v = name[-1]
        hgt = {'A': 0.95, 'B': 0.8, 'C': 1.05}[v]; w = {'A': 0.62, 'B': 0.56, 'C': 0.6}[v]
        if v == 'A':                                    # round arch top
            prof = [(-w / 2, 0), (w / 2, 0), (w / 2, hgt - w / 2)] + [(math.cos(math.pi * i / 6) * w / 2, hgt - w / 2 + math.sin(math.pi * i / 6) * w / 2) for i in range(1, 6)] + [(-w / 2, hgt - w / 2)]
        elif v == 'B':                                  # pointed shoulders
            prof = [(-w / 2, 0), (w / 2, 0), (w / 2, hgt - 0.2), (0.0, hgt), (-w / 2, hgt - 0.2)]
        else:                                           # snapped off on a slant
            prof = [(-w / 2, 0), (w / 2, 0), (w / 2, hgt * 0.62), (w * 0.12, hgt * 0.76), (-w * 0.2, hgt), (-w / 2, hgt * 0.94)]
        vs, fs = slab(prof, 0.15, R)
        xf = place(0, 0, -0.06, rad(R.uniform(-5, 5)), rad(R.uniform(-4, 4)), rad(R.uniform(-4, 4)))
        g.add(xf_pts(vs, xf), fs, 'stone')
        vs, fs = soft_box((w + 0.3, 0.42, 0.14), place(0, 0.02, 0.0, rad(R.uniform(-4, 4)), rad(R.uniform(-3, 3)), rad(R.uniform(-3, 3))), R, soft=0.25, wob=0.02, bulge=0.01, cuts=0)
        g.add(vs, fs, 'stone_dk')
    elif name == 'Signpost_A':
        vs, fs = soft_box((0.17, 0.17, 2.4), place(0, 0, 1.18, rad(R.uniform(-4, 4)), rad(R.uniform(-1.5, 1.5)), rad(R.uniform(-1.5, 1.5))), R, soft=0.10, wob=0.012, bulge=0.0, cuts=2, top_break=0.05)
        g.add(vs, fs, 'oak')
        vs, fs = soft_box((0.27, 0.27, 0.06), place(0, 0, 2.42, rad(R.uniform(-8, 8)), rad(R.uniform(-3, 3)), rad(R.uniform(-3, 3))), R, soft=0.2, wob=0.01, bulge=0.0, cuts=0)
        g.add(vs, fs, 'oak')
        for (zc, sgn, yaw) in ((2.12, 1, -6), (1.78, -1, 9)):
            prof = [(0.0, -0.1), (0.95, -0.1), (1.08, 0.0), (0.95, 0.1), (0.0, 0.1)]
            vs, fs = slab(prof, 0.045, R, wob=0.006, bulge=0.008, inset=0.05)
            xf = place(0, -0.12, zc, rad(yaw if sgn > 0 else 180 - yaw), rad(R.uniform(-1.5, 1.5)), rad(R.uniform(-2, 2)))
            if sgn < 0:                                  # points left: mirror about the post
                xf = place(0, -0.12, zc, rad(R.uniform(-1.5, 1.5))) @ Matrix.Scale(-1, 4, Vector((1, 0, 0)))
                vs = [(-x, y, z) for (x, y, z) in vs]; fs = [tuple(reversed(f)) for f in fs]; xf = place(0, -0.12, zc, rad(yaw))
            g.add(xf_pts(vs, xf), fs, 'oak')
        for i in range(4):                                # a cairn round the foot
            ang = i * 1.6 + R.uniform(-0.3, 0.3); s = R.uniform(0.2, 0.3)
            vs, fs = soft_box((s * 1.4, s, s * 0.7), place(math.cos(ang) * 0.24, math.sin(ang) * 0.24, s * 0.25, R.uniform(0, 3), rad(R.uniform(-12, 12)), rad(R.uniform(-12, 12))), R, soft=0.3, wob=0.02, bulge=0.01, cuts=0)
            g.add(vs, fs, 'stone_dk' if i % 2 else 'stone')
    elif name == 'Hatch_Oak':
        kh = 0.14
        for sy in (-1, 1):                                # kerb stones along the long sides (two each) ...
            for sx in (-1, 1):
                vs, fs = soft_box((1.06, 0.24, kh + 0.05), place(sx * 0.53, sy * 0.745, kh / 2 - 0.04, rad(R.uniform(-1.5, 1.5)), 0, rad(R.uniform(-1.5, 1.5))), R, soft=0.18, wob=0.012, bulge=0.012, cuts=0, top_break=0.02)
                g.add(vs, fs, 'stone_dk' if (sx + sy) % 4 == 0 else 'stone')
        for sx in (-1, 1):                                # ... and across the ends
            vs, fs = soft_box((0.24, 1.26, kh + 0.04), place(sx * 0.945, 0.0, kh / 2 - 0.04, rad(R.uniform(-1.5, 1.5)), 0, rad(R.uniform(-1.5, 1.5))), R, soft=0.18, wob=0.012, bulge=0.012, cuts=0)
            g.add(vs, fs, 'stone')
        vs, fs = soft_box((1.64, 1.24, 0.04), place(0, 0, -0.01), R, soft=0.0, wob=0.0, bulge=0.0, cuts=0)
        g.add(vs, fs, 'core')                              # the dark pit under the boards
        for i in range(5):                                # planks run across the 1.2 m, five of them over the 1.6 m
            px = -0.64 + 0.32 * i
            vs, fs = soft_box((0.305, 1.2, 0.07), place(px, 0, 0.055 + R.uniform(-0.004, 0.004), rad(R.uniform(-0.8, 0.8)), 0, rad(R.uniform(-0.8, 0.8))), R, soft=0.14, wob=0.004, bulge=0.004, cuts=0, top_break=0.0)
            g.add(vs, fs, 'oak')
        for sx in (-0.42, 0.42):                          # iron strap hinges along the -Y edge
            vs, fs = soft_box((0.075, 0.62, 0.014), place(sx, -0.30, 0.097), R, soft=0.1, wob=0.002, bulge=0.0, cuts=0)
            g.add(vs, fs, 'core')
            vs, fs = soft_box((0.12, 0.12, 0.03), place(sx, -0.58, 0.103), R, soft=0.4, wob=0.002, bulge=0.0, cuts=0)
            g.add(vs, fs, 'core')
        vs, fs = soft_box((0.11, 0.11, 0.016), place(0.0, 0.5, 0.097), R, soft=0.2, wob=0.002, bulge=0.0, cuts=0)
        g.add(vs, fs, 'core')                              # pull ring: plate and a small torus
        verts = []; faces = []; M, m = 8, 4; R0, r0 = 0.07, 0.012
        for i in range(M):
            a = 2 * math.pi * i / M
            for j in range(m):
                b = 2 * math.pi * j / m
                verts.append(((R0 + r0 * math.cos(b)) * math.cos(a), (R0 + r0 * math.cos(b)) * math.sin(a), r0 * math.sin(b)))
        for i in range(M):
            for j in range(m):
                faces.append((i * m + j, ((i + 1) % M) * m + j, ((i + 1) % M) * m + (j + 1) % m, i * m + (j + 1) % m))
        xf = Matrix.Translation((0.0, 0.5 - 0.04, 0.125)) @ Matrix.Rotation(rad(24), 4, 'X')
        g.add(xf_pts(verts, xf), orient(verts, faces), 'core')
    ob = host.to_object(coll, g, name)
    root = bpy.data.objects.new(name + '_root', None); coll.objects.link(root)
    ob.parent = root; ob['base'] = name; ob.name = name + '__mesh'
    tris = L.tri_count(ob)
    zmax = max(vv.co.z for vv in ob.data.vertices)
    print('PROP %s: tris %d, max z %.2f' % (name, tris, zmax))
    return dict(name=name, root=root, objs=[root, ob], coll=coll, tris=tris, zmax=zmax, ext=0.0, S=0, kind='prop', sh=None)


# ============================================================================================ export, render
def export(v):
    os.makedirs(STAGE_DIR, exist_ok=True)
    path = os.path.join(STAGE_DIR, v['name'] + '.fbx')
    kids = [o for o in v['objs'] if 'base' in o]
    for o in kids:
        o.name = o['base']
    old = v['root'].location.copy(); v['root'].location = (0, 0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    for o in v['objs']:
        o.hide_render = False
        o.select_set(True)
    if v['kind'] == 'prop':
        v['root'].name = v['name'] + '_root'
    bpy.context.view_layer.objects.active = v['root']
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                             mesh_smooth_type='OFF', colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False, use_tspace=True, path_mode='STRIP')
    v['root'].location = old
    for o in kids:
        o.name = v['name'] + '__' + o['base']
    for o in v['objs']:
        if o.name.split('__')[-1].startswith('FOOTPRINT'):
            o.hide_render = True
    print('EXPORT', path)


def show(vs, keep):
    for v in vs:
        for o in v['objs']:
            hide = (v['name'] not in keep) or o.name.split('__')[-1].startswith('FOOTPRINT')
            o.hide_render = hide


def render_cell(path, cam, w, h):
    sc = bpy.context.scene; sc.camera = cam
    sc.render.resolution_x = w; sc.render.resolution_y = h; sc.render.resolution_percentage = 100
    sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)


def human(x, y, mat):
    me = bpy.data.meshes.new('human')
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for vv in bm.verts:
        vv.co.x *= 0.38; vv.co.y *= 0.28; vv.co.z = (vv.co.z + 0.5) * 1.75
    bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new('SB_Human', me); bpy.context.scene.collection.objects.link(ob); ob.location = (x, y, 0)
    return ob


def sheet(paths, cols, w, h, out):
    rows = (len(paths) + cols - 1) // cols
    A = np.ones((rows * h, cols * w, 4), np.float32)
    for i, p in enumerate(paths):
        im = bpy.data.images.load(p, check_existing=False)
        px = np.empty(len(im.pixels), np.float32); im.pixels.foreach_get(px)
        px = px.reshape(im.size[1], im.size[0], 4)
        r, c = divmod(i, cols)
        A[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px[:h, :w]
        bpy.data.images.remove(im)
    img = bpy.data.images.new('sheet', cols * w, rows * h, alpha=False, float_buffer=False)
    img.pixels.foreach_set(A.ravel()); img.filepath_raw = out; img.file_format = 'PNG'; img.save()


def review(vs):
    sun = K['stage']()
    sc = bpy.context.scene
    hm = bpy.data.materials.new('SB_Human'); hm.use_nodes = True
    hm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.9, 0.35, 0.05, 1)
    shells = [v for v in vs if v['kind'] == 'shell']
    if len(shells) >= 1:
        cw, chh = int(K['arg']('--cw', '333')), int(K['arg']('--chh', '300')); cells = []
        order = [n for n in SHELL_NAMES if any(v['name'] == n for v in shells)]
        order = sorted(order, key=lambda n: (SIZES.index(n.split('_')[1]), 0 if n.endswith('Tall') else 1, n.split('_')[2]))
        hmn = human(0, 0, hm)
        for n in order:
            v = next(x for x in shells if x['name'] == n); S = v['S']
            show(vs, [n]); hmn.location = (S / 2 + 1.0, -S / 2 - 1.5, 0)
            dist = {6.0: 20.0, 8.0: 25.0, 11.0: 33.0}[S] * DSCALE; ang = rad(ANG)
            eye = (-math.sin(ang) * dist, -math.cos(ang) * dist, CAMZ)
            cam = K['cam_at']('cam', eye, (0, 0, min(v['zmax'] * 0.42, 3.2)), fov=36)
            p = os.path.join(OUT, n + '_3q.png'); render_cell(p, cam, cw, chh); cells.append(p)
        sheet(cells, min(3, len(cells)), cw, chh, os.path.join(OUT, SHEETNAME))
        hmn.location = (50, 50, 0)
    props = [v for v in vs if v['kind'] == 'prop']
    if props:
        xs = {'Headstone_A': -2.4, 'Headstone_B': -1.2, 'Headstone_C': 0.0, 'Signpost_A': 1.6, 'Hatch_Oak': 3.8}
        for v in props:
            v['root'].location = (xs[v['name']], 0, 0)
        show(vs, [v['name'] for v in props])
        hmn = human(-3.7, 0.4, hm)
        cam = K['cam_at']('camp', (1.0, -8.0, 1.5), (0.6, 0, 0.9), fov=38)
        sc.render.resolution_x = 1000
        render_cell(os.path.join(OUT, 'props_sheet.png'), cam, 1000, 420)
        for v in props:
            v['root'].location = (0, 0, 0)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    vs = []
    for n in WANT:
        if n in SHELL_NAMES: vs.append(build_shell(n))
        elif n in PROP_NAMES: vs.append(build_prop(n))
    with open(os.path.join(OUT, 'shell_stats.txt'), 'w') as f:
        for v in vs:
            f.write('%s tris %d zmax %.2f ext %.3f\n' % (v['name'], v['tris'], v['zmax'], v['ext']))
    if EXPORT:
        for v in vs:
            export(v)
    if RENDER:
        review(vs)


main()
