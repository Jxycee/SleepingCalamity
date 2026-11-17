"""Fine-blade grass carpet clumps for the terrain detail layers (Area 1 world, T08; docs/design/area1-world-build.md 1b point 1).

Three clumps of thin, tapered, curved single-ribbon blades, 0.22-0.40 m tall, about 0.6 m across, 180-250 triangles each (the old B9 Grass_A/B/C are
0.4-0.5 m clumps of 20-30 wide ribbons and stay as sparse accents). They share the B9 foliage atlas: UVs sit in the same two grass cells
(sc_foliage_town.py C_GRASS / C_GRASS2: dark green at the base rising to a yellow-green tip, fully opaque, the silhouette is the geometry),
so Unity builds the material from the Grass_A folder as for the old clumps and tints it per prototype. Vertex normals point mostly up
(0.85 up + 0.15 away from the clump centre) so a field of them shades as one lawn, not as separate fans. Pivot = ground centre.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_foliage_fine.py -- [--out <scratch dir for the .blend>]
Writes Assets/_Project/Content/Art/Town/B9/Grass_Fine_A|B|C/SC_B9_GrassFine_<k>.fbx (FBX only: no texture copies, the atlas lives in Grass_A).
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import sc_b9_lib as L

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[argv.index('--out') + 1]) if '--out' in argv else None
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'B9')

C_GRASS = (0.5, 0.25, 0.625, 0.5); C_GRASS2 = (0.625, 0.25, 0.75, 0.5)   # atlas cells, (u0, v0, u1, v1)

def cell_uv(rect, uv):
    return (rect[0] + (rect[2] - rect[0]) * uv[0], rect[1] + (rect[3] - rect[1]) * uv[1])

def fine_clump(seed, n, hmin, hmax, radius, segs, lean_lo, lean_hi, w_lo, w_hi):
    rr = random.Random(seed); mb = L.MB()
    for _ in range(n):
        a = rr.uniform(0, 2 * math.pi); r0 = radius * math.sqrt(rr.random())
        base = Vector((math.cos(a) * r0, math.sin(a) * r0, 0))
        # lean mostly away from the centre with some scatter, so the clump fans out about 0.6 m across
        ang = a + rr.gauss(0, 0.9); outv = Vector((math.cos(ang), math.sin(ang), 0))
        hgt = rr.uniform(hmin, hmax); lean = rr.uniform(lean_lo, lean_hi); wid = rr.uniform(w_lo, w_hi) * (hgt / 0.3) ** 0.5
        side = outv.cross(Vector((0, 0, 1))).normalized(); rect = C_GRASS if rr.random() < 0.55 else C_GRASS2
        curl = rr.uniform(0.15, 0.45)                         # how far the tip droops back down
        prev = None
        for s in range(segs + 1):
            t = s / segs
            p = base + outv * (lean * hgt * t * t) + Vector((0, 0, hgt * (t - curl * lean * t * t)))
            hw = wid * (1 - t ** 1.25) + 0.0008
            Lp, Rp = p - side * hw, p + side * hw
            out_c = Vector((p.x, p.y, 0)); out_c = out_c.normalized() if out_c.length > 1e-4 else Vector()
            nn = (Vector((0, 0, 1)) * 0.85 + out_c * 0.15).normalized(); ao = 0.45 + 0.55 * t
            if prev:
                pl, pr, pn, pa, pt = prev
                mb.face([pl, pr, Rp, Lp], [cell_uv(rect, (0, pt)), cell_uv(rect, (1, pt)), cell_uv(rect, (1, t)), cell_uv(rect, (0, t))], [pn, pn, nn, nn], [pa, pa, ao, ao])
            prev = (Lp, Rp, nn, ao, t)
    return mb

# name, seed, blades, h range, radius, segments, lean range, half-width range (m)
CLUMPS = {
    'A': (101, 40, 0.25, 0.36, 0.19, 3, 0.20, 0.55, 0.009, 0.014),
    'B': (202, 56, 0.28, 0.40, 0.19, 2, 0.15, 0.50, 0.010, 0.015),
    'C': (303, 30, 0.22, 0.32, 0.20, 3, 0.30, 0.70, 0.011, 0.016),
}

if __name__ == '__main__':
    bpy.ops.wm.read_homefile(use_empty=True)
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    objs = {}
    for k, (seed, n, h0, h1, rad, segs, l0, l1, w0, w1) in CLUMPS.items():
        mb = fine_clump(seed, n, h0, h1, rad, segs, l0, l1, w0, w1)
        ob = L.build(f'SC_B9_GrassFine_{k}', mb)
        xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]; zs = [v.co.z for v in ob.data.vertices]
        print(k, 'tris', L.tri_count(ob), 'blades', n, 'height', round(max(zs), 3), 'across', round(max(max(xs) - min(xs), max(ys) - min(ys)), 3))
        objs[k] = ob
    if OUT:
        os.makedirs(OUT, exist_ok=True); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'grass_fine.blend'))
    for k, ob in objs.items():
        d = os.path.join(EXP, f'Grass_Fine_{k}'); os.makedirs(d, exist_ok=True)
        L.export_fbx([ob], os.path.join(d, f'SC_B9_GrassFine_{k}.fbx'))
    print('EXPORT', EXP)
