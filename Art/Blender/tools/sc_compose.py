"""[Ported from FishGame fg-aaa-prop, M8. Review rig (hearth + studio) built around the potion-stand example; reuse the lighting/camera code.] Assemble the potion stand from baked lows (+ unbaked glass) and render the review set.
blender -b --factory-startup --python compose.py -- --out <dir> --tag <tag> [--res 1600] [--engine EEVEE|CYCLES] [--views a,b,studio_wide]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); PDIR = os.path.join(OUT, 'props'); TAG = arg('--tag', 'stand')
RES = int(arg('--res', '1600')); ENG = arg('--engine', 'EEVEE')
VIEWS = arg('--views', '').split(',') if arg('--views') else None   # e.g. --views kcd_mortar,mortar
LOOK = arg('--look', 'hearth')   # 'kcd': lever 3 presentation (dark room, hot raking fire key, flame cards, ref framings, no studio)
SS = int(arg('--ss', '1'))       # supersample: render SS x RES, downscale to RES (kills the soft-texel read)
bpy.ops.wm.read_homefile(use_empty=True); sc = bpy.context.scene

def load(path, names=None):
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if names is None or n in names]
    obs = []
    for o in dst.objects:
        if o is None: continue
        sc.collection.objects.link(o); obs.append(o)
    return obs

def place(objs, loc, rz=0.0, rx=0.0, ry=0.0):
    for o in objs:
        o.location = Vector(o.location) + Vector(loc) if False else Vector(loc) + Vector(o.location)
        o.rotation_euler = (math.radians(rx), math.radians(ry), math.radians(rz))
    return objs

TOP = 0.88
load(os.path.join(OUT, 'bench_game.blend'))
P = lambda n: os.path.join(PDIR, n + '.blend')
# right end: still on its slab; receiver flask under the spout
still = load(P('Still')); place(still, (0.46, 0.04, TOP))
sx, sy, sz = (-0.30, -0.12, 0.17)
glass = load(P('Glass'))
by = {o.name: o for o in glass}
def gplace(name, loc, rz=0.0):
    for n in (name, name + '_liq'):
        if n in by:
            o = by[n]; base = Vector(o.location)
            o.location = Vector(loc) + Vector((0, 0, base.z)); o.rotation_euler = (0, 0, math.radians(rz))
gplace('Receiver', (0.46 + sx - 0.0, 0.04 + sy, TOP))
# centre: mortar, flask in its ring, bottle, vial
place(load(P('Mortar')), (-0.02, -0.14, TOP), rz=20)
place(load(P('Ring')), (0.12, 0.17, TOP)); gplace('Flask', (0.12, 0.17, TOP + 0.002))
gplace('Bottle', (-0.02, 0.22, TOP)); gplace('Vial', (0.06, -0.02, TOP))
place(load(P('CorkBottle')), (-0.02, 0.22, TOP + 0.197)); place(load(P('CorkVial')), (0.06, -0.02, TOP + 0.082))
# left: book, candle, jug, jar, herbs
place(load(P('Book')), (-0.44, -0.04, TOP), rz=-8)
place(load(P('Candle')), (-0.66, 0.2, TOP))
place(load(P('Jug')), (-0.22, 0.2, TOP), rz=-30)
place(load(P('Jar')), (-0.40, 0.24, TOP), rz=40)
place(load(P('Herbs')), (-0.18, -0.24, TOP), rz=-12)
# under-shelf: a second jug
j2 = load(P('Jug')); place(j2, (0.3, 0.05, 0.27 + 0.035 + 0.028), rz=70)
if '--glb' in argv:
    # export the placed stand (props + bench, no room/lights) as one glTF binary for a model viewer. The glass and
    # liquid shaders (fake refraction/reflection, emission) are procedural and do not survive glTF, so they are swapped
    # for plain glTF glass (KHR transmission) and tinted alpha liquids; every baked prop exports as rendered.
    LIQ = {'M_PotionGreen': (0.12, 0.42, 0.08), 'M_PotionRed': (0.45, 0.04, 0.03), 'M_PotionAmber': (0.7, 0.36, 0.08)}
    for m in list(bpy.data.materials):
        if m.name.startswith('M_Glass') or m.name in LIQ:
            m.use_nodes = True; nt_ = m.node_tree; nt_.nodes.clear()
            bs_ = nt_.nodes.new('ShaderNodeBsdfPrincipled'); out_ = nt_.nodes.new('ShaderNodeOutputMaterial')
            nt_.links.new(bs_.outputs[0], out_.inputs['Surface'])
            if m.name in LIQ:
                bs_.inputs['Base Color'].default_value = (*LIQ[m.name], 1); bs_.inputs['Alpha'].default_value = 0.8; bs_.inputs['Roughness'].default_value = 0.08
            else:
                bs_.inputs['Base Color'].default_value = (0.82, 0.88, 0.8, 1); bs_.inputs['Transmission Weight'].default_value = 1.0
                bs_.inputs['Roughness'].default_value = 0.06; bs_.inputs['Alpha'].default_value = 0.35
            m.surface_render_method = 'BLENDED'
    for o_ in sc.objects: o_.location.z -= TOP   # stand on the origin
    bpy.ops.export_scene.gltf(filepath=arg('--glb'), export_format='GLB', export_apply=True, export_image_format='JPEG', export_jpeg_quality=90)
    print('GLB', arg('--glb')); sys.exit(0)
# candle flame (game: emissive card / VFX), plus its light
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.006, location=(-0.66 + 0.001, 0.2, TOP + 0.158), segments=16, ring_count=8)
fl = bpy.context.active_object; fl.scale = (0.8, 0.8, 2.4)
fm = bpy.data.materials.new('Flame'); fm.use_nodes = True; nt = fm.node_tree
nt.nodes.clear(); em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Color'].default_value = (1.0, 0.55, 0.18, 1); em.inputs['Strength'].default_value = 60
o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], o.inputs['Surface']); fl.data.materials.append(fm)

# room: floor, back wall, left wall (rough plaster/stone stand-ins)
def plane(name, loc, rot, s, col, rough=0.9):
    bpy.ops.mesh.primitive_plane_add(size=s, location=loc, rotation=rot)
    p = bpy.context.active_object; p.name = name
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']; bs.inputs['Base Color'].default_value = (*col, 1); bs.inputs['Roughness'].default_value = rough
    p.data.materials.append(m)
plane('floor', (0, 0, 0), (0, 0, 0), 10, (0.05, 0.045, 0.04))
plane('wall', (0, 0.6, 2.5), (math.radians(90), 0, 0), 10, (0.09, 0.08, 0.07))

if ENG == 'EEVEE':
    sc.render.engine = 'BLENDER_EEVEE'
    for k, v in (('use_raytracing', True), ('taa_render_samples', 96), ('use_shadows', True)):
        if hasattr(sc.eevee, k): setattr(sc.eevee, k, v)
else:
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 256; sc.cycles.use_denoising = True
    pr = bpy.context.preferences.addons['cycles'].preferences; pr.compute_device_type = 'OPTIX'; pr.get_devices()
    for d in pr.devices: d.use = d.type == 'OPTIX'
    sc.cycles.device = 'GPU'
sc.render.resolution_x = RES * SS; sc.render.resolution_y = int(RES * 0.5625) * SS
sc.view_settings.view_transform = 'AgX'
_looks = [l.identifier for l in bpy.types.ColorManagedViewSettings.bl_rna.properties['look'].enum_items]
for _l in ('AgX - Punchy', 'Punchy', 'AgX - Medium High Contrast'):
    if _l in _looks: sc.view_settings.look = _l; break
sc.render.filter_size = 1.0
sc.render.dither_intensity = 1.0; sc.render.image_settings.color_depth = '16'   # 8-bit AgX output posterised the dark walls into contour bands
w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']

def interior_env(base, window, fire):
    """Procedural room for reflections (stand-in for an engine's reflection probe): dark floor,
    warm plaster walls, a cool window lobe and a warm fire lobe. Metals need this or they render black."""
    nt = w.node_tree
    for n in [n for n in nt.nodes if n.name.startswith('env_')]: nt.nodes.remove(n)
    def N(t, **kw):
        n = nt.nodes.new(t); n.name = 'env_' + t
        for k, v in kw.items(): setattr(n, k, v)
        return n
    tc = N('ShaderNodeTexCoord'); sep = N('ShaderNodeSeparateXYZ'); nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    def lobe(direction, power, col, k):
        d = N('ShaderNodeVectorMath', operation='DOT_PRODUCT'); nt.links.new(tc.outputs['Generated'], d.inputs[0])
        d.inputs[1].default_value = direction
        pw = N('ShaderNodeMath', operation='POWER'); nt.links.new(d.outputs['Value'], pw.inputs[0]); pw.inputs[1].default_value = power
        mx = N('ShaderNodeMath', operation='MAXIMUM'); nt.links.new(pw.outputs[0], mx.inputs[0]); mx.inputs[1].default_value = 0.0
        cm = N('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY'); cm.inputs[0].default_value = 1.0
        cm.inputs[6].default_value = (*col, 1); nt.links.new(mx.outputs[0], cm.inputs[7])
        sc_ = N('ShaderNodeVectorMath', operation='SCALE'); nt.links.new(cm.outputs[2], sc_.inputs[0]); sc_.inputs['Scale'].default_value = k
        return sc_.outputs[0]
    wall = N('ShaderNodeMapRange', clamp=True); nt.links.new(sep.outputs['Z'], wall.inputs['Value'])
    wall.inputs['From Min'].default_value = -0.2; wall.inputs['From Max'].default_value = 0.3
    walls = N('ShaderNodeMix', data_type='RGBA'); nt.links.new(wall.outputs['Result'], walls.inputs[0])
    walls.inputs[6].default_value = (0.012, 0.009, 0.007, 1); walls.inputs[7].default_value = (*base, 1)
    a1 = N('ShaderNodeVectorMath', operation='ADD'); nt.links.new(walls.outputs[2], a1.inputs[0]); nt.links.new(lobe((-0.55, -0.6, 0.58), 24.0, (0.75, 0.85, 1.0), window), a1.inputs[1])
    a2 = N('ShaderNodeVectorMath', operation='ADD'); nt.links.new(a1.outputs[0], a2.inputs[0]); nt.links.new(lobe((0.85, -0.3, 0.1), 10.0, (1.0, 0.45, 0.15), fire), a2.inputs[1])
    nt.links.new(a2.outputs[0], bg.inputs['Color']); bg.inputs['Strength'].default_value = 1.0

def light(name, typ, loc, energy, col, size=0.5, aim=None):
    ld = bpy.data.lights.new(name, typ); ld.energy = 0.0 if name in os.environ.get('AAA_LIGHTOFF', '').split(',') else energy; ld.color = col   # AAA_LIGHTOFF=brazier: diagnostic
    if typ == 'AREA': ld.size = size; ld.shape = 'DISK'
    else: ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld); sc.collection.objects.link(ob); ob.location = loc
    d = Vector(aim or (0, 0, TOP)) - Vector(loc); ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
def clear_lights():
    for o in [o for o in sc.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o)
bpy.ops.object.lightprobe_add(type='SPHERE', location=(0.2, 0.0, TOP + 0.3)); pr_ = bpy.context.active_object
pr_.data.influence_distance = 1.5
cam_d = bpy.data.cameras.new('cam'); cam = bpy.data.objects.new('cam', cam_d); sc.collection.objects.link(cam); sc.camera = cam
def flame_cards(base, w, h, n, tag, ring=0.0):
    """Game-style fire: n crossed emissive cards with a procedural flame mask (tongues that narrow upward,
    noise-broken, white-yellow core to deep orange edge), alpha-blended."""
    m = bpy.data.materials.new('Flame_' + tag); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    if hasattr(m, 'surface_render_method'): m.surface_render_method = 'BLENDED'
    N = lambda t, **k: (lambda nd: ([setattr(nd, a, b) for a, b in k.items()], nd)[1])(nt.nodes.new(t))
    L = nt.links.new
    tc = N('ShaderNodeTexCoord'); sp = N('ShaderNodeSeparateXYZ'); L(tc.outputs['UV'], sp.inputs[0])
    obj = N('ShaderNodeTexCoord'); nz = N('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 2.0; nz.inputs['Distortion'].default_value = 1.5; nz.inputs['Detail'].default_value = 4
    mp = N('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (1.0, 0.35, 1.0); L(tc.outputs['UV'], mp.inputs[0]); L(mp.outputs[0], nz.inputs['Vector'])
    # tongue shape: |u-0.5| < (1 - v)^1.3 * 0.5 * noise
    a = N('ShaderNodeMath', operation='SUBTRACT'); L(sp.outputs['X'], a.inputs[0]); a.inputs[1].default_value = 0.5
    ab = N('ShaderNodeMath', operation='ABSOLUTE'); L(a.outputs[0], ab.inputs[0])
    iv = N('ShaderNodeMath', operation='SUBTRACT'); iv.inputs[0].default_value = 1.0; L(sp.outputs['Y'], iv.inputs[1])
    pw = N('ShaderNodeMath', operation='POWER'); L(iv.outputs[0], pw.inputs[0]); pw.inputs[1].default_value = 1.3
    wn = N('ShaderNodeMath', operation='MULTIPLY'); L(pw.outputs[0], wn.inputs[0]); L(nz.outputs['Fac'], wn.inputs[1])
    wn2 = N('ShaderNodeMath', operation='MULTIPLY'); L(wn.outputs[0], wn2.inputs[0]); wn2.inputs[1].default_value = 0.75; wn = wn2
    d = N('ShaderNodeMath', operation='SUBTRACT'); L(wn.outputs[0], d.inputs[0]); L(ab.outputs[0], d.inputs[1])
    msk = N('ShaderNodeMapRange'); L(d.outputs[0], msk.inputs['Value']); msk.inputs['From Min'].default_value = 0.0; msk.inputs['From Max'].default_value = 0.2
    rp = N('ShaderNodeValToRGB'); L(msk.outputs['Result'], rp.inputs[0])
    rp.color_ramp.elements[0].color = (0.25, 0.025, 0.0, 1); rp.color_ramp.elements[1].color = (1.0, 0.5, 0.12, 1)
    rp.color_ramp.elements.new(0.4).color = (0.9, 0.22, 0.02, 1)
    em = N('ShaderNodeEmission'); L(rp.outputs['Color'], em.inputs['Color']); em.inputs['Strength'].default_value = 2.2
    tr = N('ShaderNodeBsdfTransparent'); mx = N('ShaderNodeMixShader'); L(msk.outputs['Result'], mx.inputs[0])
    L(tr.outputs[0], mx.inputs[1]); L(em.outputs[0], mx.inputs[2])
    out = N('ShaderNodeOutputMaterial'); L(mx.outputs[0], out.inputs['Surface'])
    import random as _r; rr = _r.Random(len(tag))
    for i in range(n):
        # a ring of tongues at radius `ring` round base (flames licking up round a pot), cards tangent to it
        a = i * 2 * math.pi / n + rr.uniform(-0.2, 0.2); hh = h * rr.uniform(0.6, 1.1)
        bpy.ops.mesh.primitive_plane_add(size=1.0, location=(base[0] + math.cos(a) * ring, base[1] + math.sin(a) * ring, base[2] + hh * 0.5))
        c = bpy.context.active_object; c.name = f'flame_{tag}{i}'
        c.scale = (w * rr.uniform(0.6, 1.0), hh, 1.0); c.rotation_euler = (math.radians(90), 0, a + math.pi / 2)
        c.data.materials.append(m)
        c.visible_shadow = False

def shoot(name, pos, look, lens=35, fstop=None):
    cam.location = pos; cam_d.lens = lens
    cam.rotation_euler = (Vector(look) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
    cam_d.dof.use_dof = fstop is not None
    if fstop: cam_d.dof.focus_distance = (Vector(look) - Vector(pos)).length; cam_d.dof.aperture_fstop = fstop
    fp = os.path.join(OUT, 'r', f'{TAG}_{name}.png'); sc.render.filepath = fp; bpy.ops.render.render(write_still=True)
    if SS > 1:
        im = bpy.data.images.load(fp); im.scale(RES, int(RES * 0.5625)); im.save(); bpy.data.images.remove(im)

views = [('wide', (0.9, -1.75, 1.45), (0.0, 0.0, 0.95), 32, None),
         ('fp', (-0.05, -0.75, 1.42), (0.0, 0.05, 0.92), 30, None),             # first-person at the stand (KCD-style)
         ('still', (0.15, -0.45, 1.18), (0.42, 0.05, 1.1), 45, 11.0),
         ('mortar', (0.12, -0.42, 1.08), (-0.02, -0.12, 0.93), 50, 11.0),
         ('left', (-0.95, -0.6, 1.2), (-0.45, 0.05, 0.95), 40, 4.0),
         ('kcd_still', (0.40, -0.30, 1.36), (0.46, 0.04, 1.17), 26, 11.0),          # framed like the KCD2 cauldron shot
         ('kcd_mortar', (0.04, -0.34, 1.16), (-0.02, -0.14, 0.93), 30, 11.0),
         ('glass', (0.02, -0.30, 1.08), (0.06, 0.12, 0.96), 42, 11.0),
         ('jug', (-0.30, -0.22, 1.04), (-0.22, 0.2, 0.99), 45, 11.0)]            # jug + albarello close-up          # flask, bottle, vial close-up       # framed like the KCD2 mortar shot
# hearth: candle + brazier glow + dim cool fill (the KCD look)
if LOOK == 'kcd':
    # KCD contrast: near-black room, the fire is the key. A small hot raking source low on the right, the
    # brazier glow from below, a dim cool bounce so shadows are not pure black, and visible flame cards.
    clear_lights(); interior_env((0.008, 0.006, 0.005), 0.25, 4.0)
    DS = os.environ.get('AAA_LIGHTDESAT') == '1'   # less saturated firelight: under near-monochrome orange light, stone, clay, oak and paper converge in hue (t6: 'monochrome orange-brown, materials don't separate')
    SAT = (lambda c: {(1.0, 0.42, 0.12): (1.0, 0.62, 0.38), (1.0, 0.36, 0.09): (1.0, 0.52, 0.27), (1.0, 0.55, 0.22): (1.0, 0.7, 0.45)}.get(c, c)) if DS else (lambda c: c)
    light('candle', 'POINT', (-0.66, 0.2, TOP + 0.165), 9.0, SAT((1.0, 0.55, 0.22)), 0.008)
    if os.environ.get('AAA_FIRELIGHT', '1') == '1':   # default since round 7 (brazier v5 A/B); AAA_FIRELIGHT=0 = the old in-wall light
        # the old brazier light sat 11 cm off-axis, inside the clay wall: every air hole looked onto clay lit at ~0 distance (a white disc).
        # Fire light now sits on the coal bed at the brazier axis; a weaker spill outside the front stands in for the glow below the pot.
        light('brazier', 'POINT', (0.46, 0.04, TOP + 0.235), 14.0, SAT((1.0, 0.36, 0.09)), 0.04)
        light('spill', 'POINT', (0.46, -0.42, TOP + 0.12), 10.0, SAT((1.0, 0.36, 0.09)), 0.08)
    else:
        light('brazier', 'POINT', (0.46, -0.07, TOP + 0.17), 22.0, SAT((1.0, 0.36, 0.09)), 0.04)
    if '--lift' in argv:
        # lift: the hearth fire as a broad raking key across the worktop (KCD2's worktops read in the firelight),
        # a dim warm lantern overhead, a little more cool bounce; shadows stay deep
        light('fire', 'AREA', (0.95, -0.55, TOP + 0.14), 120, SAT((1.0, 0.44, 0.14)), 0.35, aim=(-0.1, 0.0, TOP + 0.02))
        light('lantern', 'AREA', (-0.15, -0.15, TOP + 0.95), 22, (1.0, 0.62, 0.3), 0.5)
        light('bounce', 'AREA', (-1.0, -1.2, 2.0), 13, (0.55, 0.65, 1.0), 1.5)
    else:
        light('fire', 'AREA', (0.9, -0.5, TOP + 0.1), 70, SAT((1.0, 0.42, 0.12)), 0.08, aim=(0.0, -0.1, TOP + 0.05))
        light('bounce', 'AREA', (-1.0, -1.2, 2.0), 8, (0.55, 0.65, 1.0), 1.5)
    if '--hearth' in argv:
        # the hearth fire the key light stands for, as a surface: camera-invisible, but seen in reflections, so
        # glass, glaze and copper show a long warm fire reflection instead of a pin-point (a game's reflection
        # probe would capture the real fireplace)
        bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.95, -0.55, TOP + 0.15)); hp = bpy.context.active_object
        hp.name = 'hearth_reflector'; hp.scale = (0.7, 0.35, 1.0)
        hp.rotation_euler = (Vector((0.1, 0.0, TOP + 0.1)) - hp.location).to_track_quat('Z', 'Y').to_euler()
        hm = bpy.data.materials.new('M_Hearth'); hm.use_nodes = True; nt = hm.node_tree; nt.nodes.clear()
        em = nt.nodes.new('ShaderNodeEmission'); em.inputs['Color'].default_value = (1.0, 0.42, 0.12, 1); em.inputs['Strength'].default_value = 5.0
        tx = nt.nodes.new('ShaderNodeTexNoise'); tx.inputs['Scale'].default_value = 3.0
        mm = nt.nodes.new('ShaderNodeMath'); mm.operation = 'MULTIPLY_ADD'; mm.inputs[1].default_value = 6.0; mm.inputs[2].default_value = 1.0
        nt.links.new(tx.outputs['Fac'], mm.inputs[0]); nt.links.new(mm.outputs[0], em.inputs['Strength'])
        out = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(em.outputs[0], out.inputs['Surface'])
        hp.data.materials.append(hm); hp.visible_camera = False; hp.visible_shadow = False
    if '--flames' in argv: flame_cards((0.46, 0.04, TOP + 0.18), 0.1, 0.12, 8, 'brazier', ring=0.118)   # opt-in: fire is engine VFX, cards read as white spikes in review   # tongues licking up round the pot base
    for o in sc.objects:
        if o.name in ('wall', 'floor'): o.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.02, 0.016, 0.013, 1)
    views = [v if v[0] != 'kcd_mortar' else ('kcd_mortar', (-0.04, -0.36, TOP + 0.36), (-0.02, -0.15, TOP + 0.05), 28, 11.0) for v in views]
else:
    clear_lights(); interior_env((0.035, 0.025, 0.018), 2.5, 3.0)
    light('candle', 'POINT', (-0.66, 0.2, TOP + 0.165), 6.0, (1.0, 0.6, 0.28), 0.01)
    light('brazier', 'POINT', (0.46, 0.04, TOP + 0.25), 25.0, (1.0, 0.38, 0.12), 0.06)
    light('fire', 'AREA', (1.25, -0.55, 0.95), 70, (1.0, 0.45, 0.18), 0.12)
    light('window', 'AREA', (-1.2, -1.4, 2.4), 70, (0.65, 0.75, 1.0), 1.2)
for v in views:
    if not VIEWS or v[0] in VIEWS: shoot('hearth_' + v[0], *v[1:])
# neutral studio for material reading (skipped in the kcd look: reviewers read it as "clean render")
if LOOK == 'kcd': VIEWS = ['__none__']
clear_lights(); interior_env((0.08, 0.08, 0.085), 3.0, 0.5)
light('key', 'AREA', (-1.2, -1.8, 2.4), 260, (1, 0.97, 0.92), 1.5)
light('fill', 'AREA', (2.0, -1.4, 1.4), 80, (0.9, 0.95, 1.0), 2.0)
light('rim', 'AREA', (0.4, 1.5, 2.2), 180, (1, 1, 1), 1.0)
for v in views[:2]:
    if not VIEWS or ('studio_' + v[0]) in VIEWS: shoot('studio_' + v[0], *v[1:])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f'{TAG}.blend'))
print('COMPOSED', TAG)
