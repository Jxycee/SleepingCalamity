import bpy, sys, importlib.util
from mathutils import Vector
ADDON="/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/aetherstrand_plugin/AetherStrand_Blender/aetherstrand_hair_cards.py"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath="/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/tobin/Tobin_Base_v2_sliders.glb")
head=bpy.data.objects["AvatarHead"]
def bb(o):
    ws=[o.matrix_world @ v.co for v in o.data.vertices]
    mn=Vector((min(p[i] for p in ws) for i in range(3))) if False else Vector(tuple(min(p[i] for p in ws) for i in range(3)))
    mx=Vector(tuple(max(p[i] for p in ws) for i in range(3)))
    return mn,mx
mn,mx=bb(head); print("HEADBB min",[round(x,3) for x in mn],"max",[round(x,3) for x in mx])
spec=importlib.util.spec_from_file_location("aetherstrand_hair_cards", ADDON)
m=importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
m.register()
before=set(bpy.data.objects.keys())
r=bpy.ops.aetherstrand.generate_sculpted_hair()
print("OPRESULT", r)
new=[o for o in bpy.data.objects if o.name not in before]
meshes=[o for o in new if o.type=='MESH']
print("NEWMESHES", len(meshes), "TOTALVERTS", sum(len(o.data.vertices) for o in meshes))
ws=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
if ws:
    hmn=[min(p[i] for p in ws) for i in range(3)]; hmx=[max(p[i] for p in ws) for i in range(3)]
    print("HAIRBB min",[round(x,3) for x in hmn],"max",[round(x,3) for x in hmx])
DZ=mx[2]-hmx[2]
for o in new:
    if o.parent is None: o.location.z += DZ
print("APPLIED_DZ", round(DZ,3))
print("ARMATURES", [o.name for o in new if o.type=='ARMATURE'])
import math
from mathutils import Vector as V
# hide body parts, keep head + hair only
for o in bpy.data.objects:
    if o.type=='MESH' and o.name!='AvatarHead' and o not in meshes: o.hide_render=True
for o in bpy.data.objects:
    if o.type in ('ARMATURE',) : o.hide_render=True
for o in bpy.data.objects:
    if o.type in ('MESH','CURVE') and o.name in ('AvatarBody','AvatarEyelashes','AvatarLeftEyeball','AvatarRightEyeball','AvatarTeethLower','AvatarTeethUpper','BaseUnderlay_Legs','BaseUnderlay_Torso','Icosphere'): o.hide_render=True
# move everything to origin at head base for a clean frame: centre of head bbox
c=V(((mn[0]+mx[0])/2,(mn[1]+mx[1])/2,(mn[2]+mx[2])/2))
print("CENTRE", [round(x,3) for x in c])
sc=bpy.context.scene
sc.render.engine='BLENDER_WORKBENCH'
sc.render.resolution_x=960; sc.render.resolution_y=960
sc.display.shading.light='STUDIO'; sc.display.shading.color_type='MATERIAL'
hm=bpy.data.materials.new('H'); hm.diffuse_color=(0.9,0.75,0.65,1); head.data.materials.clear(); head.data.materials.append(hm)
for o in meshes:
    o.data.materials.clear(); hh=bpy.data.materials.new('Hair'); hh.diffuse_color=(0.9,0.2,0.2,1); o.data.materials.append(hh)
cam=bpy.data.cameras.new('C'); camo=bpy.data.objects.new('C',cam); sc.collection.objects.link(camo); sc.camera=camo
cam.lens=85
for name,loc,rot in [('front',(c.x,c.y-1.0,c.z),(math.radians(90),0,0)),('side',(c.x+1.0,c.y,c.z),(math.radians(90),0,math.radians(90)))]:
    camo.location=loc; camo.rotation_euler=rot
    sc.render.filepath=r"/tmp/claude-0/-home-user-SleepingCalamity/5bd8c6bb-33e1-5bbf-b3d4-29f2ef526d91/scratchpad/tobin/run/"+"fit_"+name+".png"
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)
