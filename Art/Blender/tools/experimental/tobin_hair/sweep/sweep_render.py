# Preview renders for the hair sweep (Blender 5.x headless): hd_fit.blend -> tinted hair with the head, plus a hair-only RGBA pass for colour scoring.
#   blender -b <variant>/hd_fit.blend -P sweep_render.py -- <out_dir> [root=#rrggbb] [tip=#rrggbb] [res=700]
# Writes prev_front/q34/side/back.png (head + tinted hair, same camera as hd_fit.py, Standard view transform so colours compare with the
# reference photos) and hair_front/side/back.png (RGBA: the head is a holdout, so alpha = visible hair; used by sweep_score.py).
# root/tip: sRGB hex; the hair colour runs root -> tip along the card (uv v). Without them the flat hd_fit preview colour is used.
import bpy, sys, os, math
from mathutils import Vector
a = sys.argv[sys.argv.index('--') + 1:]; OUT = a[0]; kw = dict(x.split('=', 1) for x in a[1:] if '=' in x); os.makedirs(OUT, exist_ok=True)
def lin(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1]
hair = bpy.data.objects['Tobin_Hair']; mat = hair.data.materials[0]; nt = mat.node_tree; bsdf = nt.nodes['Principled BSDF']
if 'root' in kw or 'tip' in kw:
    uv = nt.nodes.new('ShaderNodeUVMap'); uv.uv_map = 'UVMap'; sep = nt.nodes.new('ShaderNodeSeparateXYZ'); mix = nt.nodes.new('ShaderNodeMix')
    mix.data_type = 'RGBA'; mix.inputs['A'].default_value = lin(kw.get('root', kw.get('tip'))); mix.inputs['B'].default_value = lin(kw.get('tip', kw.get('root')))
    nt.links.new(uv.outputs['UV'], sep.inputs[0]); nt.links.new(sep.outputs['Y'], mix.inputs['Factor']); nt.links.new(mix.outputs['Result'], bsdf.inputs['Base Color'])
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = int(kw.get('res', 700))
sc.view_settings.view_transform = 'Standard'
sc.world = bpy.data.worlds.new('w'); sc.world.use_nodes = True; sc.world.node_tree.nodes['Background'].inputs[0].default_value = (0.35, 0.35, 0.37, 1)
for nm, rot, en in (('key', (50, 0, -35), 3.0), ('fill', (60, 0, 140), 1.2)):
    l = bpy.data.objects.new(nm, bpy.data.lights.new(nm, 'SUN')); l.data.energy = en; l.rotation_euler = [math.radians(x) for x in rot]; sc.collection.objects.link(l)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 0.42
def shoot(v, az, path):
    a_ = math.radians(az); tgt = Vector((0, -0.03, 1.735)); d = Vector((math.sin(a_), -math.cos(a_), 0.05)).normalized()
    cam.location = tgt + d * 1.0; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
VIEWS = dict(front=0, q34=-40, side=90, back=180)
sc.render.image_settings.file_format = 'PNG'; sc.render.film_transparent = False
for v, az in VIEWS.items(): shoot(v, az, os.path.join(OUT, f'prev_{v}.png'))
sc.render.film_transparent = True; sc.render.image_settings.color_mode = 'RGBA'
for o in bpy.data.objects:
    if o.type == 'MESH' and o is not hair: o.is_holdout = True
for v in ('front', 'side', 'back'): shoot(v, VIEWS[v], os.path.join(OUT, f'hair_{v}.png'))
print('SWEEP_RENDER_DONE', OUT)
