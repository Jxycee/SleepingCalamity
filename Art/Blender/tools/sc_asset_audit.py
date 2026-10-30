# sc_asset_audit.py (rewritten 2026-10-30, M8): measures a SleepingCalamity model against the style bible budgets (docs/PHASE1.md 3.3), as JSON.
# Used by the sc-asset-* skills to measure a model against the PHASE1 3.3 budgets before it is committed; read-only (it never writes into the repo).
#
# Run:  blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <out.json> [--class kit|scatter|hero|building|character|enemy|prop] [--tex-res 2048] <model.fbx|.glb> [...]
#       blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- --selftest      (builds a T2 test mesh: 2 UV maps + a corner colour attribute)
# Per mesh object it reports:
#   size: tris, verts, surface area (m2), tri density (tris/m2), bounding box in metres (Blender axes after import: z up), origin offset
#   uv: layer names, UV0 present and inside the tile range, texel density (px/m) for --tex-res against the class target (PHASE1 3.3)
#   colour attributes: every attribute's name, domain and luminance min/mean/max (a T2 piece's mask-blend colour is allowed, never required)
#   shading: smooth share, smear share (corner normals > 45 deg off their face)
#   hygiene: degenerate faces, loose verts, non-manifold edges, material slots
# FAIL = over a PHASE1 3.3 hard budget (triangles, material slots); WARN = advice (texel density, density, hygiene). Exit code is 0 either way: read the JSON.
# It makes no palette, colour-encoding or second-UV checks: a T2 piece may carry a second UV map and a mask-blend colour attribute. Colour is judged
# from renders and captures with sc_ref_palette.py (sc-asset-review).
import bpy, bmesh, json, math, os, sys
from mathutils import Vector

# Budgets per class from docs/PHASE1.md 3.3 (LOD0 triangles) and the sc-asset-budget skill. tex_ppm = target texel density in px/m.
#   tris: hard ceiling per object; mats: material slots (a kit piece is one tileable set plus an optional trim/blend set; a building shares sets).
BUDGET = {
    'kit':       dict(tris=5000,  tex_ppm=1024, mats=2),    # T2 modular kit piece 0.5-5k (walls up to 3 m 1024 px/m; upper storeys 512)
    'scatter':   dict(tris=400,   tex_ppm=1024, mats=1),    # T3 instanced sculpted piece 50-400
    'hero':      dict(tris=60000, tex_ppm=1536, mats=3),    # T4 hero prop 30-60k, 1024-2048 px/m
    'building':  dict(tris=75000, tex_ppm=1024, mats=8),    # a whole building (kit + heroes) at most 75k
    'character': dict(tris=60000, tex_ppm=None, mats=2),    # hero character 60k, 1x4K body+outfit, 1x2K head+hair
    'enemy':     dict(tris=25000, tex_ppm=None, mats=1),
    'prop':      dict(tris=6000,  tex_ppm=1024, mats=2),    # generic fallback for a T2/T3 prop family member
}
SMALL_DIAG = 0.8   # objects under this bounding diagonal (m) skip the texel-density judgement
TEXEL_TOL = 2.0    # WARN when density is outside target / TEXEL_TOL .. target * TEXEL_TOL


def lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def colour_attrs(me):
    out = []
    for ca in me.color_attributes:
        n = len(ca.data)
        if not n:
            continue
        c = [0.0] * (4 * n)
        ca.data.foreach_get('color', c)
        L = [lum(c[4 * i:4 * i + 3]) for i in range(n)]
        mean = sum(L) / n
        out.append(dict(name=ca.name, domain=ca.domain, type=ca.data_type, lum=[round(min(L), 3), round(mean, 3), round(max(L), 3)],
                        std=round(math.sqrt(sum((x - mean) ** 2 for x in L) / n), 4)))
    return out


def audit_object(o, cls, tex_res=2048):
    b = BUDGET[cls]
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    bm.transform(o.matrix_world)
    bm.normal_update()
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    areas = [f.calc_area() for f in bm.faces]
    area = sum(areas)
    degenerate = sum(1 for a in areas if a < 1e-8)
    loose = sum(1 for v in bm.verts if not v.link_edges)
    nonman = sum(1 for e in bm.edges if not e.is_manifold and not e.is_boundary)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    smooth_faces = sum(1 for f in bm.faces if f.smooth)
    xs = [v.co for v in bm.verts]
    lo = Vector((min(p.x for p in xs), min(p.y for p in xs), min(p.z for p in xs))) if xs else Vector()
    hi = Vector((max(p.x for p in xs), max(p.y for p in xs), max(p.z for p in xs))) if xs else Vector()
    # UV0 texel density: sqrt(uv area / world area) * resolution, face by face (uv units: one tile = 1.0 = tex_res px)
    uv_area = 0.0
    uv0 = bm.loops.layers.uv.active
    uv_out_of_range = 0
    if uv0 is not None:
        for f, a in zip(bm.faces, areas):
            pts = [l[uv0].uv for l in f.loops]
            for p in pts:
                if not (-64 <= p.x <= 64 and -64 <= p.y <= 64):
                    uv_out_of_range += 1
            s = 0.0
            for i in range(1, len(pts) - 1):
                s += abs((pts[i] - pts[0]).cross(pts[i + 1] - pts[0])) * 0.5
            uv_area += s
    bm.free()
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
    r['uv_layers'] = [u.name for u in me.uv_layers]
    r['colour_attrs'] = colour_attrs(me)
    r['texel_ppm'] = round(tex_res * math.sqrt(uv_area / area), 1) if (uv_area > 0 and area > 1e-6) else None

    F, W = [], []
    diag = (hi - lo).length; r['diag'] = round(diag, 3)
    r['tris_budget'] = b['tris']
    if tris > b['tris']:
        F.append('tris %d > %d (PHASE1 3.3 %s)' % (tris, b['tris'], cls))
    if r['mats'] > b['mats']:
        F.append('%d material slots > %d' % (r['mats'], b['mats']))
    if not r['uv_layers']:
        W.append('no UV map')
    elif uv_out_of_range:
        W.append('%d UV corners far outside the tile range (check UV units)' % uv_out_of_range)
    ppm = b.get('tex_ppm')
    if ppm and r['texel_ppm'] and diag >= SMALL_DIAG:
        if not (ppm / TEXEL_TOL <= r['texel_ppm'] <= ppm * TEXEL_TOL):
            W.append('texel density %.0f px/m at %d px vs target %d (PHASE1 3.3; 512 is right for upper storeys, roofs, vault crowns)' % (r['texel_ppm'], tex_res, ppm))
    if r['smooth'] < 0.5 and cls in ('hero', 'character', 'enemy'):
        W.append('only %.0f%% faces smooth-shaded' % (100 * r['smooth']))
    if r['smear_share'] > 0.60:
        W.append('%.0f%% of corner normals > 45 deg off their face: check the preview for gradients across flat faces' % (100 * r['smear_share']))
    if degenerate: W.append('%d degenerate faces' % degenerate)
    if loose: W.append('%d loose verts' % loose)
    if nonman: W.append('%d non-manifold edges' % nonman)
    r['FAIL'] = F; r['WARN'] = W
    r['pass'] = not F
    return r


def selftest():
    """A T2 kit piece: 1 m cube, two UV maps (UV0 tiling in metres at 2K, UV1 a mask-blend UV) and a corner colour attribute.
    This is the case that crashed the FishGame audit (second UV map + corner colour)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    me = bpy.data.meshes.new('selftest')
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for name in ('UVMap', 'UVMask'):
        bm.loops.layers.uv.new(name)
    cl = bm.loops.layers.color.new('BlendMask')
    u0 = bm.loops.layers.uv['UVMap']
    for f in bm.faces:
        for l in f.loops:
            l[u0].uv = (l.vert.co.x / 2.0 + 0.5, l.vert.co.y / 2.0 + 0.5)   # tile = 2 m on a 2K map = 1024 px/m
            l[cl] = (0.25, 0.5, 0.75, 1.0)
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('selftest', me); bpy.context.scene.collection.objects.link(o)
    r = audit_object(o, 'kit')
    assert r['uv_layers'] == ['UVMap', 'UVMask'], r['uv_layers']
    assert r['colour_attrs'] and r['colour_attrs'][0]['name'] == 'BlendMask', r['colour_attrs']
    assert r['tris'] == 12 and not r['FAIL'], r
    print('SELFTEST PASS tris=%d texel_ppm=%s uv=%s colour=%s' % (r['tris'], r['texel_ppm'], r['uv_layers'], [c['name'] for c in r['colour_attrs']]))
    o.data.materials.append(bpy.data.materials.new('a')); o.data.materials.append(bpy.data.materials.new('b')); o.data.materials.append(bpy.data.materials.new('c'))
    bpy.context.view_layer.update()
    r2 = audit_object(o, 'kit')
    assert any('material slots' in f for f in r2['FAIL']), r2
    print('SELFTEST PASS (FAIL path: 3 materials on a kit piece)')


def main(argv):
    if '--selftest' in argv:
        selftest(); return
    out = argv[0]; rest = argv[1:]
    cls = 'prop'; tex_res = 2048
    if '--class' in rest:
        i = rest.index('--class'); cls = rest[i + 1]; del rest[i:i + 2]
    if '--tex-res' in rest:
        i = rest.index('--tex-res'); tex_res = int(rest[i + 1]); del rest[i:i + 2]
    if cls not in BUDGET:
        sys.exit('unknown --class %s; choose from %s' % (cls, ', '.join(BUDGET)))
    rows = []
    for path in rest:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        ext = os.path.splitext(path)[1].lower()
        if ext == '.fbx':
            bpy.ops.import_scene.fbx(filepath=path, colors_type='LINEAR')
        elif ext in ('.glb', '.gltf'):
            bpy.ops.import_scene.gltf(filepath=path)
        elif ext == '.obj':
            bpy.ops.wm.obj_import(filepath=path)
        else:
            print('SKIP', path); continue
        for o in bpy.data.objects:
            if o.type == 'MESH':
                r = audit_object(o, cls, tex_res); r['file'] = os.path.basename(path); rows.append(r)
                print('AUDIT %-40s %-28s %s tris=%d dens=%.0f ppm=%s %s' % (r['file'][:40], o.name[:28], 'PASS' if r['pass'] else 'FAIL', r['tris'], r['dens'],
                                                                           r['texel_ppm'], '; '.join(r['FAIL'] + r['WARN'])[:200]))
    with open(out, 'w') as f:
        json.dump(rows, f, indent=1)


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
