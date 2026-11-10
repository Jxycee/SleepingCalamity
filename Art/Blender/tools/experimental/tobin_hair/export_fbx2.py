# blender -b mp/Tobin_mp_hair.blend -P export_fbx2.py -- <out.fbx>  : HairArmature (Head + chains) + Tobin_Hair only, rest pose, world frame of Tobin_Base_v2_sliders.glb
import bpy, sys
out=sys.argv[sys.argv.index('--')+1]
arm=bpy.data.objects['HairArmature']; arm.data.pose_position='REST'
for pb in arm.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.rotation_euler=(0,0,0)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.data.objects['Tobin_Hair'].select_set(True); bpy.data.objects['Tobin_HairCap'].select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.export_scene.fbx(filepath=out,use_selection=True,path_mode='STRIP',embed_textures=False,apply_scale_options='FBX_SCALE_ALL',add_leaf_bones=False,mesh_smooth_type='OFF',axis_forward='-Z',axis_up='Y',bake_anim=False,use_armature_deform_only=False)
print('EXPORT_DONE',len(arm.data.bones))
