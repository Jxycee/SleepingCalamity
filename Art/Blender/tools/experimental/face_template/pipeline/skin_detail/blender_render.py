"""Blender script (blender -b <run blend> --python blender_render.py -- <job.json>). Never saves the blend.
job.json: {out_dir, mode: before|after, albedo, normal, rough (paths or null), shots: [{name, target[3], dir[3], dist, lens, res, key}], samples}
Shows only the *_tpl_full head variant (+eyes/teeth/lashes/body), swaps the Head_cloud textures, Cycles OPTIX, fixed seed -> before/after are pixel-comparable."""
import bpy, sys, json, math, mathutils

job = json.load(open(sys.argv[sys.argv.index('--') + 1]))
KEEP = {'AvatarHead_tpl_full', 'AvatarLeftEyeball_tpl_full', 'AvatarRightEyeball_tpl_full', 'AvatarEyelashes_tpl_full', 'AvatarTeethLower_tpl_full', 'AvatarTeethUpper_tpl_full', 'AvatarBody'}
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_render = o.name not in KEEP; o.hide_viewport = o.hide_render
# --- material swap
m = bpy.data.materials['Head_cloud']; nt = m.node_tree; b = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'][0]
imgs = [n for n in nt.nodes if n.type == 'TEX_IMAGE']
alb = [n for n in imgs if any(l.to_socket == b.inputs['Base Color'] for l in n.outputs['Color'].links)][0]
nrm = [n for n in imgs if n is not alb][0]
if job.get('albedo'): alb.image = bpy.data.images.load(job['albedo'])
if job.get('normal'): nrm.image = bpy.data.images.load(job['normal']); nrm.image.colorspace_settings.name = 'Non-Color'
if job.get('rough'):
    r = nt.nodes.new('ShaderNodeTexImage'); r.image = bpy.data.images.load(job['rough']); r.image.colorspace_settings.name = 'Non-Color'
    for l in list(nrm.inputs['Vector'].links): nt.links.new(l.from_socket, r.inputs['Vector'])
    nt.links.new(r.outputs['Color'], b.inputs['Roughness'])
# --- scene
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; cy = sc.cycles; cy.samples = job.get('samples', 96); cy.use_denoising = True; cy.denoiser = 'OPENIMAGEDENOISE'
cy.seed = 7; cy.use_animated_seed = False
try:
    cy.denoiser = 'OPTIX'
except Exception: pass
prefs = bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
    for d in prefs.devices: d.use = d.type == 'OPTIX'
    cy.device = 'GPU'
except Exception as e: print('GPU setup', e)
sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs['Color'].default_value = (0.18, 0.18, 0.2, 1); w.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.5
for o in list(bpy.data.objects):
    if o.type == 'LIGHT' or o.type == 'CAMERA': bpy.data.objects.remove(o)
def aim(obj, tgt):
    d = mathutils.Vector(tgt) - obj.location; obj.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
def light(name, loc, tgt, energy, size):
    L = bpy.data.lights.new(name, 'AREA'); L.energy = energy; L.size = size; o = bpy.data.objects.new(name, L); sc.collection.objects.link(o); o.location = loc; aim(o, tgt); return o
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
for s in job['shots']:
    T = mathutils.Vector(s['target']); D = mathutils.Vector(s['dir']).normalized(); dist = s['dist']
    cam.data.lens = s['lens']; cam.location = T + D * dist; aim(cam, T)
    # raking key (~70 deg from the view axis, a little above) shows the relief; weak frontal fill
    up = mathutils.Vector((0, 0, 1)); side = D.cross(up).normalized()
    for o in [o for o in bpy.data.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o)
    kd = (D * math.cos(math.radians(s.get('key', 62))) + side * math.sin(math.radians(s.get('key', 62))) + up * 0.35).normalized()
    light('key', T + kd * 0.9, T, 60 * s.get('kscale', 1), 0.35)
    light('fill', T + (D - side * 0.5 + up * 0.2).normalized() * 1.2, T, 6, 0.8)
    sc.render.resolution_x = sc.render.resolution_y = s['res']; sc.render.filepath = '%s/%s_%s.png' % (job['out_dir'], job['mode'], s['name']); sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True); print('RENDERED', sc.render.filepath)
