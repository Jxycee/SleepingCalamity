"""Tobin Marsh concept blockouts (M10 concept stage). Rough primitives, flat colours, no detail.
Run: blender -b --factory-startup --python-exit-code 1 --python Art/Blender/concepts/tobin_blockout.py -- --out <dir> [--only a|b|c]
Writes option_a.jpg, option_b.jpg, option_c.jpg (2048x768 turnaround: front, 3/4, side, back + small gameplay-distance figure).
Blender axes here (z up, front -y); concept art only, not an export generator."""
import bpy, math, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
out = os.path.abspath(argv[argv.index('--out') + 1]) if '--out' in argv else os.getcwd()
only = argv[argv.index('--only') + 1] if '--only' in argv else 'abc'


def hexc(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def srgb2lin(c):
    return tuple((v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4) for v in c)


SKIN, OAK, IRON = '#c98f6b', '#6b4a2b', '#6e7378'
OPTS = {
    'a': dict(top='#d8cdb0', trouser='#5a4a38', boot='#3f2c20', apron='#8a5a30', belt='#2e2520', hair='#3a2a1e'),
    'b': dict(top='#5f6b4a', trouser='#4e4844', boot='#3f2c20', cloak='#7c3f2e', fur='#cbbd9c', belt='#2e2520',
              hair='#4a3426', extra='#b6ab28'),
    'c': dict(top='#cbbd9c', vest='#3d5a6e', trouser='#6b5a44', boot='#3f2c20', belt='#4e3420', wrap='#e6dcc0',
              hair='#5a3a22', extra='#c8802a'),
}
_mats = {}


def mat(h):
    if h in _mats:
        return _mats[h]
    m = bpy.data.materials.new(h)
    c = srgb2lin(hexc(h))
    m.diffuse_color = c + (1,)
    m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF')
    if b:
        b.inputs['Base Color'].default_value = c + (1,)
    _mats[h] = m
    return m


def prim(kind, loc, scale, h, rot=(0, 0, 0), parent=None, r2=0.6, minor=0.25):
    if kind == 'sphere':
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1)
    elif kind == 'cyl':
        bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=1, depth=1)
    elif kind == 'cone':
        bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=1, radius2=r2, depth=1)
    elif kind == 'cube':
        bpy.ops.mesh.primitive_cube_add(size=1)
    elif kind == 'torus':
        bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=minor)
    o = bpy.context.active_object
    o.location = loc
    o.scale = scale
    o.rotation_euler = rot
    o.data.materials.append(mat(h))
    for p in o.data.polygons:
        p.use_smooth = True
    if parent:
        o.parent = parent
    return o


def limb(a, b, r, h, par):
    a, b = Vector(a), Vector(b)
    d = b - a
    o = prim('cyl', (a + b) / 2, (r, r, d.length), h, parent=par)
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = d.to_track_quat('Z', 'Y')
    return o


def figure(k, loc, yaw, s=1.0):
    o = OPTS[k]
    root = bpy.data.objects.new('fig_' + k, None)
    bpy.context.collection.objects.link(root)
    root.location = loc
    root.rotation_euler = (0, 0, yaw)
    root.scale = (s, s, s)
    P = root
    # 1.90 m, ~6.5 heads (head 0.29 m), broad shoulders
    for sx in (-1, 1):
        limb((sx * .13, 0, .10), (sx * .13, 0, .95), .105, o['trouser'], P)
        prim('cube', (sx * .13, -.04, .07), (.15, .30, .14), o['boot'], parent=P)   # chunky boots
    prim('sphere', (0, 0, 1.0), (.27, .17, .22), o['trouser'], parent=P)
    prim('cyl', (0, 0, 1.27), (.31, .20, .52), o['top'], parent=P)
    prim('sphere', (0, 0, 1.50), (.40, .22, .17), o['top'], parent=P)
    prim('cyl', (0, 0, 1.64), (.075, .075, .10), SKIN, parent=P)
    prim('sphere', (0, -.01, 1.76), (.135, .15, .16), SKIN, parent=P)
    prim('sphere', (0, .02, 1.80), (.145, .155, .14), o['hair'], parent=P)
    prim('sphere', (0, -.15, 1.74), (.035, .03, .035), SKIN, parent=P)             # nose marks the front
    return P, o


def arm(P, sh, el, hd, upper, lower, hand=SKIN):
    limb(sh, el, .09, upper, P)
    limb(el, hd, .08, lower, P)
    prim('sphere', hd, (.085, .075, .085), hand, parent=P)                          # hands ~1.15x


def axe(P, base, direction, length=.95, side=1):
    base, d = Vector(base), Vector(direction).normalized()
    top = base + d * length
    limb(base, top, .028, OAK, P)
    q = d.to_track_quat('Z', 'Y')
    blade = prim('cube', top - d * .08 + Vector((side * .09, 0, 0)), (.20, .035, .16), IRON, parent=P)
    blade.rotation_mode = 'QUATERNION'
    blade.rotation_quaternion = q
    cheek = prim('cube', top - d * .08, (.07, .06, .10), IRON, parent=P)
    cheek.rotation_mode = 'QUATERNION'
    cheek.rotation_quaternion = q


def build(k, loc, yaw, s=1.0):
    P, o = figure(k, loc, yaw, s)
    L = ((-.42, 0, 1.50), (-.55, -.05, 1.22), (-.58, -.14, 1.00))
    R = ((.42, 0, 1.50), (.55, -.05, 1.22), (.58, -.14, 1.00))
    if k == 'a':   # timber-yard worker: rolled sleeves, leather apron, axe across the back
        arm(P, *L, o['top'], SKIN)
        arm(P, *R, o['top'], SKIN)
        for sx in (-1, 1):
            prim('torus', (sx * .50, -.04, 1.34), (.095, .095, .095), o['top'], rot=(0, math.radians(75), 0), parent=P, minor=.3)
        prim('cube', (0, -.19, 1.02), (.46, .04, .62), o['apron'], parent=P)
        prim('cube', (0, -.19, 1.46), (.34, .04, .14), o['apron'], parent=P)
        prim('cyl', (0, 0, 1.03), (.33, .22, .08), o['belt'], parent=P)
        axe(P, (.22, .24, .75), (-.35, 0, 1.0), 1.0, side=1)
    if k == 'b':   # mountain warden: long wool cloak, fur collar, axe held low
        prim('cone', (0, .10, 1.10), (.52, .30, 1.15), o['cloak'], parent=P, r2=.45)
        prim('torus', (0, 0, 1.60), (.30, .20, .20), o['fur'], parent=P, minor=.40)
        arm(P, *L, o['top'], o['top'], o['belt'])
        arm(P, (.42, 0, 1.50), (.58, -.08, 1.20), (.62, -.20, .98), o['top'], o['top'], o['belt'])
        prim('cyl', (0, 0, 1.03), (.33, .22, .07), o['belt'], parent=P)
        axe(P, (.62, -.20, 1.02), (0.04, -.06, -1.0), 1.0, side=1)
    if k == 'c':   # town carpenter: waistcoat, tool belt, bandaged forearms, axe over shoulder
        arm(P, *L, o['top'], o['wrap'])
        arm(P, (.42, 0, 1.50), (.52, -.10, 1.30), (.40, -.22, 1.62), o['top'], o['wrap'])
        prim('cube', (0, -.19, 1.30), (.46, .05, .46), o['vest'], parent=P)
        prim('cyl', (0, 0, 1.03), (.33, .22, .10), o['belt'], parent=P)
        for x in (-.2, .0, .2):
            prim('cube', (x, -.22, .97), (.09, .06, .12), o['extra'], parent=P)
        prim('cyl', (-.30, -.10, .86), (.02, .02, .30), OAK, parent=P)
        prim('cube', (-.30, -.10, 1.03), (.10, .05, .05), IRON, parent=P)
        axe(P, (.36, -.30, 1.45), (-.1, .6, 1.0), 1.0, side=-1)
    return P


def render(k, path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear()
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'
    sh.color_type = 'MATERIAL'
    sh.show_shadows = True
    sc.display.render_aa = '8'
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 2048, 768
    sc.render.image_settings.file_format = 'JPEG'
    sc.render.image_settings.quality = 90
    w = bpy.data.worlds.new('w')
    w.color = srgb2lin(hexc('#8d9096'))
    sc.world = w
    xs = [-3.6, -1.8, 0.0, 1.8]
    yaws = [0, math.radians(-40), math.radians(-90), math.radians(180)]
    for x, yw in zip(xs, yaws):
        build(k, (x, 0, 0), yw)
    build(k, (4.0, 0, 0), math.radians(-25), s=0.3)    # gameplay-camera-size figure (5-6.5 m boom), corner
    prim('cube', (0.2, 0, -0.02), (10.2, 1.2, 0.04), '#6a6d72')
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 10.24
    cam.location = (0, -10, 1.35)
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.camera = cam
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


os.makedirs(out, exist_ok=True)
for k in only:
    render(k, os.path.join(out, 'option_%s.jpg' % k))
