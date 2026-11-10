# blender -b Tobin_rigged_hair.blend -P export_fbx.py -- <out.fbx>   (rest pose, all bones incl. hair chains, meshes + armature, textures not embedded)
import bpy, sys
out=sys.argv[sys.argv.index('--')+1]
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; arm.data.pose_position='REST'
for pb in arm.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.rotation_euler=(0,0,0)
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in ('ARMATURE','MESH'): o.select_set(True)
bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,path_mode='STRIP',embed_textures=False,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,mesh_smooth_type='OFF',axis_forward='-Z',axis_up='Y',bake_anim=False,use_armature_deform_only=False)
print('EXPORT_DONE',len(arm.data.bones))
