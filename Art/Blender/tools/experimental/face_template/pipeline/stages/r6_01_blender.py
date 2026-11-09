# Blender bg, round 2.  usage: -- export out.npz | render src out.png cam.json [tight_z0 tight_z1] | ray src cam.json pix.json hits.json
# src: .glb (imported; Z-up, face -Y) or .npz (v,f in template frame; plus optional extra eyes e.npz keys ev,ef)
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import bpy, sys, json, numpy as np
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; mode = a[0]
GLB = _P_GLB
def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if src.endswith('.glb'):
        bpy.ops.import_scene.gltf(filepath=src)
        ob = [o for o in bpy.data.objects if o.type == 'MESH']
        V = []; F = []; off = 0
        for o in ob:
            m = np.array(o.matrix_world); v = np.array([tuple(x.co) for x in o.data.vertices]); v = v @ m[:3, :3].T + m[:3, 3]
            o.data.calc_loop_triangles(); f = np.array([t.vertices[:] for t in o.data.loop_triangles]) + off
            V.append(v); F.append(f); off += len(v)
        return np.concatenate(V), np.concatenate(F)
    d = np.load(src); return d['v'].astype(np.float64), d['f'].astype(np.int64), (d['ev'], d['ef']) if 'ev' in d.files else None
def build(v, f, extra=None):
    sc = bpy.context.scene
    for k, (vv, ff) in enumerate([(v, f)] + ([extra] if extra else [])):
        me = bpy.data.meshes.new(f'm{k}'); me.from_pydata(vv.astype(np.float32).tolist(), [], ff.astype(np.int32).tolist()); me.update()
        ob = bpy.data.objects.new(f'm{k}', me); sc.collection.objects.link(ob)
        for p in me.polygons: p.use_smooth = True
    return sc
if mode == 'export':
    v, f = load(GLB)[:2]; np.savez(a[1], v=v, f=f); print('EXPORT', v.shape, f.shape, v.min(0), v.max(0)); sys.exit()
src = a[1]; r = load(src); v, f = r[0], r[1]; extra = None
if len(r) > 2 and r[2] is not None: extra = r[2]
sc = build(v, f, extra)
if mode == 'render':
    out, camj = a[2], a[3]
    z0, z1 = (float(a[4]), float(a[5])) if len(a) > 5 else (v[:, 2].min(), v[:, 2].max())
    cx = float((v[:, 0].min() + v[:, 0].max()) / 2); cz = (z0 + z1) / 2; S = (z1 - z0) * 1.04; R = 1400
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = S; cam.location = (cx, -5, cz); cam.rotation_euler = (np.pi / 2, 0, 0)
    sc.render.resolution_x = sc.render.resolution_y = R; sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.75, 0.72, 0.68); sh.show_cavity = False
    sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('w'); w.use_nodes = False; w.color = (0.55, 0.55, 0.57); sc.world = w
    sc.render.filepath = out; bpy.ops.render.render(write_still=True)
    json.dump(dict(cx=cx, cz=cz, S=S, R=R), open(camj, 'w'))
elif mode == 'ray':
    cj = json.load(open(a[2])); pts = json.load(open(a[3])); R, S, cx, cz = cj['R'], cj['S'], cj['cx'], cj['cz']
    dg = bpy.context.evaluated_depsgraph_get(); res = {}
    for k, (px, py) in pts.items():
        x = cx + (px / R - 0.5) * S; z = cz + (0.5 - py / R) * S
        ok, loc, n, i, o, m = sc.ray_cast(dg, Vector((x, -5, z)), Vector((0, 1, 0)))
        res[k] = list(loc) if ok else None
    json.dump(res, open(a[4], 'w'))
