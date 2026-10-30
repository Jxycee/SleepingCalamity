"""sc_tex_sheet_blender.py (M8): Blender-side of the batch contact sheet. One session renders every material of a batch as a cell:
a 2 x 2 tile plane (so seams show) plus a sphere, neutral light, Standard view transform, albedo/normal/ORM wired the way URP Lit reads them.
Run by sc_tex_sheet.py:  blender -b --factory-startup --python-exit-code 1 --python sc_tex_sheet_blender.py -- <batch dir> <out dir> <cell px> <tile metres>
Cells are written as <out dir>/<name>.png. A calibration pass scales the sun so a 50% sRGB grey plane renders near 0.5 (so albedo measured on the render
reads as albedo, not as albedo x an arbitrary exposure). Decals are drawn over a neutral mid-tan plane; trim sheets are shown once (not tiled).
"""
import bpy, math, os, sys, glob
from mathutils import Vector

batch_dir, out_dir, cell, tile_m = sys.argv[sys.argv.index('--') + 1:][:4]
cell = int(cell); tile_m = float(tile_m)
os.makedirs(out_dir, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)   # no default cube, light or camera
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.resolution_x = sc.render.resolution_y = cell
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'; sc.render.film_transparent = True
sc.view_settings.view_transform = 'Standard'; sc.view_settings.look = 'None'
sc.eevee.taa_render_samples = 24

w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs[1].default_value = 0.6
# metals need something to reflect (sc-asset-craft): a gradient sky + bright ground instead of a flat colour
tc = w.node_tree.nodes.new('ShaderNodeTexCoord'); sep = w.node_tree.nodes.new('ShaderNodeSeparateXYZ'); ramp = w.node_tree.nodes.new('ShaderNodeValToRGB')
w.node_tree.links.new(tc.outputs['Generated'], sep.inputs[0]); w.node_tree.links.new(sep.outputs['Z'], ramp.inputs[0])
ramp.color_ramp.elements[0].position = 0.3; ramp.color_ramp.elements[0].color = (0.35, 0.32, 0.28, 1)
ramp.color_ramp.elements[1].position = 0.8; ramp.color_ramp.elements[1].color = (0.75, 0.85, 1.0, 1)
w.node_tree.links.new(ramp.outputs[0], bg.inputs[0])

sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.0; sun.angle = math.radians(2.0)
so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(48), 0, math.radians(35))
cam = bpy.data.cameras.new('c'); cam.lens = 50
co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co); sc.camera = co
co.location = (0, -5.4, 3.5)
co.rotation_euler = (Vector((0, 5.4, -3.5)).to_track_quat('-Z', 'Y').to_euler())


def img(path, nc):
    im = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    im.colorspace_settings.name = 'Non-Color' if nc else 'sRGB'
    return im


def material(name, d, decal=False, grey=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); bs = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bs.outputs[0], out.inputs[0])
    if grey is not None:
        bs.inputs['Base Color'].default_value = (grey, grey, grey, 1); bs.inputs['Roughness'].default_value = 1.0
        return m
    uv = nt.nodes.new('ShaderNodeUVMap')
    def tex(path, nc):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = img(path, nc); t.interpolation = 'Cubic'
        nt.links.new(uv.outputs[0], t.inputs[0]); return t
    a = tex(os.path.join(d, f'{name}_albedo.png'), False)
    n = tex(os.path.join(d, f'{name}_normal.png'), True)
    o = tex(os.path.join(d, f'{name}_orm.png'), True)
    sp = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(o.outputs[0], sp.inputs[0])
    nm = nt.nodes.new('ShaderNodeNormalMap'); nm.space = 'TANGENT'; nt.links.new(n.outputs[0], nm.inputs['Color'])
    mixao = nt.nodes.new('ShaderNodeMix'); mixao.data_type = 'RGBA'; mixao.blend_type = 'MULTIPLY'; mixao.inputs[0].default_value = 0.6
    nt.links.new(a.outputs[0], mixao.inputs[6]); nt.links.new(sp.outputs[0], mixao.inputs[7])
    nt.links.new(mixao.outputs[2], bs.inputs['Base Color'])
    nt.links.new(sp.outputs[1], bs.inputs['Roughness']); nt.links.new(sp.outputs[2], bs.inputs['Metallic'])
    nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    if decal:
        base = nt.nodes.new('ShaderNodeBsdfPrincipled'); base.inputs['Base Color'].default_value = (0.30, 0.26, 0.20, 1); base.inputs['Roughness'].default_value = 0.9
        mix = nt.nodes.new('ShaderNodeMixShader'); nt.links.new(a.outputs[1], mix.inputs[0])
        nt.links.new(base.outputs[0], mix.inputs[1]); nt.links.new(bs.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
    return m


def plane(size, uv_tiles):
    me = bpy.data.meshes.new('p'); h = size / 2
    me.from_pydata([(-h, -h, 0), (h, -h, 0), (h, h, 0), (-h, h, 0)], [], [(0, 1, 2, 3)])
    uvl = me.uv_layers.new(name='UVMap')
    for l, uv in zip(me.loops, [(0, 0), (uv_tiles, 0), (uv_tiles, uv_tiles), (0, uv_tiles)]):
        uvl.data[l.index].uv = uv
    ob = bpy.data.objects.new('plane', me); sc.collection.objects.link(ob); return ob


def sphere(r, uv_tiles):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=r, segments=48, ring_count=24, location=(0, -0.2, r))
    ob = bpy.context.active_object
    bpy.ops.object.shade_smooth()
    for l in ob.data.uv_layers.active.data:
        l.uv = (l.uv[0] * uv_tiles, l.uv[1] * uv_tiles * 0.5)
    return ob


def render(path):
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)


def clear():
    for o in [o for o in bpy.data.objects if o.name.startswith(('plane', 'Sphere'))]:
        bpy.data.objects.remove(o, do_unlink=True)


# calibration: 50% sRGB grey plane
lin50 = 0.2140
p = plane(4.0, 1); p.data.materials.append(material('g', '', grey=lin50))
render(os.path.join(out_dir, '_cal0.png'))
def centre_mean(path):
    im = bpy.data.images.load(path); px = list(im.pixels); w_, h_ = im.size
    vals = [px[(y * w_ + x) * 4] for y in range(h_ // 2 - 6, h_ // 2 + 6) for x in range(w_ // 2 - 6, w_ // 2 + 6)]
    bpy.data.images.remove(im); return sum(vals) / len(vals)
m0 = centre_mean(os.path.join(out_dir, '_cal0.png'))   # sRGB value of the 8-bit PNG
def s2l(x): return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
def render_cal(energy):
    sun.energy = energy; render(os.path.join(out_dir, '_cal.png'))
    return s2l(centre_mean(os.path.join(out_dir, '_cal.png')))
# two-point linear fit (ambient + k * sun) so the world light is accounted for
e1, e2 = 1.0, 2.0
l1, l2 = render_cal(e1), render_cal(e2)
k = (l2 - l1) / (e2 - e1); amb = l1 - k * e1
sun.energy = max((lin50 - amb) / max(k, 1e-6), 0.05)
print('CAL grey50: ambient %.3f, k %.3f -> sun %.2f' % (amb, k, sun.energy))
print('CAL after %.3f (target 0.500)' % (m0 * 0 + (lambda v: 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055)(render_cal(sun.energy))))
clear()

names = sorted(d for d in os.listdir(batch_dir) if os.path.isdir(os.path.join(batch_dir, d)))
for name in names:
    d = os.path.join(batch_dir, name)
    decal = name in ('moss', 'grime', 'drip', 'crack'); trim = name.startswith('trim_')
    tiles = 1 if (decal or trim) else 2
    pl = plane(tile_m * 2 if not (decal or trim) else 2.6, tiles)
    pl.data.materials.append(material(name, d, decal=decal))
    sp = sphere(0.62, 1 if not trim else 1)
    sp.data.materials.append(material(name, d, decal=decal))
    render(os.path.join(out_dir, name + '.png'))
    print('CELL', name)
    clear()
