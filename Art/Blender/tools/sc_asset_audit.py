# sc_asset_audit.py (2026-10-25, asset skills): measures a SleepingCalamity model export against the project's encoding and budgets, as JSON.
# Used by the sc-asset-* skills (.claude/skills) to score a model before it is committed; read-only (it never writes into the repo).
#
# Run:  blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <out.json> [--class prop|outbuilding|building|tree|rock|card|hero|run|creature|cover] <model.fbx|.glb> [...]
# Per mesh object it reports:
#   size: tris, verts, surface area (m2), tri density (tris/m2), bounding box in metres (Blender axes after FBX import: z up), origin offset
#   encoding: UVMap / Layer (uv1 = palette slot, texture layer) / Col (vertex colour) present; slot and layer values integral and in range
#   colour: Col luminance min/mean/max, albedo value p10/mean/p90 (lin(palette[slot]) x 2 x Col as sRGB), the clipped share (a channel at 1.0: the tint went past the 0.5 x ao x tint clamp), ao spread (std)
#   shading: smooth share, smear share (corner normals > 45 deg off their face: gradients that are not on the surface)
#   hygiene: degenerate faces, loose verts, non-manifold edges, material slots
# and checks each against the class budget in BUDGET (measured from the shipped Millpond exports, 2026-10-25; see the sc-asset-budget skill).
# An object passes when it has no FAIL; WARN is advice. Exit code is 0 either way: read the JSON.
import bpy, bmesh, json, math, os, re, sys
from mathutils import Vector

# Budgets per class, from the 232 shipped Millpond FBX (median / p75 tri density per class) and the HF study (sc-asset-budget, 2026-10-25).
#   tris: hard ceiling per object; dens: tris per m2 of surface (WARN above warn, FAIL above fail); mats: material slots.
BUDGET = {
    'prop':     dict(tris=6000,  dens_warn=500,  dens_fail=1500, mats=1),
    'building': dict(tris=26000, dens_warn=200,  dens_fail=600,  mats=1),
    'outbuilding': dict(tris=9000, dens_warn=400, dens_fail=1200, mats=1),   # walk-up structures under ~3 m: kilns, sheds, privies, pump houses
    'tree':     dict(tris=9000,  dens_warn=3600, dens_fail=8000, mats=2),
    'rock':     dict(tris=1500,  dens_warn=300,  dens_fail=800,  mats=1),
    'card':     dict(tris=4000,  dens_warn=4000, dens_fail=9000, mats=1),
    'hero':     dict(tris=40000, dens_warn=900,  dens_fail=2500, mats=2),
    'run':      dict(tris=6000,  dens_warn=500,  dens_fail=1500, mats=1),   # walls, fences, edges laid in modules: also tris per metre of run
    # animals and birds (duo round 28): small but slender, so no small-object cap; the shipped Life animals run 628-3698 tris (median 1480) at
    # 1400-6300 tris/m2 (a kingfisher is 0.2 m), so density is judged loosely
    'creature': dict(tris=4000, dens_warn=9000, dens_fail=30000, mats=1),
    # instanced / many-placed shore and ground plants (duo round 30): the sc-asset-budget 300-tri ceiling per season model is a FAIL (it is
    # drawn hundreds of times); small by nature, so the small-object cap and the smear check don't apply
    'cover':    dict(tris=300,  dens_warn=2000, dens_fail=6000, mats=1),
}
# class run: the shipped runs' median tris per metre (sc_asset_score_baseline_run.json, duo round 24: StoneWall 699, StoneWallTumbled 574,
# RaceWall 672, PathEdge 252, FenceRail 221); WARN above 2x
RUN_MEDIAN_PER_M = 573.5
SLOTS, LAYERS = 16, 32   # ArtPalette slots; MillpondTextureLayers.Count (0-15 env copy, 16+ Millpond grain)
# Small things are judged by count, not density (a 0.3 m medal needs a few hundred tris whatever its area): an object whose bounding
# diagonal is under SMALL_DIAG metres gets SMALL_TRIS and no density check; density applies from DENSITY_MIN_AREA m2 of surface.
SMALL_DIAG, SMALL_TRIS, DENSITY_MIN_AREA = 0.8, 1600, 1.5

def _palette():
    # ArtPalette.asset Summer, sRGB -> linear (PaletteGlobals linearises it), so the audit can compute the albedo the game draws
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'Assets', '_Project', 'Content', 'Art', 'ArtPalette.asset'), encoding='utf-8').read()
    block = src.split('- name: Summer', 1)[1]
    cols = re.findall(r'\{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: [\d.]+\}', block)[:16]
    return [tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in map(float, c)) for c in cols]

def to_srgb(x):
    return 12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055

def lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]

def audit_object(o, cls):
    b = BUDGET[cls]
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    bm.transform(o.matrix_world)
    bm.normal_update()
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    area = sum(f.calc_area() for f in bm.faces)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-8)
    loose = sum(1 for v in bm.verts if not v.link_edges)
    nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    smooth_faces = sum(1 for f in bm.faces if f.smooth)
    xs = [v.co for v in bm.verts]
    lo = Vector((min(p.x for p in xs), min(p.y for p in xs), min(p.z for p in xs))) if xs else Vector()
    hi = Vector((max(p.x for p in xs), max(p.y for p in xs), max(p.z for p in xs))) if xs else Vector()
    bm.free()
    # Smear: a corner normal far from its face's normal (> 45 deg) shades the face with a gradient that is not on the surface: a big
    # smooth-shaded facet, or a thin part averaged across its sides. Intended hard edges (split normals that follow each face) do not count.
    cn = [0.0] * (3 * len(me.loops))
    me.corner_normals.foreach_get('vector', cn)
    smear = 0
    for poly in me.polygons:
        fn = poly.normal
        for li in poly.loop_indices:
            if fn.dot(Vector(cn[3 * li:3 * li + 3])) < 0.707:
                smear += 1

    r = dict(name=o.name, cls=cls, tris=tris, verts=len(me.vertices), area=round(area, 3), dens=round(tris / max(area, 1e-4), 1),
             size=[round(x, 3) for x in (hi - lo)], min=[round(x, 3) for x in lo], max=[round(x, 3) for x in hi],
             origin_to_base=round(lo.z - o.matrix_world.translation.z, 3), mats=len(o.material_slots),
             smooth=round(smooth_faces / max(1, len(me.polygons)), 3), smear_share=round(smear / max(1, len(me.loops)), 3),
             degenerate=degenerate, loose=loose, nonmanifold=nonman, open_edges=boundary)
    uvn = [u.name for u in me.uv_layers]
    r['uv_layers'] = uvn
    # FBX import names the second UV set after the exported name ('Layer'); a GLB import may call it UVMap.001
    lay = me.uv_layers.get('Layer') or (me.uv_layers[1] if len(me.uv_layers) > 1 else None)
    if lay:
        vals = [0.0] * (2 * len(me.loops)); lay.data.foreach_get('uv', vals)
        sl = vals[0::2]; ly = vals[1::2]
        r['slots'] = sorted({int(round(s)) for s in sl})
        r['layers'] = sorted({int(round(s)) for s in ly})
        r['slot_nonint'] = sum(1 for s in sl + ly if abs(s - round(s)) > 0.02)
        r['slot_range_bad'] = sum(1 for s in sl if not 0 <= round(s) < SLOTS) + sum(1 for s in ly if not 0 <= round(s) < LAYERS)
    ca = me.color_attributes.get('Col') or (me.color_attributes[0] if len(me.color_attributes) else None)
    if ca:
        n = len(ca.data); c = [0.0] * (4 * n); ca.data.foreach_get('color', c)
        L = [lum(c[4 * i:4 * i + 3]) for i in range(n)]
        mean = sum(L) / max(1, n)
        r['col_name'] = ca.name
        r['col_lum'] = [round(min(L), 3), round(mean, 3), round(max(L), 3)]
        r['col_std'] = round(math.sqrt(sum((x - mean) ** 2 for x in L) / max(1, n)), 4)
        r['col_clipped'] = round(sum(1 for i in range(n) if max(c[4 * i:4 * i + 3]) >= 0.999) / max(1, n), 4)
        mx = sorted(max(c[4 * i:4 * i + 3]) for i in range(n)); r['col_max_p90'] = round(mx[int(0.9 * (n - 1))], 3) if n else 0.0
        if lay and ca.domain == 'CORNER' and len(sl) == n:
            # the albedo the game draws: lin(palette[slot]) x 2 x Col, as sRGB value (0-1); area-agnostic (per corner)
            pal = _palette()
            vals = []
            for i in range(n):
                pc = pal[min(15, max(0, int(round(sl[i]))))]
                vals.append(to_srgb(lum([pc[k] * 2 * c[4 * i + k] for k in range(3)])))
            vals.sort()
            r['albedo_value'] = [round(vals[int(0.1 * (n - 1))], 3), round(sum(vals) / n, 3), round(vals[int(0.9 * (n - 1))], 3)]

    F, W = [], []
    diag = (hi - lo).length; r['diag'] = round(diag, 3)
    cap = SMALL_TRIS if (diag < SMALL_DIAG and cls not in ('creature', 'cover')) else b['tris']
    if tris > cap: F.append('tris %d > %d%s' % (tris, cap, ' (small object)' if diag < SMALL_DIAG else ''))
    r['dens_checked'] = area >= DENSITY_MIN_AREA   # under DENSITY_MIN_AREA m2 of surface the density is reported but not judged
    if cls == 'run':
        run_len = max(hi.x - lo.x, hi.y - lo.y)
        r['tris_per_m'] = round(tris / max(run_len, 1e-3), 1)
        if r['tris_per_m'] > 2 * RUN_MEDIAN_PER_M:
            W.append('%.0f tris per metre of run > 2x the shipped runs\' median %.0f' % (r['tris_per_m'], RUN_MEDIAN_PER_M))
    if area >= DENSITY_MIN_AREA:
        if r['dens'] > b['dens_fail']: F.append('density %.0f > %d tris/m2' % (r['dens'], b['dens_fail']))
        elif r['dens'] > b['dens_warn']: W.append('density %.0f > %d tris/m2 (spend it on the silhouette?)' % (r['dens'], b['dens_warn']))
    if r['mats'] > b['mats']: F.append('%d material slots > %d (one shared toon material; colour comes from uv1)' % (r['mats'], b['mats']))
    if not lay: F.append('no uv1 "Layer" (palette slot, texture layer)')
    elif r['slot_nonint'] or r['slot_range_bad']: F.append('uv1 values not integral/in range (%d/%d)' % (r['slot_nonint'], r['slot_range_bad']))
    if not ca: F.append('no vertex colour "Col" (0.5 x ao x tint)')
    else:
        if r['col_clipped'] > 0.02: W.append('%.1f%% of vertex colours clip at 1.0 (tint too strong for the 0.5 x ao x tint clamp)' % (100 * r['col_clipped']))
        if r['col_std'] < 0.01: W.append('vertex colour nearly flat (std %.3f): no baked ao/variation' % r['col_std'])
        if r.get('albedo_value') and r['albedo_value'][1] < 0.22:
            W.append('dark: mean albedo value %.2f (sRGB; the concepts\' darkest masses sit near 0.25): raise the ao floor or the tint (sc_tint.py)' % r['albedo_value'][1])
        if r.get('col_max_p90', 0) > 0.88:
            W.append('no headroom: 10%% of corners have a Col channel above %.2f, so edge wear and brush (sc_paint_pass) cannot lighten them: move the part to a lighter slot (sc_tint.py) so its tint drops' % r['col_max_p90'])
        if r['col_lum'][1] > 0.75: W.append('vertex colour mean %.2f: not the half-scale encoding (expect ~0.2-0.5)' % r['col_lum'][1])
    if r['smooth'] < 0.9: W.append('only %.0f%% faces smooth-shaded (R74: rounded, soft)' % (100 * r['smooth']))
    # Shipped Millpond props: median smear 0.22 (soft averaged normals on chunky shapes are part of the rounded look; woven strands of 3-4 sides
    # sit at 0.6-0.9). Above 0.6 it is worth a look in the preview, not a defect by itself.
    if r['smear_share'] > 0.60 and cls not in ('tree', 'card', 'cover'): W.append('%.0f%% of corner normals > 45 deg off their face: check the preview for gradients across flat faces (strands and intended soft blocks are fine)' % (100 * r['smear_share']))
    if degenerate: W.append('%d degenerate faces' % degenerate)
    if loose: W.append('%d loose verts' % loose)
    if nonman: W.append('%d non-manifold edges' % nonman)
    r['FAIL'] = F; r['WARN'] = W
    r['pass'] = not F
    return r

def main(argv):
    out = argv[0]; rest = argv[1:]
    cls = 'prop'
    if '--class' in rest:
        i = rest.index('--class'); cls = rest[i + 1]; del rest[i:i + 2]
    rows = []
    for path in rest:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        ext = os.path.splitext(path)[1].lower()
        if ext == '.fbx':
            bpy.ops.import_scene.fbx(filepath=path, colors_type='LINEAR')   # the kit exports Col as LINEAR (export_fbx); the default SRGB would re-linearise it
        elif ext in ('.glb', '.gltf'):
            bpy.ops.import_scene.gltf(filepath=path)
        elif ext == '.obj':
            bpy.ops.wm.obj_import(filepath=path)
        else:
            print('SKIP', path); continue
        for o in bpy.data.objects:
            if o.type == 'MESH':
                r = audit_object(o, cls); r['file'] = os.path.basename(path); rows.append(r)
                print('AUDIT %-40s %-28s %s tris=%d dens=%.0f %s' % (r['file'][:40], o.name[:28], 'PASS' if r['pass'] else 'FAIL', r['tris'], r['dens'],
                                                                   '; '.join(r['FAIL'] + r['WARN'])[:200]))
    with open(out, 'w') as f:
        json.dump(rows, f, indent=1)

if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
