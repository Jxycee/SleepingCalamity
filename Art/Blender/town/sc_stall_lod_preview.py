# sc_stall_lod_preview.py: Workbench side-by-side of a stall's LOD0 / LOD1 / LOD2 (Rebuilt and Ruined), one PNG per state.
# Run:  blender -b --factory-startup --python Art/Blender/town/sc_stall_lod_preview.py -- <stall.fbx> <out_prefix> [--px 960]
import bpy, sys, math
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]
src, outp = argv[0], argv[1]
PX = int(argv[argv.index('--px') + 1]) if '--px' in argv else 960
bpy.ops.wm.read_homefile(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading; sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.75, 0.68, 0.55); sh.show_cavity = True
sc.render.resolution_x = PX; sc.render.resolution_y = int(PX * 0.7); sc.render.film_transparent = False
sc.world = bpy.data.worlds.new('w'); sc.world.color = (0.25, 0.27, 0.3)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
meshes = [o for o in sc.objects if o.type == 'MESH']
for state in ('Rebuilt', 'Ruined'):
    for lv in range(3):
        for o in meshes: o.hide_render = not o.name.startswith(f'{state}_LOD{lv}')
        o = next(o for o in meshes if o.name.startswith(f'{state}_LOD{lv}'))
        ctr = sum((o.matrix_world @ Vector(c) for c in o.bound_box), Vector()) / 8
        cam.location = ctr + Vector((5.2, -6.8, 3.2)); d = ctr - cam.location
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f'{outp}_{state}_LOD{lv}.png'
        bpy.ops.render.render(write_still=True)
