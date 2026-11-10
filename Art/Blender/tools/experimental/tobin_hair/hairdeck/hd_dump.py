# Dump world-space meshes for the numpy shape tools (Blender 5.x headless).
#   blender -b <file.blend | --factory-startup> -P hd_dump.py -- <out.npz> [objects=AvatarHead,Tobin_Hair] [tobin=1]
# tobin=1 imports TobinBase.fbx first (factory scene). Writes <name>_v, <name>_f, <name>_n (vertex normals) per object.
import bpy, bmesh, sys, os, numpy as np
a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; kw = dict(x.split('=', 1) for x in a[1:] if '=' in x)
if kw.get('tobin') == '1':
    HERE = os.path.dirname(os.path.abspath(__file__))
    bpy.ops.import_scene.fbx(filepath=os.path.normpath(os.path.join(HERE, '../../../../../../Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx')))
dg = bpy.context.evaluated_depsgraph_get(); d = {}
for name in kw.get('objects', 'AvatarHead').split(','):
    o = bpy.data.objects.get(name)
    if o is None: continue
    bm = bmesh.new(); bm.from_object(o, dg); bm.transform(o.matrix_world); bmesh.ops.triangulate(bm, faces=bm.faces); bm.normal_update()
    d[name + '_v'] = np.array([v.co[:] for v in bm.verts], np.float32); d[name + '_n'] = np.array([v.normal[:] for v in bm.verts], np.float32)
    d[name + '_f'] = np.array([[v.index for v in f.verts] for f in bm.faces], np.int32)
    print('DUMP', name, len(bm.verts), len(bm.faces))
np.savez(OUT, **d)
