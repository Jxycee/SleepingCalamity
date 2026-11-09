"""Fixed orthographic workbench renders of the head: front + right profile (+ 3/4 not needed).
blender -b --factory-startup --python render_ortho.py -- <glb> <outdir> [lighting FLAT|STUDIO]
Camera: center (0,-0.02,1.66), ortho_scale 0.4, 1600x1600. front cam at -Y looking +Y; right profile cam at -X looking +X.
"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, math, bpy
from mathutils import Vector
glb, outdir = sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
# the GLB carries a non-rest default pose (head shifted ~2.4 cm in -Y at frame 0): force REST so renders match the raw basis coords
for o in bpy.data.objects:
    if o.type == 'ARMATURE':
        o.data.pose_position = 'REST'
for o in list(bpy.data.objects):
    if o.name != "AvatarHead" and o.type == 'MESH':
        o.hide_render = True
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.render.resolution_x = sc.render.resolution_y = 1600
sc.render.film_transparent = False
sh = sc.display.shading
sh.light = 'STUDIO'; sh.color_type = 'SINGLE'; sh.single_color = (0.8, 0.7, 0.62)
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.12, 0.12, 0.14)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.4
c = Vector((0, -0.02, 1.66))
for name, off, rot in (("front", (0, -1, 0), (math.radians(90), 0, 0)),
                       ("right", (-1, 0, 0), (math.radians(90), 0, math.radians(-90)))):
    cam.location = c + Vector(off); cam.rotation_euler = rot
    sc.render.filepath = f"{outdir}/tpl_{name}.png"
    bpy.ops.render.render(write_still=True)
print("DONE")
