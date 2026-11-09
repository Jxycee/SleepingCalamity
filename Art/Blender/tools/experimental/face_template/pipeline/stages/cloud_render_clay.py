# blender -b -P cloud_render_clay.py -- in.glb out_prefix [clay|tex] [views e.g. front,right,left,back] [facing: glTF axis +Z|-Z|+X|-X, default +Z]
# Ortho renders. View names are relative to the model's facing axis (glTF coords, Y-up). 'right' = the subject's right side?? -> we define 'side' = camera on model +X side for +Z facing.
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import bpy, sys, math
from mathutils import Vector
a=sys.argv[sys.argv.index('--')+1:]
glb,pref=a[0],a[1]; mode=a[2] if len(a)>2 else 'clay'; views=(a[3] if len(a)>3 else 'front,side').split(','); facing=a[4] if len(a)>4 else '+Z'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']; sc=bpy.context.scene
tris=0
for o in objs:
    o.data.calc_loop_triangles(); tris+=len(o.data.loop_triangles)
    if mode=='clay':
        m=bpy.data.materials.new('c'); m.use_nodes=True; b=m.node_tree.nodes['Principled BSDF']
        b.inputs['Base Color'].default_value=(0.75,0.72,0.68,1); b.inputs['Roughness'].default_value=0.55
        o.data.materials.clear(); o.data.materials.append(m)
bpy.context.view_layer.update()
pts=[o.matrix_world@v.co for o in objs for v in o.data.vertices]
mn=Vector((min(p[i] for p in pts) for i in range(3))); mx=Vector((max(p[i] for p in pts) for i in range(3))); ctr=(mn+mx)/2
print('BBOX_BLENDER_ZUP',list(mn),list(mx),'TRIS',tris)
# glTF +Z-facing == Blender -Y facing after import. Map facing to Blender dir vector of the face normal
F={'+Z':Vector((0,-1,0)),'-Z':Vector((0,1,0)),'+X':Vector((1,0,0)),'-X':Vector((-1,0,0))}[facing]
Rr=Vector((F.y*-1*-1,0,0)) if False else None
up=Vector((0,0,1)); side=F.cross(up)  # subject's left? -> camera on 'side' side
w=bpy.data.worlds.new('w'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.55,0.55,0.57,1); sc.world=w
cam=bpy.data.objects.new('cam',bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; size=max(mx-mn)*1.05; cam.data.ortho_scale=size
sc.render.resolution_x=sc.render.resolution_y=1024
sc.render.engine='BLENDER_EEVEE'; sc.view_settings.view_transform='Standard'
lt=bpy.data.objects.new('l',bpy.data.lights.new('l','SUN')); lt.data.energy=3.0; sc.collection.objects.link(lt)
for v in views:
    d={'front':F,'side':side,'side2':-side,'back':-F,'top':up}[v]
    cam.location=ctr+d*10; cam.rotation_euler=(ctr-cam.location).to_track_quat('-Z','Y' if v!='top' else 'Y').to_euler()
    ld=(d*0.6+Vector((0,0,0.5))+side*0.3).normalized() if mode=='clay' else (d*0.8+Vector((0,0,0.4))).normalized()
    lt.rotation_euler=(-ld).to_track_quat('-Z','Y').to_euler()
    if mode=='tex': lt.data.energy=1.5
    sc.render.filepath=f'{pref}_{v}.png'; bpy.ops.render.render(write_still=True)
