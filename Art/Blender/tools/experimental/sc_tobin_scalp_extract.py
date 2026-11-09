"""Extract a scalp patch (crown down to the hairline, above the ears) from AvatarHead of Tobin_Base_v2_sliders.glb.

Extraction only: faces are copied from AvatarHead (no sculpt, no conform); the armature and skin weights stay, so the FBX
has the same rig and axes as TobinBase.fbx (see sc_tobin_glb_to_fbx.py). Rest pose.
Run: blender -b --factory-startup -P sc_tobin_scalp_extract.py -- <in.glb> <out.fbx>
"""
import sys
import bpy
import bmesh

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)
for o in bpy.data.objects:
    if o.animation_data:
        o.animation_data_clear()
    if o.type == 'ARMATURE':
        o.data.pose_position = 'REST'

head = bpy.data.objects["AvatarHead"]
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o is not head:
        bpy.data.objects.remove(o, do_unlink=True)
head.name = "TobinScalp"
head.data.shape_keys and head.shape_key_clear()

# Hairline plane in world space (Blender Z up, face looks toward -Y): front hairline high, sides above ears, back lower.
Z_FRONT, Z_SIDE, Z_BACK = 1.775, 1.755, 1.70
bm = bmesh.new()
bm.from_mesh(head.data)
mw = head.matrix_world
drop = []
for f in bm.faces:
    c = mw @ f.calc_center_median()
    if c.y < -0.03:
        cut = Z_FRONT
    elif c.y < 0.02:
        cut = Z_SIDE
    else:
        cut = Z_BACK
    if c.z < cut:
        drop.append(f)
bmesh.ops.delete(bm, geom=drop, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(head.data)
bm.free()
print("REPORT scalp faces", len(head.data.polygons), "verts", len(head.data.vertices))

bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in {'ARMATURE', 'MESH'}:
        o.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, path_mode='STRIP',
    global_scale=1.0, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', bake_space_transform=False,
    axis_forward='-Z', axis_up='Y', use_armature_deform_only=False, bake_anim=False, use_mesh_modifiers=True,
    mesh_smooth_type='FACE', use_tspace=False, embed_textures=False)
