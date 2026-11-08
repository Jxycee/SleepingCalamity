"""Plain format conversion: Tobin_Base_v2_sliders.glb -> TobinBase.fbx for a Unity Humanoid import.

No mesh or rig edits. Armature + meshes, shape keys kept, no leaf bones, rest pose only (no animation baked).
Run: blender -b -P sc_tobin_glb_to_fbx.py -- <in.glb> <out.fbx>
Also prints a per-mesh report (tris, shape keys) re-read from the written FBX.
"""
import sys
import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

# Drop the glTF animation (Male_Pose_A): the FBX carries the rest pose only.
for act in list(bpy.data.actions):
    bpy.data.actions.remove(act)
for o in bpy.data.objects:
    if o.animation_data:
        o.animation_data_clear()
for o in bpy.data.objects:
    if o.type == 'ARMATURE':
        o.data.pose_position = 'REST'

bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.type in {'ARMATURE', 'MESH'}:
        o.select_set(True)

bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=True,
    object_types={'ARMATURE', 'MESH'},
    add_leaf_bones=False,
    path_mode='STRIP',
    global_scale=1.0,
    apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL',
    bake_space_transform=False,
    axis_forward='-Z',
    axis_up='Y',
    use_armature_deform_only=False,
    bake_anim=False,
    use_mesh_modifiers=True,
    mesh_smooth_type='FACE',
    use_tspace=False,
    embed_textures=False,
)

# Re-read the written FBX and report.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=dst)
for o in sorted(bpy.data.objects, key=lambda x: x.name):
    if o.type == 'MESH':
        me = o.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        keys = len(me.shape_keys.key_blocks) - 1 if me.shape_keys else 0
        print("REPORT mesh", o.name, "tris", tris, "shapekeys", keys)
    elif o.type == 'ARMATURE':
        print("REPORT armature", o.name, "bones", len(o.data.bones), "scale", tuple(o.scale))
