# sc_paint_pass.py (2026-10-25, asset skills): the hand-painted finish for a SleepingCalamity model, applied to its vertex colour after the AO bake.
# Stylised AAA props get their "crafted" read from two painter's habits the AO alone doesn't give: worn, lighter convex edges and darker cavities,
# plus a warm top / cool underside. This does both on `Col` (0.5 x ao x tint), multiplicatively, so the palette slot and the season still own the hue.
#
# Use from a generator, after B.realize(b) / make_object and before export:
#     import importlib.util, os
#     _s = importlib.util.spec_from_file_location('sc_paint_pass', os.path.join(ROOT, 'Art', 'Blender', 'tools', 'sc_paint_pass.py'))
#     PP = importlib.util.module_from_spec(_s); _s.loader.exec_module(PP)
#     for ob in objs: PP.paint_pass(ob)                      # defaults suit wood and painted props; PP.STONE / PP.METAL presets below
# Or on an exported FBX (to try it on a shipped model; writes the FBX next to it unless --out):
#     blender -b --factory-startup --python Art/Blender/tools/sc_paint_pass.py -- <in.fbx> <out.fbx> [--edge 0.18] [--cavity 0.14] [--preset wood|stone|metal]
#
# How: per vertex, convexity = mean over its edges of (n . (neighbour - v) / |neighbour - v|), negative on a ridge, positive in a crease;
# smoothed once over the neighbours so edges read as a soft painted band, not a hairline. Edge wear lightens toward a pale worn tone (the same hue,
# lighter: x (1 + edge)); cavities darken (x (1 - cavity)). Top/bottom: a small value and warm/cool shift by the normal's y (Unity up = Blender z).
# Deterministic (no random draws). Col stays clamped to 1, and each object keeps its mean value (keep_mean).
# Vertex colour can only paint where vertices are: the edge band shows on rounded parts (H.rbox bevel loops, lathes, tubes); a flat B.box face
# has only corners, so it just shifts as a whole. Round the parts whose edges should catch the eye.
import bpy, bmesh, math, sys
from mathutils import Vector, noise

WOOD = dict(edge=0.35, cavity=0.20, top=0.05, bottom=0.06)
STONE = dict(edge=0.25, cavity=0.26, top=0.06, bottom=0.08)
METAL = dict(edge=0.45, cavity=0.14, top=0.03, bottom=0.04)     # bare metal shines through worn paint on edges
PRESETS = {'wood': WOOD, 'stone': STONE, 'metal': METAL}

def convexity(me, smooth_steps=1):
    # On WELDED positions: the kit builds hard-edged parts from separate vertices per face (split normals), so a vertex's own edges never cross the
    # corner. Group vertices by position, give each group the area-weighted normal of every face touching it, and its neighbours across all faces.
    key = {}
    vkey = []
    for v in me.vertices:
        k = (round(v.co.x, 4), round(v.co.y, 4), round(v.co.z, 4))
        vkey.append(key.setdefault(k, len(key)))
    G = len(key)
    pos = [Vector()] * G
    N = [Vector((0.0, 0.0, 0.0)) for _ in range(G)]
    nb = [set() for _ in range(G)]
    for v in me.vertices:
        pos[vkey[v.index]] = v.co.copy()
    for poly in me.polygons:
        ks = [vkey[i] for i in poly.vertices]
        w = poly.normal * poly.area
        for i, k in enumerate(ks):
            N[k] += w
            nb[k].add(ks[i - 1]); nb[k].add(ks[(i + 1) % len(ks)])
    N = [n.normalized() if n.length > 1e-9 else Vector((0, 0, 1)) for n in N]
    cv = [0.0] * G
    for k in range(G):
        acc, n = 0.0, 0
        for o in nb[k]:
            if o == k: continue
            d = pos[o] - pos[k]; L = d.length
            if L > 1e-6:
                acc += N[k].dot(d) / L; n += 1
        cv[k] = acc / n if n else 0.0
    for _ in range(smooth_steps):
        nv = cv[:]
        for k in range(G):
            ns = [cv[o] for o in nb[k] if o != k]
            if ns: nv[k] = 0.5 * cv[k] + 0.5 * sum(ns) / len(ns)
        cv = nv
    return [cv[vkey[i]] for i in range(len(me.vertices))], [N[vkey[i]].z for i in range(len(me.vertices))]

def paint_pass(ob, edge=0.30, cavity=0.20, top=0.05, bottom=0.06, width=1, attr='Col', keep_mean=False, brush=0.35, brush_scale=0.22, seed=0.0, presets_by_slot=None, presets_by_layer=None):
    # v2 (2026-10-25, duo round 2 finding: v1 was a no-op on kit geometry). Col is per CORNER, so a bevel strip can be painted apart from the flat
    # face beside it even where they share positions. A bevel / rounded-edge face is one whose corner normals bend away from its own face normal
    # (H.rbox's exact rounded normals); a flat face's corners equal its normal. Edge wear lightens those strips by `edge` x how much they bend,
    # so the worn band is exactly the bevel. Curved parts (lathes, tubes) bend everywhere and get a uniform, small lift, which keeps them even.
    # Cavities: a face whose corners bend INTO it (a concave fillet) darkens by `cavity`. Top/bottom: warm/cool by the corner normal's up.
    # Brush (duo round 3: rubric line 5 stuck at 3 on box-board props): prop-scale painterly variation INSIDE a part. Each corner gets
    # +-brush/2 value and a warm/cool shift from position noise at `brush_scale` metres (the shader's own _FGPaint drift is 0.7-2.3 m, too
    # coarse for a 0.7 m chest), so a 4-vertex board face carries a soft gradient. Position-only noise: deterministic; `seed` offsets it per asset.
    me = ob.data
    ca = me.color_attributes.get(attr)
    if ca is None:
        return
    n = len(me.loops)
    col = [0.0] * (4 * n); ca.data.foreach_get('color', col)
    cn = [0.0] * (3 * n); me.corner_normals.foreach_get('vector', cn)
    # face bend: mean (1 - corner . face normal); convex when the corners lean outward from the face centre
    warm, cool = (1.04, 1.01, 0.95), (0.95, 0.98, 1.05)
    bends = []
    for poly in me.polygons:
        fn = poly.normal; c0 = poly.center
        acc, conv = 0.0, 0.0
        for li in poly.loop_indices:
            v = Vector(cn[3 * li:3 * li + 3])
            acc += 1.0 - max(-1.0, min(1.0, v.dot(fn)))
            d = me.vertices[me.loops[li].vertex_index].co - c0
            conv += (v - fn).dot(d)            # > 0: corners lean outward (convex rounding), < 0: inward (concave)
        bends.append((acc / poly.loop_total, conv))
    # normalise the bend to this object's strongest bevel, so the band is visible whatever the bevel radius / segment count
    peak = max([b for b, _ in bends] + [1e-6])
    # presets_by_slot={6: METAL, 3: WOOD}: a mixed timber/iron object takes each face's edge/cavity strength from its palette slot (uv1.x)
    slot_of = None
    lay = me.uv_layers.get('Layer')
    by = presets_by_layer or presets_by_slot
    if by and lay:
        # presets_by_layer={0: METAL, 3: WOOD}: keyed on the TEXTURE layer (uv1.y). The headroom recipes put timber, iron and oak all on the Rock
        # slot, but timber carries the Wood layer (3) and iron none (0), so the layer tells them apart (duo blind test #2).
        uvv = [0.0] * (2 * n); lay.data.foreach_get('uv', uvv)
        k = 1 if presets_by_layer else 0
        slot_of = [int(round(uvv[2 * poly.loop_start + k])) for poly in me.polygons]
        presets_by_slot = by
    for pi, (poly, (bend, conv)) in enumerate(zip(me.polygons, bends)):
        e_, c_ = edge, cavity
        if slot_of is not None and slot_of[pi] in presets_by_slot:
            e_, c_ = presets_by_slot[slot_of[pi]].get('edge', edge), presets_by_slot[slot_of[pi]].get('cavity', cavity)
        w = min(1.0, bend / max(peak * 0.6, 0.004))
        f = 1.0 + e_ * w if conv >= 0 else 1.0 - c_ * w
        for li in poly.loop_indices:
            up = cn[3 * li + 2]                     # Blender z = Unity up
            p = me.vertices[me.loops[li].vertex_index].co / max(brush_scale, 1e-3) + Vector((seed, seed * 0.7, seed * 1.3))
            # mathutils noise spans about +-0.47 (p5/p95): x2 so `brush` really is the +-value range (duo round 4, T8)
            bv = max(-1.0, min(1.0, 2.0 * noise.noise(p))) * brush * 0.5
            bw = max(-1.0, min(1.0, 2.0 * noise.noise(p * 0.7 + Vector((17.0, 3.0, 9.0)))))
            hw = 0.25 * brush
            # warm: a little more red, less blue; cool: blue-GREEN, never lilac (T9)
            bf = ((1.0 + bv) * (1.0 + 0.4 * hw * bw), (1.0 + bv) * (1.0 + 0.15 * hw * abs(bw)), (1.0 + bv) * (1.0 - 0.6 * hw * bw))
            g = warm if up > 0 else cool
            k = (top if up > 0 else bottom) * min(1.0, abs(up))
            rgb = [col[4 * li + t] * bf[t] * (1.0 + (g[t] - 1.0) * (k / 0.05) * 0.5) * (1.0 + (k if up > 0 else -k) * 0.5) for t in range(3)]
            if f >= 1.0:
                # worn edge: lift toward a paler version of the SAME hue with headroom, never past Col 1 (T7: a plain multiply clipped the
                # kit's high tints, e.g. GREY_TIMBER's blue, and drifted the edge yellow)
                top_c = max(rgb + [1e-6]); room = max(0.0, 1.0 - top_c)
                lift = min((f - 1.0) * top_c, room * 0.8)            # how much the brightest channel can still rise (keep 20% headroom)
                k2 = (top_c + lift) / top_c
                rgb = [c * k2 for c in rgb]
            else:
                rgb = [c * f for c in rgb]
            m = max(rgb)
            if m > 0.99:                                            # keep the hue: scale down instead of clipping a channel
                rgb = [c * 0.99 / m for c in rgb]
            for t in range(3):
                col[4 * li + t] = max(0.0, rgb[t])
    if keep_mean:
        before = [0.0] * (4 * n); ca.data.foreach_get('color', before)
        m0 = sum(before[4 * i] + before[4 * i + 1] + before[4 * i + 2] for i in range(n))
        m1 = sum(col[4 * i] + col[4 * i + 1] + col[4 * i + 2] for i in range(n))
        if m1 > 1e-6:
            k = m0 / m1
            col = [min(1.0, x * k) if (i % 4) != 3 else x for i, x in enumerate(col)]
    ca.data.foreach_set('color', col)

def stave_bands(ob, centre, count, value=0.12, seam=0.30, axis='y', attr='Col', r_range=None, h_range=None):
    # Painterly staves / planks on a round part (barrels, buckets, tubs, round towers): alternate each sector's value by +-value/2 (deterministic,
    # by sector index) and darken a narrow seam where sectors meet, around `axis` (Unity axes: 'y' up) through `centre` (Unity x, y, z).
    # Needs vertices at the seams: build the lathe with sides = a multiple of `count` (2 x count gives one seam line per stave edge).
    # r_range=(r0, r1) / h_range=(h0, h1) (metres, Unity axes) limit it to the staves, so lids, taps, hoops and pots nearby stay as they are.
    # centre: the barrel axis in the FINAL (exported) coordinates: after LK.to_base(b) that is the authored axis minus the returned shift.
    me = ob.data; ca = me.color_attributes.get(attr)
    if ca is None: return
    n = len(me.loops); col = [0.0] * (4 * n); ca.data.foreach_get('color', col)
    cx, cy, cz = centre
    for li, lp in enumerate(me.loops):
        co = me.vertices[lp.vertex_index].co           # Blender axes: Unity (x, y, z) = (-bx, bz, -by)
        ux, uy, uz = -co.x, co.z, -co.y
        a, b = (ux - cx, uz - cz) if axis == 'y' else ((uy - cy, uz - cz) if axis == 'x' else (ux - cx, uy - cy))
        if r_range and not (r_range[0] <= math.hypot(a, b) <= r_range[1]): continue
        hh = uy if axis == 'y' else (ux if axis == 'x' else uz)
        if h_range and not (h_range[0] <= hh <= h_range[1]): continue
        t = (math.atan2(b, a) / (2 * math.pi) + 1.0) % 1.0 * count
        k = int(t) % count
        f = 1.0 + value * (0.5 if k % 2 else -0.5) * (1.0 if (k * 7) % 3 else 0.6)
        edge = min(t - int(t), 1 - (t - int(t)))          # 0 at a seam
        if edge < 0.08: f *= 1.0 - seam * (1 - edge / 0.08)
        for c in range(3): col[4 * li + c] = min(1.0, col[4 * li + c] * f)
    ca.data.foreach_set('color', col)

def main(argv):
    src, out = argv[0], argv[1]; rest = argv[2:]
    p = dict(WOOD)
    if '--preset' in rest: p = dict(PRESETS[rest[rest.index('--preset') + 1]])
    for k in ('edge', 'cavity', 'top', 'bottom'):
        if '--' + k in rest: p[k] = float(rest[rest.index('--' + k) + 1])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=src, colors_type='LINEAR')
    for o in bpy.data.objects:
        if o.type == 'MESH': paint_pass(o, **p)
    for o in bpy.data.objects: o.select_set(o.type == 'MESH')
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)
    print('PAINTPASS', out, p)

if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
