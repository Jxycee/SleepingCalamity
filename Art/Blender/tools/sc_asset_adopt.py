# sc_asset_adopt.py (2026-10-25, asset skills): converts an external mesh (CC0 / CC BY GLB, FBX or OBJ from a checked source) into the SleepingCalamity model
# encoding, for permanent (non-seasonal) props: boats, crates, barrels, stonework, machinery. Living things (grass, leaves, reeds) stay generated, because
# their colour must follow the season's palette slot. See the sc-asset-adopt skill for when to use it and how to record the licence.
#
# Run:  blender -b --factory-startup --python Art/Blender/tools/sc_asset_adopt.py -- <source.glb> <out.fbx> --name Millpond_Prop_<Name>
#           [--size <metres>] [--axis height|length] [--tris <budget>] [--colours 8] [--slots 3,4,5,6,8,9,10] [--layer 0] [--ao-floor 0.5]
#           [--smooth-angle 40] [--yaw <deg>] [--report <json>] [--per-region 3] [--exclude Planter,FlowerBox]
#           [--rules "thatch:slot=5,layer=0;plaster:slot=11,layer=20,tri;stone:slot=6,layer=16,tri;timber:slot=6,tint=0.68/0.64/0.56,layer=3"]
#           [--regions "hull=4,deck=2"] [--part-tris "anchorchain=0,rope=600"] [--waterline <m>] [--preview-yaw] [--raw]
#           [--band-cuts hullpaint] [--mute 0.5] [--weld 0.0005] [--list-parts]
# Round 33: --band-cuts cuts the named regions along their painted band boundaries (after the decimation: split each edge where the texture's
# band changes, join the splits), so a livery's stripes stop zig-zagging; --band-planes uses horizontal planes at the band heights instead; --mute pulls every patch toward the nearest concept README hex in Lab and caps its chroma at the README's.
# Round 32 (asset duo): the steps are functions a 'make it ours' generator imports instead of patching: import_any (exclude, per-part budgets),
# prepare (join, the sc_mat face tag = source material, yaw, scale, waterline), decimate, sample, regions, bake_ao, encode, split_and_export
# (export_if_changed). --regions sets the tones per material (substring of its name), so one painted texture (a hull livery) splits into its bands;
# --part-tris decimates matching parts to that many tris before the join (0 drops them); --waterline puts the origin and the AO ground plane at
# that height above the keel; --preview-yaw writes a sheet of the source at yaw 0/90/180/270 (left to right) to pick the facing; --raw also
# writes <out>_raw.fbx (the imported mesh before decimation and encoding). Without these flags the output is as before (audits identical).
# Regions (duo round 6, A1-A4): each SOURCE MATERIAL is a region; colour patches are found inside a region (--per-region tones), never across,
# so fine-faceted parts (17k-tri thatch) don't turn into per-face confetti. --rules matches a substring of the material name (lowercase):
# slot= forces the palette slot, tint=r/g/b (with slot) sets the colour outright (kit constants, sc_tint.py), layer= the texture layer, tri sends the region to <out>_Tri.fbx (stone, slate, render: M_Millpond_Tri).
# --exclude drops source objects whose names contain any of the comma-separated parts (adopt the shell, generate the living parts).
# Steps (deterministic; no random draws):
#   1. import, apply transforms, join into one mesh; scale so the largest (or --axis) dimension is --size metres; origin at the bottom centre, front -Y
#      in Blender (= +z in Unity, the project's convention; checked round 32: the preview's three-quarter camera looks at -Y, so the model's front
#      must face -Y; don't reason from 'screen left', use --preview-yaw); --yaw turns it first (counter-clockwise seen from above)
#   2. decimate (collapse) to --tris when over it, symmetric along X
#   3. sample each face's albedo (the material's base colour texture at the face's UV centroid, else its base colour), in linear
#   4. simplify the colours to --colours painterly patches (k-means in linear RGB, fixed initialisation): photo noise becomes broad masses
#   5. fit each patch to the nearest permanent palette slot by chroma (sc_harbour PALETTE_SUMMER) and keep the rest as a tint (clamped 0.15-1.9)
#   6. bake per-vertex AO (hemisphere rays against the model and a ground plane), ao = floor + (1 - floor) x raw
#   7. write UVMap (box-projected metres), Layer (slot, --layer), Col (0.5 x ao x tint), smooth by angle, one material slot; export with the
#      project's FBX settings (sc_millpond_trees.export_fbx)
import bpy, bmesh, json, math, os, re, sys
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
# ArtPalette.asset: slots 0 Grass, 1 Foliage, 2 FoliageDark, 7 Reed, 14 Blossom, 15 FoliageAlt change with the season; these never do:
PERMANENT_SLOTS = [3, 4, 5, 6, 8, 9, 10, 11]   # Wood, WoodDark, Sand, Rock, Dock, Roof, Accent, Snow (white all year: white paint, gull-white)

def palette():
    src = open(os.path.join(ROOT, 'Art', 'Blender', 'sc_harbour.py'), encoding='utf-8').read()
    m = re.search(r'PALETTE_SUMMER\s*=\s*\[(.*?)\]\s*(#[^\n]*)?\n', src, re.S)
    return [tuple(float(x) for x in t) for t in re.findall(r'\(([\d.]+),\s*([\d.]+),\s*([\d.]+)\)', m.group(1))][:16]

def lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def opt(rest, name, default, cast=str):
    if name in rest:
        i = rest.index(name); v = rest[i + 1]; del rest[i:i + 2]; return cast(v)
    return default

def import_any(path, exclude=(), part_tris=None):
    # Import the source (glb, gltf, fbx, obj; a .blend is not read: use the dataset's .glb). exclude: drop objects whose names contain any of
    # these (lowercase) parts (anchor chains, quay bollards, planters). part_tris {name part: tris}: decimate a matching object to that many
    # tris before the join, or drop it at 0 (round 32: one global ratio left rope coils dense and fenders octagonal). Returns the mesh objects.
    ext = os.path.splitext(path)[1].lower()
    if ext in ('.glb', '.gltf'): bpy.ops.import_scene.gltf(filepath=path)
    elif ext == '.fbx': bpy.ops.import_scene.fbx(filepath=path)
    elif ext == '.obj': bpy.ops.wm.obj_import(filepath=path)
    else: raise SystemExit('unsupported source %s (glb, gltf, fbx or obj; for a .blend use the .glb beside it)' % path)
    bpy.context.view_layer.update()
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    if exclude:
        for o in [o for o in objs if any(e in o.name.lower() for e in exclude)]:
            bpy.data.objects.remove(o)
        objs = [o for o in bpy.data.objects if o.type == 'MESH']
    return _part_budgets(objs, part_tris)

def _part_budgets(objs, part_tris):
    for key, n in sorted((part_tris or {}).items()):
        for o in [o for o in objs if key in o.name.lower()]:
            if n <= 0:
                bpy.data.objects.remove(o); continue
            t0 = sum(len(p.vertices) - 2 for p in o.data.polygons)
            if t0 > n:
                if o.data.users > 1: o.data = o.data.copy()
                mod = o.modifiers.new('part_dec', 'DECIMATE'); mod.ratio = n / t0 * 0.98
                with bpy.context.temp_override(object=o, active_object=o):
                    bpy.ops.object.modifier_apply(modifier=mod.name)
        objs = [o for o in bpy.data.objects if o.type == 'MESH']
    return objs

def list_parts(src):
    # --list-parts (round 34): the source's parts before converting: name, tris, materials, islands (unwelded glTF parts show thousands of
    # islands: weld them before a per-part budget, --weld), so excludes and --part-tris are picked from the names
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = import_any(src)
    print('%-32s %7s %8s  %s' % ('part', 'tris', 'islands', 'materials'))
    for o in sorted(objs, key=lambda o: -len(o.data.polygons)):
        bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
        seen, islands = set(), 0
        for f in bm.faces:
            if f in seen: continue
            islands += 1; stack = [f]; seen.add(f)
            while stack:
                g = stack.pop()
                for e in g.edges:
                    for h in e.link_faces:
                        if h not in seen: seen.add(h); stack.append(h)
        bm.free()
        print('%-32s %7d %8d  %s' % (o.name[:32], sum(len(p.vertices) - 2 for p in o.data.polygons), islands, ', '.join(m.name for m in o.data.materials if m)))

def prepare(src, exclude=(), part_tris=None, yaw=0.0, size=0.0, axis='max', waterline=0.0, weld=0.0):
    # import, join into one mesh (transforms applied), tag each face with its source material (face attribute sc_mat = index into
    # ob['sc_mat_names']: the regions survive decimation and any later edit), --yaw, centre and scale (origin at the bottom centre, or at the
    # waterline: waterline m above the keel after scaling). Returns (ob, scale, dims before scaling).
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if weld > 0:   # --weld <m> (round 34): merge each part's coincident verts first; glTF imports split every UV seam, so a per-part collapse
        # breaks the parts apart (blind #13's wheels went to loose arcs). Opt-in: welding changes the decimation, so default outputs would move.
        objs0 = import_any(src, exclude)
        for o in objs0:
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=weld)
            bm.to_mesh(o.data); bm.free()
        objs = _part_budgets([o for o in bpy.data.objects if o.type == 'MESH'], part_tris)
    else:
        objs = import_any(src, exclude, part_tris)
    if not objs: raise SystemExit('no meshes in ' + src)
    for o in bpy.data.objects: o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(objs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    for o in [o for o in bpy.data.objects if o != ob]: bpy.data.objects.remove(o)
    me = ob.data
    if part_tris:   # a per-part decimation leaves verts no face uses (r31: 554 of them); the default path never runs it, so its output is as before
        bm = bmesh.new(); bm.from_mesh(me)
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(me); bm.free()
    ob['sc_mat_names'] = [(sl.material.name if sl.material else 'none') for sl in ob.material_slots] or ['none']
    at = me.attributes.new('sc_mat', 'INT', 'FACE')
    at.data.foreach_set('value', [p.material_index for p in me.polygons])
    if yaw: me.transform(Matrix.Rotation(math.radians(yaw), 4, 'Z'))
    xs = [v.co for v in me.vertices]
    lo = Vector((min(p.x for p in xs), min(p.y for p in xs), min(p.z for p in xs))); hi = Vector((max(p.x for p in xs), max(p.y for p in xs), max(p.z for p in xs)))
    dims = hi - lo
    ref = {'height': dims.z, 'length': max(dims.x, dims.y)}.get(axis, max(dims))
    s = size / ref if size > 0 and ref > 0 else 1.0
    me.transform(Matrix.Translation(Vector((-(lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))))
    me.transform(Matrix.Scale(s, 4))
    if waterline:
        me.transform(Matrix.Translation(Vector((0.0, 0.0, -waterline))))   # origin (and the AO ground plane) at the waterline
    return ob, s, dims

def decimate(ob, budget):
    # collapse the joined mesh to the budget (symmetric along X) when over it; returns the tris before
    me = ob.data
    tris0 = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris0 > budget:
        mod = ob.modifiers.new('dec', 'DECIMATE'); mod.ratio = budget / tris0 * 0.98; mod.use_symmetry = True; mod.symmetry_axis = 'X'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return tris0

def face_regions(ob):
    # each face's source material index (the sc_mat tag), and the material names
    me = ob.data
    at = me.attributes.get('sc_mat')
    if at is not None:
        v = [0] * len(me.polygons); at.data.foreach_get('value', v)
    else:
        v = [p.material_index for p in me.polygons]
    names = [n.lower() for n in ob.get('sc_mat_names', [])] or \
        [(ob.material_slots[i].material.name if ob.material_slots[i].material else 'none').lower() for i in range(len(ob.material_slots))] or ['none']
    return v, names

class Sampler:
    # base colour of a material: (image pixels, w, h, is_srgb) or a constant linear colour
    def __init__(self):
        self.cache = {}
        self.uv_name = {}   # material -> the UV map its base-colour texture reads, when a UV Map node names one

    def material(self, mat):
        if mat in self.cache: return self.cache[mat]
        res = ('const', (0.5, 0.5, 0.5))
        if mat and mat.use_nodes:
            bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if bsdf:
                sock = bsdf.inputs['Base Color']
                img = None
                if sock.is_linked:
                    stack = [sock.links[0].from_node]
                    while stack and img is None:
                        n = stack.pop()
                        if n.type == 'TEX_IMAGE' and n.image:
                            img = n.image
                            # the UV map the texture reads (round 33, blind #13: a source with three maps bound UVMap.002 through a UV Map
                            # node; sampling the active map read the wrong place and clamped every tint)
                            vec = n.inputs.get('Vector')
                            if vec and vec.is_linked and vec.links[0].from_node.type == 'UVMAP' and vec.links[0].from_node.uv_map:
                                self.uv_name[mat] = vec.links[0].from_node.uv_map
                        else: stack += [l.from_node for i in n.inputs for l in i.links]
                if img and img.size[0] > 0:
                    w, h = img.size
                    px = [0.0] * (w * h * 4); img.pixels.foreach_get(px)
                    srgb = img.colorspace_settings.name.lower().startswith('srgb') and not img.is_float
                    res = ('img', (px, w, h, srgb))
                else:
                    res = ('const', tuple(sock.default_value)[:3])
        elif mat:
            res = ('const', tuple(mat.diffuse_color)[:3])
        self.cache[mat] = res
        return res

    def face(self, mat, uv):
        kind, d = self.material(mat)
        if kind == 'const' or uv is None: return d if kind == 'const' else (0.5, 0.5, 0.5)
        px, w, h, srgb = d
        u, v = uv[0] % 1.0, uv[1] % 1.0
        x, y = min(w - 1, int(u * w)), min(h - 1, int(v * h))
        acc = [0.0, 0.0, 0.0]; n = 0
        for dx in (-2, 0, 2):            # a small box filter: the patch colour, not a single texel
            for dy in (-2, 0, 2):
                xx, yy = min(w - 1, max(0, x + dx)), min(h - 1, max(0, y + dy))
                i = (yy * w + xx) * 4
                for k in range(3): acc[k] += px[i + k]
                n += 1
        c = tuple(a / n for a in acc)
        return lin(c) if srgb else c

def kmeans(cols, weights, k, iters=12):
    # deterministic: centres start at k colours spread over the luminance order
    if len(cols) <= k: return list(range(len(cols))), list(cols)
    # deterministic farthest-point initialisation over the distinct colours (many faces share one constant material colour, so spreading
    # over the luminance order would start several centres on the same colour and leave them empty)
    distinct = sorted(set((round(c[0], 4), round(c[1], 4), round(c[2], 4)) for c in cols))
    if len(distinct) <= k:
        centres = list(distinct)
        k = len(centres)
    else:
        wsum = {}
        for c, w in zip(cols, weights):
            key = (round(c[0], 4), round(c[1], 4), round(c[2], 4)); wsum[key] = wsum.get(key, 0.0) + w
        centres = [max(distinct, key=lambda c: wsum[c])]          # the largest area first
        while len(centres) < k:
            centres.append(max(distinct, key=lambda c: min(sum((c[t] - q[t]) ** 2 for t in range(3)) for q in centres)))
    assign = [0] * len(cols)
    for _ in range(iters):
        for i, c in enumerate(cols):
            assign[i] = min(range(k), key=lambda j: sum((c[t] - centres[j][t]) ** 2 for t in range(3)))
        sums = [[0.0, 0.0, 0.0, 0.0] for _ in range(k)]
        for i, c in enumerate(cols):
            s = sums[assign[i]]; wgt = weights[i]
            s[0] += c[0] * wgt; s[1] += c[1] * wgt; s[2] += c[2] * wgt; s[3] += wgt
        centres = [(s[0] / s[3], s[1] / s[3], s[2] / s[3]) if s[3] > 0 else centres[j] for j, s in enumerate(sums)]
    return assign, centres

def fit_slot(c, pal, slots):
    # nearest slot by chroma (colour direction), then the tint carries the value; a near-grey patch goes to Rock (6)
    def chroma(x):
        s = sum(x) + 1e-6; return (x[0] / s, x[1] / s, x[2] / s)
    cc = chroma(c)
    def cost(j):
        # hue distance, plus how far the tint would have to clip (a white trim must not land on a dark slot at tint 1.9)
        sl = lin(pal[j]); t = [c[i] / max(sl[i], 1e-3) for i in range(3)]
        clip = sum(max(0.0, x - 1.9) + max(0.0, 0.15 - x) for x in t)
        # (round 34, tried and dropped: a penalty on cool tints for warm patches, so dark oak would leave Dock. slot x tint reproduces the
        # sampled colour exactly when nothing clamps, so oak on Dock already renders as oak; the penalty moved soil and a window glow onto
        # slots where the tint clamps at 0.15, a worse colour)
        return 10.0 * sum((cc[i] - chroma(sl)[i]) ** 2 for i in range(3)) + clip
    best = min(slots, key=cost)
    sl = lin(pal[best])
    tint = tuple(max(0.15, min(1.9, c[t] / max(sl[t], 1e-3))) for t in range(3))
    return best, tint

def bake_ao(me, mw, rays=32, dist=1.0, ground_z=0.0):
    bm = bmesh.new(); bm.from_mesh(me); bm.transform(mw)
    # a ground plane occluder under the model
    r = dist * 4
    g = [bm.verts.new((x, y, ground_z - 0.001)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))]
    bm.faces.new(g)
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    # fixed hemisphere directions (Fibonacci), no random draws
    dirs = []
    for i in range(rays):
        z = (i + 0.5) / rays; rr = math.sqrt(max(0.0, 1 - z * z)); a = i * 2.399963
        dirs.append(Vector((rr * math.cos(a), rr * math.sin(a), z)))
    out = []
    for v in me.vertices:
        p = mw @ v.co; n = (mw.to_3x3() @ v.normal).normalized()
        t = n.orthogonal().normalized(); b = n.cross(t)
        occ = 0.0
        for d in dirs:
            w = (t * d.x + b * d.y + n * d.z).normalized()
            hit = bvh.ray_cast(p + n * 0.002, w, dist)
            if hit[0] is not None: occ += 1.0 - 0.6 * (hit[3] / dist)
        out.append(max(0.0, 1.0 - occ / rays))
    return out

def _lab(c):
    # linear RGB -> CIE Lab (D65)
    r, g, b = c
    X = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.9505; Y = 0.2126 * r + 0.7152 * g + 0.0722 * b; Z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.089
    f = lambda t: t ** (1 / 3) if t > 0.008856 else 7.787 * t + 16 / 116
    fx, fy, fz = f(X), f(Y), f(Z)
    return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz))

def _unlab(L, a, b):
    fy = (L + 16) / 116; fx = fy + a / 500; fz = fy - b / 200
    g = lambda t: t ** 3 if t ** 3 > 0.008856 else (t - 16 / 116) / 7.787
    X, Y, Z = g(fx) * 0.9505, g(fy), g(fz) * 1.089
    return (max(0.0, 3.2406 * X - 1.5372 * Y - 0.4986 * Z), max(0.0, -0.9689 * X + 1.8758 * Y + 0.0415 * Z), max(0.0, 0.0557 * X - 0.2040 * Y + 1.0570 * Z))

def concept_palette():
    # the Millpond concept README's measured hexes (Art/Design/Concepts/Millpond/README.md), as Lab
    src = open(os.path.join(ROOT, 'Art', 'Design', 'Concepts', 'Millpond', 'README.md'), encoding='utf-8').read()
    hexes = sorted(set(h.lower() for h in re.findall(r'#([0-9a-fA-F]{6})\b', src)))
    return [_lab(lin(tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)))) for h in hexes]

def mute(fits, amount, pal=None):
    # --mute (round 33): pull each patch's rendered colour (lin(slot) x tint) toward the nearest concept README hex in Lab by `amount`
    # (0..1), then cap its chroma at the README's highest: the converter's straight fit keeps a model's CG saturation, the paintings don't.
    # The slot stays; the tint is recomputed (clamped 0.15-1.9). amount 0 returns fits unchanged.
    if amount <= 0: return fits
    pal = pal or palette()
    cp = concept_palette()
    cmax = max(math.hypot(a, b) for _, a, b in cp)
    out = []
    for slot, tint in fits:
        sl = lin(pal[slot])
        L, a, b = _lab(tuple(sl[i] * tint[i] for i in range(3)))
        tL, ta, tb = min(cp, key=lambda q: (q[0] - L) ** 2 + (q[1] - a) ** 2 + (q[2] - b) ** 2)
        L, a, b = L + (tL - L) * amount, a + (ta - a) * amount, b + (tb - b) * amount
        c = math.hypot(a, b)
        if c > cmax:
            a, b = a * cmax / c, b * cmax / c
        rgb = _unlab(L, a, b)
        out.append((slot, tuple(max(0.15, min(1.9, rgb[i] / max(sl[i], 1e-3))) for i in range(3))))
    return out

def band_cuts(ob, keys, k=4, max_cuts=6):
    # --band-cuts (round 33): straight paint bands on a part whose one texture carries several (a hull livery). For each region whose material
    # name contains a key: sample its faces' albedo, split them into k bands (k-means), order the bands by mean height and find the height
    # between each pair of neighbours that best separates them; then bisect that region's faces with horizontal planes at those heights, so
    # every face lies inside one band (no zig-zag edges). Run after the decimation (a collapse would undo the cuts). Returns (heights, tris added).
    me = ob.data
    t0 = sum(len(p.vertices) - 2 for p in me.polygons)
    region_of, names = face_regions(ob)
    cols, wts = sample(ob)
    heights = []
    for ri in sorted(set(region_of)):
        if not any(key in (names[ri] if ri < len(names) else '') for key in keys):
            continue
        idx = [i for i in range(len(cols)) if region_of[i] == ri]
        a_r, c_r = kmeans([cols[i] for i in idx], [wts[i] for i in idx], k)
        zs = [me.polygons[i].center.z for i in idx]
        bands = {}
        for j, z in zip(a_r, zs): bands.setdefault(j, []).append(z)
        order = sorted(bands, key=lambda j: sum(bands[j]) / len(bands[j]))
        for lo_b, hi_b in zip(order, order[1:]):
            both = sorted([(z, 0) for z in bands[lo_b]] + [(z, 1) for z in bands[hi_b]])
            best, err = None, None
            n1 = sum(1 for _, t in both if t == 1)
            below_hi, above_lo = 0, len(bands[lo_b])
            for zi, (z, t) in enumerate(both):   # threshold after element zi: misplaced = high-band below it + low-band above it
                if t == 1: below_hi += 1
                else: above_lo -= 1
                e = below_hi + above_lo
                if err is None or e < err:
                    err, best = e, (z + both[min(zi + 1, len(both) - 1)][0]) / 2
            if best is not None: heights.append((ri, best))
    heights = heights[:max_cuts * 4]
    bm = bmesh.new(); bm.from_mesh(me)
    lay = bm.faces.layers.int.get('sc_mat')
    for ri, z in heights:
        bm.faces.ensure_lookup_table()
        faces = [f for f in bm.faces if (f[lay] if lay else f.material_index) == ri]
        geom = list({v for f in faces for v in f.verts}) + list({e for f in faces for e in f.edges}) + faces
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0.0, 0.0, z), plane_no=(0.0, 0.0, 1.0))
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(me); bm.free()
    return [round(z, 3) for _, z in heights], sum(len(p.vertices) - 2 for p in me.polygons) - t0

def band_contours(ob, keys, k=4):
    # --band-cuts (round 33, second form): cut the named regions along their painted band boundaries themselves. Generated sources (image-to-3d)
    # paint a livery into an auto-unwrapped texture: its bands follow the sheer, not horizontal planes or UV rows, so planar cuts can't
    # straighten them. Here each region's faces are split into k bands (k-means of the sampled albedo); every face corner is labelled by the
    # texture under its UV; an edge whose two ends land in different bands is split where the label changes (sampled along the edge in UV), and
    # the two new verts on a face are joined, so the cut follows the painted edge at the mesh's own resolution. Run after the decimation.
    # Returns (cuts made, tris added).
    me = ob.data
    t0 = sum(len(p.vertices) - 2 for p in me.polygons)
    region_of, names = face_regions(ob)
    cols, wts = sample(ob)
    sam = Sampler()
    bm = bmesh.new(); bm.from_mesh(me)
    lay = bm.faces.layers.int.get('sc_mat'); uvl = bm.loops.layers.uv.active
    if uvl is None:
        bm.free(); return 0, 0
    made = 0
    for ri in sorted(set(region_of)):
        if not any(key in (names[ri] if ri < len(names) else '') for key in keys):
            continue
        idx = [i for i in range(len(cols)) if region_of[i] == ri]
        _, centres = kmeans([cols[i] for i in idx], [wts[i] for i in idx], k)
        mat = ob.material_slots[me.polygons[idx[0]].material_index].material if ob.material_slots else None
        def label(uv):
            c = sam.face(mat, (uv[0], uv[1]))
            return min(range(len(centres)), key=lambda j: sum((c[t] - centres[j][t]) ** 2 for t in range(3)))
        bm.faces.ensure_lookup_table()
        faces = [f for f in bm.faces if (f[lay] if lay else f.material_index) == ri]
        fset = set(faces)
        split_at = {}   # edge -> fraction from edge.verts[0]
        for f in faces:
            for lp in f.loops:
                e, nx = lp.edge, lp.link_loop_next
                if e in split_at: continue
                a, b = lp[uvl].uv.copy(), nx[uvl].uv.copy()
                la, lb = label(a), label(b)
                if la == lb: continue
                prev = la
                for st in range(1, 9):
                    t = st / 8.0
                    if label(a.lerp(b, t)) != prev:
                        frac = t - 1 / 16.0
                        split_at[e] = frac if e.verts[0] == lp.vert else 1.0 - frac
                        break
        new_on = {}
        for e, frac in split_at.items():
            if not e.is_valid: continue
            faces_e = [f for f in e.link_faces if f in fset]
            ne, nv = bmesh.utils.edge_split(e, e.verts[0], max(0.02, min(0.98, frac)))
            for f in nv.link_faces:
                new_on.setdefault(f, []).append(nv)
        for f, vs in new_on.items():
            if f.is_valid and len(vs) == 2 and f in fset:
                try:
                    bmesh.ops.connect_verts(bm, verts=vs); made += 1
                except Exception:
                    pass
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(me); bm.free()
    return made, sum(len(p.vertices) - 2 for p in me.polygons) - t0

def sample(ob):
    # 3. each face's albedo (linear) and area weight
    me = ob.data
    sam = Sampler()
    uvl0 = me.uv_layers.active
    cols, wts = [], []
    for p in me.polygons:
        mat = ob.material_slots[p.material_index].material if ob.material_slots else None
        uv = None
        sam.material(mat)
        uvl = me.uv_layers.get(sam.uv_name.get(mat, ''), uvl0) if sam.uv_name.get(mat) else uvl0
        if uvl:
            uu = [uvl.data[li].uv for li in p.loop_indices]; uv = (sum(u.x for u in uu) / len(uu), sum(u.y for u in uu) / len(uu))
        cols.append(sam.face(mat, uv)); wts.append(p.area + 1e-9)
    return cols, wts

def parse_rules(spec):
    rules = []
    for part in [r for r in spec.split(';') if r.strip()]:
        key, _, sp = part.partition(':'); d = dict(key=key.strip().lower(), tri=False)
        for kv in sp.split(','):
            kv = kv.strip()
            if kv == 'tri': d['tri'] = True
            elif kv.startswith('tint='): d['tint'] = tuple(float(x) for x in kv[5:].split('/'))   # tint=r/g/b (sc_tint.py)
            elif '=' in kv: kk, vv = kv.split('='); d[kk.strip()] = int(vv)
        rules.append(d)
    return rules

def regions(ob, cols, wts, k=8, per_region=1, region_k=None, rules=(), slots=PERMANENT_SLOTS):
    # 4-5. painterly patches per REGION (source material): k-means of the faces' sampled albedo inside each region, so one painted texture
    # splits into its bands (a hull livery: antifoul, boot stripe, white band, topsides) without mixing regions. region_k {material name part:
    # tones} overrides per_region for matching materials (--regions 'hull=4,deck=2'). Each patch -> (slot, tint) by the rules or fit_slot.
    # Returns (fits, assign per face, region per face, material names, rule_for).
    pal = palette()
    region_of, mat_names = face_regions(ob)
    def rule_for(ri):
        nm = mat_names[ri] if ri < len(mat_names) else 'none'
        return next((r for r in rules if r['key'] in nm), None)
    def tones_for(ri):
        nm = mat_names[ri] if ri < len(mat_names) else 'none'
        return next((n for key, n in sorted((region_k or {}).items()) if key in nm), per_region)
    assign = [0] * len(cols); fits = []; patch_region = []
    for ri in sorted(set(region_of)):
        idx = [i for i in range(len(cols)) if region_of[i] == ri]
        kk = tones_for(ri) if k > 0 else 1
        a_r, c_r = kmeans([cols[i] for i in idx], [wts[i] for i in idx], kk)
        rule = rule_for(ri)
        base = len(fits)
        for c in c_r:
            if rule and 'tint' in rule and 'slot' in rule:
                fits.append((rule['slot'], rule['tint']))
            elif rule and 'slot' in rule:
                sl = lin(pal[rule['slot']])
                fits.append((rule['slot'], tuple(max(0.15, min(1.9, c[t] / max(sl[t], 1e-3))) for t in range(3))))
            else:
                fits.append(fit_slot(c, pal, slots))
            patch_region.append(ri)
        for j, i in enumerate(idx): assign[i] = base + a_r[j]
    return fits, assign, region_of, mat_names, rule_for, patch_region

def encode(ob, fits, assign, region_of, rule_for, raw, layer=0, floor=0.5, angle=40.0, name='Millpond_Prop_Adopted'):
    # 7. the SleepingCalamity encoding: UVMap (box-projected metres), Layer (slot, layer), Col (0.5 x ao x tint). ONE slot per face, forced: every corner
    # of a face carries its patch's slot (the shader would blend slot ids across a face whose corners differ). One material, smooth by angle.
    me = ob.data
    while len(me.uv_layers):   # by index, not through a saved list: removing one invalidates the others' references (blind #13: 3 maps)
        me.uv_layers.remove(me.uv_layers[0])
    while len(me.color_attributes):
        me.color_attributes.remove(me.color_attributes[0])
    uv0 = me.uv_layers.new(name='UVMap'); uv1 = me.uv_layers.new(name='Layer')
    ca = me.color_attributes.new('Col', 'FLOAT_COLOR', 'CORNER')
    n = len(me.loops)
    a0 = [0.0] * (2 * n); a1 = [0.0] * (2 * n); cl = [0.0] * (4 * n)
    for pi, p in enumerate(me.polygons):
        slot, tint = fits[assign[pi]]
        nn = p.normal; ax = max(range(3), key=lambda t: abs(nn[t]))
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            u, v = [(co.y, co.z), (co.x, co.z), (co.x, co.y)][ax]   # box projection in metres
            a0[2 * li], a0[2 * li + 1] = u, v
            rr = rule_for(region_of[pi])
            a1[2 * li], a1[2 * li + 1] = float(slot), float(rr['layer'] if rr and 'layer' in rr else layer)
            ao = floor + (1.0 - floor) * raw[me.loops[li].vertex_index]
            cl[4 * li:4 * li + 4] = (min(1.0, 0.5 * ao * tint[0]), min(1.0, 0.5 * ao * tint[1]), min(1.0, 0.5 * ao * tint[2]), 1.0)
    uv0.data.foreach_set('uv', a0); uv1.data.foreach_set('uv', a1); ca.data.foreach_set('color', cl)
    me.uv_layers.active = uv0; uv0.active_render = True
    me.color_attributes.active_color = ca
    ob.data.materials.clear()
    m = bpy.data.materials.get('M_Toon_Base') or bpy.data.materials.new('M_Toon_Base'); me.materials.append(m)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    ob.name = me.name = name

def export_fbx(o, path):
    # the project's FBX settings (sc_millpond_trees.export_fbx)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                             colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, add_leaf_bones=False,
                             bake_anim=False, use_custom_props=False)

_H = None
def export_if_changed(o, path):
    # export, but keep an existing file whose content is the same (sc_harbour.fbx_equivalent: the FBX header carries a timestamp, so
    # identical models otherwise differ in bytes). Returns 'kept' or 'written'.
    global _H
    tmp = path[:-4] + '.tmp.fbx' if path.lower().endswith('.fbx') else path + '.tmp.fbx'
    export_fbx(o, tmp)
    if os.path.exists(path):
        if _H is None:
            import importlib.util
            sp = importlib.util.spec_from_file_location('sc_harbour', os.path.join(ROOT, 'Art', 'Blender', 'sc_harbour.py'))
            _H = importlib.util.module_from_spec(sp); sp.loader.exec_module(_H)
        if _H.fbx_equivalent(path, tmp):
            os.remove(tmp); return 'kept'
    os.replace(tmp, path)
    return 'written'

def split_and_export(ob, out, rule_for, region_of, name):
    # tri regions (rules with ,tri) go to <out>_Tri.fbx (M_Millpond_Tri), the rest to <out>
    me = ob.data
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    tri_faces = [pi for pi in range(len(me.polygons)) if (lambda r: r and r['tri'])(rule_for(region_of[pi]))]
    if tri_faces and len(tri_faces) < len(me.polygons):
        tri_ob = ob.copy(); tri_ob.data = me.copy(); tri_ob.name = tri_ob.data.name = name + '_Tri'
        bpy.context.scene.collection.objects.link(tri_ob)
        keep = set(tri_faces)
        for o2, drop in ((ob, keep), (tri_ob, set(range(len(me.polygons))) - keep)):
            bm = bmesh.new(); bm.from_mesh(o2.data); bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm, geom=[bm.faces[i] for i in sorted(drop)], context='FACES')
            bm.to_mesh(o2.data); bm.free()
        return [export_if_changed(ob, out), export_if_changed(tri_ob, out[:-4] + '_Tri.fbx' if out.lower().endswith('.fbx') else out + '_Tri.fbx')]
    return [export_if_changed(ob, out)]

def preview_yaws(src, out, exclude, part_tris, size, axis):
    # --preview-yaw: the source at yaw 0 / 90 / 180 / 270 side by side on one sc_asset_preview sheet (left to right), unencoded, so the facing
    # is picked by eye in one run: the right yaw shows the model's FRONT (bow, door, face) to the three-quarter view (top left), which looks at
    # -Y in Blender (+z in Unity).
    tmp = []
    for yw in (0, 90, 180, 270):
        ob, s, dims = prepare(src, exclude, part_tris, yw, size, axis)
        p = os.path.splitext(out)[0] + '_yaw%03d.glb' % yw
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.ops.export_scene.gltf(filepath=p, use_selection=True, export_format='GLB')
        tmp.append(p)
    import importlib.util
    sp = importlib.util.spec_from_file_location('sc_asset_preview', os.path.join(HERE, 'sc_asset_preview.py'))
    P = importlib.util.module_from_spec(sp); sp.loader.exec_module(P)
    P.main([out, '--size', '512'] + tmp)
    print('PREVIEW-YAW', out, 'left to right: yaw 0, 90, 180, 270 (pick the one whose front faces the three-quarter camera)')

def main(argv):
    rest = list(argv)
    src, out = rest[0], rest[1]; rest = rest[2:]
    name = opt(rest, '--name', 'Millpond_Prop_Adopted')
    size = opt(rest, '--size', 0.0, float); axis = opt(rest, '--axis', 'max')
    budget = opt(rest, '--tris', 6000, int); k = opt(rest, '--colours', 8, int)
    slots = [int(s) for s in opt(rest, '--slots', ','.join(map(str, PERMANENT_SLOTS))).split(',')]
    layer = opt(rest, '--layer', 0, int); floor = opt(rest, '--ao-floor', 0.5, float)
    angle = opt(rest, '--smooth-angle', 40.0, float); yaw = opt(rest, '--yaw', 0.0, float)
    report = opt(rest, '--report', None)
    per_region = opt(rest, '--per-region', 1, int)   # 1 tone per material by default: more than 1 makes fine-faceted parts a per-face mosaic (duo r7); add tone with the paint pass
    exclude = [e.lower() for e in opt(rest, '--exclude', '').split(',') if e]
    region_k = {kv.split('=')[0].strip().lower(): int(kv.split('=')[1]) for kv in opt(rest, '--regions', '').split(',') if '=' in kv}
    part_tris = {kv.split('=')[0].strip().lower(): int(kv.split('=')[1]) for kv in opt(rest, '--part-tris', '').split(',') if '=' in kv}
    waterline = opt(rest, '--waterline', 0.0, float)
    weld = opt(rest, '--weld', 0.0, float)
    cut_keys = [x.strip().lower() for x in opt(rest, '--band-cuts', '').split(',') if x.strip()]
    mute_amt = opt(rest, '--mute', 0.0, float)
    rules = parse_rules(opt(rest, '--rules', ''))
    if '--list-parts' in rest:
        return list_parts(src)
    if '--preview-yaw' in rest:
        return preview_yaws(src, out, exclude, part_tris, size, axis)
    ob, s, dims = prepare(src, exclude, part_tris, yaw, size, axis, waterline, weld)
    if '--raw' in rest:   # the imported, scaled mesh before decimation and encoding, for inspection
        export_fbx(ob, os.path.splitext(out)[0] + '_raw.fbx')
    tris0 = decimate(ob, budget)
    cuts = None
    if cut_keys:
        kb = next((n for kk, n in region_k.items() if any(kk in c or c in kk for c in cut_keys)), 4)
        if '--band-planes' in rest:   # horizontal planes (a livery painted level with the waterline on a hand-made UV)
            cuts = band_cuts(ob, cut_keys, kb); print('BAND-CUTS planes at %s, +%d tris' % cuts)
        else:                          # default: along the painted boundaries themselves
            cuts = band_contours(ob, cut_keys, kb); print('BAND-CUTS %d contour cuts, +%d tris' % cuts)
    me = ob.data
    cols, wts = sample(ob)
    fits, assign, region_of, mat_names, rule_for, patch_region = regions(ob, cols, wts, k, per_region, region_k, rules, slots)
    fits = mute(fits, mute_amt)
    # 6. AO (the ground plane at z 0: the keel, or the waterline with --waterline)
    diag = (Vector(dims) * s).length
    raw = bake_ao(me, ob.matrix_world, dist=max(0.2, 0.3 * diag))
    encode(ob, fits, assign, region_of, rule_for, raw, layer, floor, angle, name)
    tris1 = sum(len(p.vertices) - 2 for p in me.polygons)
    written = split_and_export(ob, out, rule_for, region_of, name)
    rep = dict(source=os.path.basename(src), out=out, name=name, scale=round(s, 4), size=[round(x * s, 3) for x in dims], tris_in=tris0, tris_out=tris1,
               patches=[dict(region=mat_names[patch_region[j]] if patch_region[j] < len(mat_names) else 'none', slot=f[0], tint=[round(t, 3) for t in f[1]],
                             share=round(sum(wts[i] for i in range(len(cols)) if assign[i] == j) / sum(wts), 3)) for j, f in enumerate(fits)])
    print('ADOPT', json.dumps(rep), written)
    if report:
        json.dump(rep, open(report, 'w'), indent=1)

if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1:])
