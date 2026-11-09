# Blender bg: import target.glb (importer converts Y-up/+Z face -> Z-up/-Y face), dump world verts/faces.
import bpy, numpy as np, sys
OUT = sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='C:/AI/test_out/heroes/face_spike/target/target.glb')
objs=[o for o in bpy.data.objects if o.type=='MESH']
V=[];F=[];off=0
for o in objs:
    m=np.array(o.matrix_world)
    v=np.array([tuple(x.co) for x in o.data.vertices]); v=v@m[:3,:3].T+m[:3,3]
    o.data.calc_loop_triangles()
    f=np.array([t.vertices[:] for t in o.data.loop_triangles])+off
    V.append(v);F.append(f);off+=len(v)
V=np.concatenate(V);F=np.concatenate(F)
np.savez(OUT,v=V,f=F)
print('OBJS',len(objs),V.shape,F.shape,V.min(0),V.max(0))
