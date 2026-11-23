import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/tobin/Tobin_Base_v2_sliders.glb")
head = bpy.data.objects["AvatarHead"]
bpy.ops.object.select_all(action='DESELECT')
head.select_set(True)
bpy.context.view_layer.objects.active = head
bpy.ops.wm.obj_export(filepath="/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/tobin/Tobin_Head.obj",
    export_selected_objects=True, export_materials=False, export_normals=True, export_uv=True, forward_axis='NEGATIVE_Z', up_axis='Y')
print("EXPORTED", len(head.data.vertices), tuple(head.dimensions))
