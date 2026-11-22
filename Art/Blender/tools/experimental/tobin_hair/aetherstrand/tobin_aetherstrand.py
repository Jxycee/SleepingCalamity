# Tobin hair, AetherStrand trial (side-by-side A/B against the HairDeck cards, 2026-11-22).
# Builds AetherStrand sculpted locks on the SAME cut as the installed HairDeck hair, so the A/B compares lock geometry, shading and motion,
# not a new hairstyle:
#   1. import the installed TobinHairMesh.fbx (HairDeck cards + scalp shell + hair_<k>_<i> rig);
#   2. merge by distance -> one island per card; each card's spine = mean position per uv.v bin (v runs root 0 -> tip 1), width = measured card width;
#   3. write a PresetGeometry.json (head-bone-local points, zone, width, thickness) and run the vendored, unmodified AetherStrand generator on it
#      (vendor/aetherstrand_hair_cards.py, operator aetherstrand.generate_sculpted_hair: crescent locks + k-means Hair_<Zone>_<c>_<k> chains);
#   4. integration patches (logged for the plugin author in Reference/sessions/hair-aetherstrand/neon-feedback.md):
#      - the generator builds its rig at the world origin: the rig is moved to the hair rig's Head bone;
#      - the generator weights each lock's root ring to chain bone 1, so roots swing off the scalp: root ring re-weighted to Head (--no-root-fix to skip);
#   5. keep the scalp shell (same cap as the HairDeck variant, weighted to Head) so both variants hide the scalp the same way;
#   6. export FBX (rig_export.py settings + linear vertex colours + path_mode STRIP) and Workbench silhouette previews (cards vs locks).
# Run (Blender 5.x headless):
#   blender -b -P tobin_aetherstrand.py -- <out_dir> [--fbx <export path>] [--chains 44] [--bones 5] [--no-root-fix] [--thick 0.38] [--width 0.85]
# Lock parameters follow Neon's HairDeck-to-preset recipe (vendor/extract_hairdeck_to_preset_json.py, neon_07): width x0.85,
# thickness clamp(w*0.38, 6-18 mm), core ribbons (uv0.x > 1.5) layerAO 0.72 vs outer 0.94, seed = uv1.x. Spines are sampled in uv.v bins per card
# island instead of Neon's round(v, 2) row grouping, so the lock count always equals the card count.
import bpy, bmesh, sys, os, json, math, random, importlib.util
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, '..', '..', '..', '..', '..', '..'))
SRC_FBX = os.path.join(REPO, 'Assets/_Project/Content/Characters/TobinBase/HairMesh/TobinHairMesh.fbx')
DEF_FBX = os.path.join(REPO, 'Assets/_Project/Content/Characters/TobinBase/AetherHair/TobinAetherHair.fbx')

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = os.path.abspath(argv[0]) if argv and not argv[0].startswith('--') else os.path.join(HERE, 'out')
def opt(name, default):
    return argv[argv.index(name) + 1] if name in argv else default
FBX_OUT = opt('--fbx', DEF_FBX)
CHAINS = int(opt('--chains', 44)); BONES = int(opt('--bones', 5)); THICK = float(opt('--thick', 0.38)); WIDTH_SCALE = float(opt('--width', 0.85))
ROOT_FIX = '--no-root-fix' not in argv
os.makedirs(OUT, exist_ok=True)
BINS = 12

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SRC_FBX)
src_arm = bpy.data.objects['HairArmature']; cards_obj = bpy.data.objects['Tobin_Hair']; shell = bpy.data.objects['Tobin_HairShell']
HEAD = src_arm.matrix_world @ src_arm.data.bones['Head'].head_local
print('HEAD bone (world)', tuple(round(c, 4) for c in HEAD))

# front direction: the shell has no face, so its centroid sits behind the head bone
sv = np.array([(shell.matrix_world @ v.co)[:] for v in shell.data.vertices])
FRONT = -1.0 if sv[:, 1].mean() > HEAD.y else 1.0   # sign of +Y that points to the face
print('FRONT sign (+Y)', FRONT, 'shell centroid y-head', round(sv[:, 1].mean() - HEAD.y, 4))

# ---- 1. card spines from the installed cards ----
bm = bmesh.new(); bm.from_mesh(cards_obj.data); bm.transform(cards_obj.matrix_world)
uvl = bm.loops.layers.uv['UVMap']; uvc = bm.loops.layers.uv['card']
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
bm.faces.ensure_lookup_table()
seen = set(); cards = []
for f in bm.faces:
    if f.index in seen: continue
    st = [f]; seen.add(f.index); comp = [f]
    while st:
        g = st.pop()
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen: seen.add(h.index); st.append(h); comp.append(h)
    cards.append(comp)
print('CARDS', len(cards))

locks = []; widths = []
for ci, comp in enumerate(cards):
    loops = [l for f in comp for l in f.loops]
    V = np.array([l[uvl].uv[1] for l in loops]); P = np.array([l.vert.co[:] for l in loops])
    core = float(np.mean([l[uvl].uv[0] for l in loops])) > 1.5   # HairDeck: uv0.x 2..3 = core ribbons
    seed = float(np.mean([l[uvc].uv[0] for l in loops]))          # uv1.x = per-strand random
    v0, v1 = V.min(), V.max()
    if v1 - v0 < 1e-4: continue
    t = (V - v0) / (v1 - v0)
    pts = []; wb = []
    for b in range(BINS):
        lo, hi = b / BINS, (b + 1) / BINS
        m = (t >= lo - 1e-6) & (t <= hi + 1e-6)
        if b == 0: m = t <= 0.04 + 1e-6       # root end
        if b == BINS - 1: m = t >= 0.96 - 1e-6  # tip end
        if not m.any(): continue
        q = P[m]; pts.append(q.mean(0))
        if len(q) > 1: wb.append(float(np.max(np.linalg.norm(q - q.mean(0), axis=1))) * 2.0)
    if len(pts) < 3: continue
    width = float(np.median(wb[: max(1, len(wb) // 2)])) if wb else 0.01
    widths.append(width)
    loc = [Vector(p) - HEAD for p in pts]   # head-bone-local (Blender axes)
    r = loc[0]; fwd = FRONT * r.y
    locks.append(dict(root=r, fwd=fwd, pts=loc, width=width * WIDTH_SCALE, seed=seed, core=core))

zmax = max(l['root'].z for l in locks)
for l in locks:
    r = l['root']
    if l['fwd'] > 0.035: z = 'Bangs'
    elif r.z > zmax - 0.03 and abs(r.x) < 0.05 and abs(l['fwd']) <= 0.035: z = 'Crown'
    elif l['fwd'] < -0.03: z = 'Rear'
    else: z = 'Sides'
    l['zone'] = z
zc = {}
for l in locks: zc[l['zone']] = zc.get(l['zone'], 0) + 1
print('LOCKS', len(locks), 'zones', zc, 'card width mm median', round(1000 * float(np.median(widths)), 1), 'p10/p90', round(1000 * float(np.percentile(widths, 10)), 1), round(1000 * float(np.percentile(widths, 90)), 1))

preset = {'locks': [dict(zone=l['zone'], points=[list(p) for p in l['pts']], width=l['width'],
                         thickness=float(min(0.018, max(0.006, l['width'] * THICK))), seed=l['seed'],
                         layerAO=0.72 if l['core'] else 0.94) for l in locks]}
PRESET = os.path.join(OUT, 'TobinPresetGeometry.json')
json.dump(preset, open(PRESET, 'w'))

# ---- 2. run the vendored AetherStrand generator (unmodified) ----
spec = importlib.util.spec_from_file_location('aetherstrand_hair_cards', os.path.join(HERE, 'vendor', 'aetherstrand_hair_cards.py'))
AS = importlib.util.module_from_spec(spec); spec.loader.exec_module(AS); AS.register()
for o in bpy.context.selected_objects: o.select_set(False)
bpy.context.view_layer.objects.active = None   # no active mesh: roots already sit on the scalp (no BVH projection)
res = bpy.ops.aetherstrand.generate_sculpted_hair(preset_geometry_json=PRESET, target_chain_count=CHAINS, bones_per_chain=BONES)
print('GENERATE', res)
arm = bpy.data.objects['AetherStrand_HairRig']; hair = bpy.data.objects['AetherStrand_SculptedHairMesh']
hair.name = 'Tobin_AetherHair'; hair.data.name = 'Tobin_AetherHair'
arm.location = HEAD   # patch: the generator builds its rig at the origin
bpy.context.view_layer.update()

# ---- 3. root ring -> Head (patch) ----
if ROOT_FIX:
    me = hair.data
    col = me.color_attributes.get('Col') or me.color_attributes[0]
    rootv = set()
    if col.domain == 'CORNER':
        for li, lp in enumerate(me.loops):
            if col.data[li].color[0] < 0.01: rootv.add(lp.vertex_index)
    else:
        rootv = {i for i, c in enumerate(col.data) if c.color[0] < 0.01}
    hg = hair.vertex_groups.get('Head') or hair.vertex_groups.new(name='Head')
    idx = list(rootv)
    for vg in hair.vertex_groups:
        if vg.name != 'Head': vg.remove(idx)
    hg.add(idx, 1.0, 'REPLACE')
    print('ROOTFIX verts', len(idx))

# ---- 4. scalp shell, weighted to Head on the new rig ----
mw = shell.matrix_world.copy(); shell.parent = None; shell.matrix_world = mw
for m in list(shell.modifiers): shell.modifiers.remove(m)
shell.vertex_groups.clear(); g = shell.vertex_groups.new(name='Head'); g.add(list(range(len(shell.data.vertices))), 1.0, 'REPLACE')
md = shell.modifiers.new('Armature', 'ARMATURE'); md.object = arm
shell.parent = arm; shell.matrix_parent_inverse = arm.matrix_world.inverted()

# ---- 5. previews: cards (A) vs locks (B), Workbench silhouettes ----
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = sc.render.resolution_y = 640
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'SINGLE'; sc.display.shading.single_color = (0.36, 0.24, 0.16)
sc.render.film_transparent = False
cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.34
centre = HEAD + Vector((0, 0, 0.09))
def shoot(tag, show):
    for o in (cards_obj, hair, shell):
        o.hide_render = o not in show
    for v, d in (('front', Vector((0, FRONT, 0))), ('side', Vector((1, 0, 0))), ('back', Vector((0, -FRONT, 0)))):
        cam.location = centre + d * 1.0
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f'preview_{tag}_{v}.png'); bpy.ops.render.render(write_still=True)
shoot('A_cards', {cards_obj, shell}); shoot('B_locks', {hair, shell})

# ---- 6. export ----
bpy.data.objects.remove(cards_obj); bpy.data.objects.remove(src_arm)
for o in bpy.context.selected_objects: o.select_set(False)
for o in (arm, hair, shell): o.select_set(True)
bpy.context.view_layer.objects.active = arm
os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=FBX_OUT, use_selection=True, object_types={'ARMATURE', 'MESH'}, path_mode='STRIP', embed_textures=False,
                         apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, mesh_smooth_type='OFF', axis_forward='-Z', axis_up='Y',
                         bake_anim=False, use_armature_deform_only=False, colors_type='LINEAR')
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'tobin_aetherstrand.blend'))
tris = sum(len(p.vertices) - 2 for p in hair.data.polygons)
roots = sorted({b.name.rsplit('_', 1)[0] for b in arm.data.bones if b.name.startswith('Hair_')})
json.dump(dict(locks=len(locks), zones=zc, chains=len(roots), bones=len(arm.data.bones), hair_tris=tris, root_fix=ROOT_FIX, head=list(HEAD), front=FRONT),
          open(os.path.join(OUT, 'summary.json'), 'w'), indent=1)
print('EXPORT_DONE', FBX_OUT, 'chains', len(roots), 'bones', len(arm.data.bones), 'hair tris', tris)
