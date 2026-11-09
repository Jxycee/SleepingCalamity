# Blender bg: ortho front render of textured target (for mediapipe) + raycast of landmark pixels.
# usage: -- out_png [pix_json_in] [hits_json_out]
import bpy, sys, json, numpy as np
from mathutils import Vector
a=sys.argv[sys.argv.index('--')+1:]
OUT=a[0]
S=1.2; R=1024; CZ=0.12  # ortho scale (m units of target), res, camera centre z
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath='C:/AI/test_out/heroes/face_spike/target/target.glb')
ob=[o for o in bpy.data.objects if o.type=='MESH'][0]
sc=bpy.context.scene
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=S
cam.location=(0,-5,CZ); cam.rotation_euler=(np.pi/2,0,0)
sc.render.resolution_x=sc.render.resolution_y=R
sc.render.engine='BLENDER_WORKBENCH'
sh=sc.display.shading; sh.light='FLAT'; sh.color_type='TEXTURE'
sc.view_settings.view_transform='Standard'
sc.render.filepath=OUT; bpy.ops.render.render(write_still=True)
if len(a)>=3:
    pts=json.load(open(a[1])); dg=bpy.context.evaluated_depsgraph_get(); res={}
    for k,(px,py) in pts.items():
        x=(px/R-0.5)*S; z=CZ+(0.5-py/R)*S
        ok,loc,n,i,o,m=sc.ray_cast(dg,Vector((x,-5,z)),Vector((0,1,0)))
        res[k]=list(loc) if ok else None
    json.dump(res,open(a[2],'w'))
