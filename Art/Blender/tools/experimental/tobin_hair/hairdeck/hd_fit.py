# HairDeck hair cards -> TobinBase head (Blender 5.x headless).
#   blender -b --factory-startup -P hd_fit.py -- <hair_seedN_nohead.glb> <out_dir> [clear=0.002] [sx=1] [sy=1] [sz=1] [dz=0] [dy=0] [res=700]
# HairDeck writes hair in its canonical head frame (hairdeck/data/head_frame.py): unit = face-mesh width W, origin O = face bbox centre
# in x / depth and face bbox top in height (W, O from workspace/data/head_frame_train.npz: W 0.1276, O (0, 0.4791, 0.0258), glTF y up,
# face +z). The glTF importer turns that into Blender z up, face -y: O_B = (0, -0.0258, 0.4791). TobinBase's matching box is AvatarHead
# above the chin (ears included, as in a VRoid face mesh): p' = A + (p - O_B) * s, s = width / W, optional per-axis tweaks sx/sy/sz.
# Default fit=ell instead maps HairDeck's head-proxy ellipsoid onto a least-squares ellipsoid of the TobinBase cranium (per-axis scale).
# Then every card vertex is pushed to >= clear above the AvatarHead skin (no clipping). Saves hd_fit.blend, prev_*.png (sheet.sh layout).
import bpy, bmesh, sys, os, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
argv = sys.argv[sys.argv.index('--') + 1:]; GLB, OUT = argv[0], argv[1]; kw = dict(a.split('=', 1) for a in argv[2:] if '=' in a)
F = lambda k, d: float(kw.get(k, d)); os.makedirs(OUT, exist_ok=True)
W_C, O_B = 0.12759824, Vector((0.0, -0.0258014528, 0.479054948))
bpy.ops.wm.read_factory_settings(use_empty=True)
HERE = os.path.dirname(os.path.abspath(__file__))
bpy.ops.import_scene.fbx(filepath=os.path.normpath(os.path.join(HERE, '../../../../../../Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx')))
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Avatar'):
        for m in o.data.materials:
            if m and m.use_nodes:
                for n in [n for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and not (n.image and n.image.has_data)]: m.node_tree.nodes.remove(n)
                b = m.node_tree.nodes.get('Principled BSDF')
                if b: b.inputs['Base Color'].default_value = (0.80, 0.56, 0.44, 1) if 'Eye' not in o.name else (0.9, 0.9, 0.9, 1)
avatar = [o for o in bpy.data.objects if o.type == 'MESH']
dg = bpy.context.evaluated_depsgraph_get(); head = bpy.data.objects['AvatarHead']
bmh = bmesh.new(); bmh.from_object(head, dg); bmh.transform(head.matrix_world); bmh.normal_update(); TREE = BVHTree.FromBMesh(bmh)
HV = np.array([v.co[:] for v in bmh.verts]); HV = HV[HV[:, 2] > 1.60]
A = Vector((0.5 * (HV[:, 0].min() + HV[:, 0].max()), 0.5 * (HV[:, 1].min() + HV[:, 1].max()), HV[:, 2].max()))
s = np.ptp(HV[:, 0]) / W_C
print('TOBIN head width %.4f depth %.4f top %.4f -> scale %.3f' % (np.ptp(HV[:, 0]), np.ptp(HV[:, 1]), HV[:, 2].max(), s))
before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=GLB)
new = [o for o in bpy.data.objects if o not in before]; hair = [o for o in new if o.type == 'MESH'][0]
bpy.context.view_layer.update(); hair.data.transform(hair.matrix_world); hair.parent = None; hair.matrix_world = Matrix.Identity(4)
for o in new:
    if o != hair: bpy.data.objects.remove(o)
hair.name = 'Tobin_Hair'
if kw.get('fit', 'ell') == 'ell':
    # HairDeck's head proxy is a fixed ellipsoid (glTF centre (0, 0.422, 0.0003), radii x 0.0517, y 0.0638, z 0.0638 = Blender centre
    # (0, -0.0003, 0.422), radii (0.0517, 0.0638, 0.0638)); map it onto a least-squares axis-aligned ellipsoid of TobinBase's cranium
    # (AvatarHead above z cr_z, ears and face below the brow left out).
    PC, PR = Vector((0.0, -0.0003, 0.422)), Vector((0.0517, 0.0638, 0.0638))
    allv = np.array([v.co[:] for v in bmh.verts]); x, y, z = allv.T
    m = (z > F('cr_z', 1.70)) & ~((np.abs(x) > 0.068) & (z < 1.765)) & ~((y < -0.075) & (z < 1.785))
    X = allv[m]; M = np.c_[X ** 2, X]; coef = np.linalg.lstsq(M, np.ones(len(X)), rcond=None)[0]
    a2, d = coef[:3], coef[3:]; cen = -d / (2 * a2); k = 1 + (d ** 2 / (4 * a2)).sum(); rad = np.sqrt(k / a2)
    print('CRANIUM ellipsoid centre', cen.round(4), 'radii', rad.round(4), 'pts', int(m.sum()))
    S = Vector(rad / np.array(PR)) * F('k', 1.0); S = Vector((S.x * F('sx', 1), S.y * F('sy', 1), S.z * F('sz', 1)))
    D = Vector(cen) + Vector((0, F('dy', 0), F('dz', 0)))
    for v in hair.data.vertices:
        p = v.co - PC; v.co = D + Vector((p.x * S.x, p.y * S.y, p.z * S.z))
    print('SCALE per axis', [round(c, 3) for c in S])
else:
    S = Vector((s * F('sx', 1), s * F('sy', 1), s * F('sz', 1))); D = Vector((0, F('dy', 0), F('dz', 0)))
    for v in hair.data.vertices:
        p = v.co - O_B; v.co = A + D + Vector((p.x * S.x, p.y * S.y, p.z * S.z))
CL = F('clear', 0.002); pushed = 0; worst = 1.0
bm = bmesh.new(); bm.from_mesh(hair.data)
for v in bm.verts:
    loc, n, _, _ = TREE.find_nearest(v.co); sd = (v.co - loc).dot(n); worst = min(worst, sd)
    if sd < CL: v.co = loc + n * CL; pushed += 1
bm.to_mesh(hair.data); hair.data.update()
print('CLEAR pushed %d of %d verts, worst before %.4f m' % (pushed, len(bm.verts), worst))
# coverage of the upper head (rays from the skull centre, face cone skipped)
th = BVHTree.FromBMesh(bm); C = Vector((0, -0.045, 1.735)); hit = tot = 0
for el in np.linspace(5, 85, 9):
    for az in np.linspace(-180, 180, 36, endpoint=False):
        if -50 < az < 50 and el < 30: continue
        d = Vector((math.cos(math.radians(el)) * math.sin(math.radians(az)), -math.cos(math.radians(el)) * math.cos(math.radians(az)), math.sin(math.radians(el))))
        tot += 1; hit += th.ray_cast(C + d * 0.4, -d, 0.4)[0] is not None
print('COVER %d/%d  TRIS %d' % (hit, tot, sum(len(p.vertices) - 2 for p in hair.data.polygons)))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'hd_fit.blend'))
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = int(kw.get('res', 700))
sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.37, 1)
for nm, rot, en in (('key', (50, 0, -35), 3.0), ('fill', (60, 0, 140), 1.2)):
    l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.rotation_euler = [math.radians(a) for a in rot]; sc.collection.objects.link(l)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.42
for v, az in dict(front=0, q34=-40, side=90, back=180).items():
    a = math.radians(az); tgt = Vector((0, -0.03, 1.735)); d = Vector((math.sin(a), -math.cos(a), 0.05)).normalized()
    cam.location = tgt + d * 1.0; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, f'prev_{v}.png'); bpy.ops.render.render(write_still=True)
print('FIT_DONE', OUT)
