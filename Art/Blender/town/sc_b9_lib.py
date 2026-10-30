"""sc_b9_lib: shared helpers for the B9 foliage and B8 lantern generators (sc_foliage_town.py, sc_lanterns_adopt.py).

Runs inside Blender's Python (numpy, no Pillow): image IO through bpy, periodic blur, colour grading toward the PHASE1 3.1
key hues, a bilinear sampler for compositing atlas cells, a mesh builder with per-corner UV, bent normals and an AO colour
attribute, FBX export with custom normals, and the review stage (grey corner stand-ins, the three brief cameras, midday sun).

Image arrays are H x W x C float32, row 0 = the BOTTOM of the image (bpy pixel order, so v = row / H directly).
Unity -> Blender: (xu, yu, zu) -> (-xu, -zu, yu); a model's front is -Y in Blender (= +z in Unity, the project convention).
"""
import os, math, colorsys
import numpy as np
import bpy
from mathutils import Vector, Matrix

# ----------------------------------------------------------------------------- colour
def srgb2lin(x):
    x = np.asarray(x, dtype=np.float32)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4).astype(np.float32)

def lin2srgb(x):
    x = np.clip(np.asarray(x, dtype=np.float32), 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * x ** (1 / 2.4) - 0.055).astype(np.float32)

def hex2srgb(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)

def hex2lin(h):
    return srgb2lin(hex2srgb(h))

def lum(lin):
    return lin[..., 0] * 0.2126 + lin[..., 1] * 0.7152 + lin[..., 2] * 0.0722

def grade(rgb, mask, target_hex, hue_keep=0.6, contrast=1.1, chroma=1.12, delight=0.0, sigma=40):
    """sRGB scan colour -> graded sRGB. Statistics over mask > 0.5 only (leaf pixels, not the background).
    The masked linear mean lands on target_hex (before chroma); the scan's luminance structure is kept (contrast power),
    hue_keep mixes the scan's own hue variation (1) with the target colour painted over its luminance (0)."""
    lin = srgb2lin(rgb[..., :3]); m = mask > 0.5
    L = lum(lin) + 1e-5
    if delight > 0:
        low = blur(L * m, sigma) / np.maximum(blur(m.astype(np.float32), sigma), 1e-3)
        lin = lin * np.clip((L[m].mean() / np.maximum(low, 1e-4)) ** delight, 0.6, 1.7)[..., None]; L = lum(lin) + 1e-5
    lin = lin * ((L / L[m].mean()) ** (contrast - 1.0))[..., None]; L = lum(lin) + 1e-5
    tgt = hex2lin(target_hex)
    gain = lin * (tgt / np.maximum(lin[m].mean(0), 1e-5))
    paint = tgt[None, None, :] * (L / L[m].mean())[..., None]
    out = gain * hue_keep + paint * (1.0 - hue_keep)
    out = out * (tgt / np.maximum(out[m].mean(0), 1e-5))
    Lo = lum(out)[..., None]; out = Lo + (out - Lo) * chroma
    return lin2srgb(out)

# ----------------------------------------------------------------------------- image ops
def blur(a, sigma):
    """Periodic gaussian blur (FFT), 2D or 3D array."""
    h, w = a.shape[:2]
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.fftfreq(w)[None, :]
    g = np.exp(-2 * (math.pi * sigma) ** 2 * (fx * fx + fy * fy)).astype(np.float32)
    if a.ndim == 2:
        return np.real(np.fft.ifft2(np.fft.fft2(a) * g)).astype(np.float32)
    return np.stack([np.real(np.fft.ifft2(np.fft.fft2(a[..., c]) * g)) for c in range(a.shape[2])], -1).astype(np.float32)

def fbm(res, seed, beta=2.2, kmin=2.0, kmax=None):
    """Tileable noise in 0..1 (spectrum-shaped white noise)."""
    r = np.random.default_rng(seed)
    h, w = (res, res) if np.isscalar(res) else res
    f = np.sqrt(np.fft.fftfreq(h)[:, None] ** 2 * h * h + np.fft.fftfreq(w)[None, :] ** 2 * w * w)
    amp = np.where(f >= kmin, np.maximum(f, 1e-6) ** (-beta / 2), 0)
    if kmax: amp = amp * (f <= kmax)
    n = np.real(np.fft.ifft2(np.fft.fft2(r.standard_normal((h, w))) * amp))
    n = (n - n.min()) / (n.max() - n.min() + 1e-9)
    return n.astype(np.float32)

def sstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def load(path, res=None, nonc=True):
    img = bpy.data.images.load(os.path.abspath(path), check_existing=False)
    img.colorspace_settings.name = 'Non-Color'
    w, h = img.size; a = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    a = a.reshape(h, w, 4)
    if res and res != w: a = resize(a, res, res)
    return a

def resize(a, h, w):
    H, W = a.shape[:2]
    if H % h == 0 and W % w == 0:
        return a.reshape(h, H // h, w, W // w, -1).mean((1, 3)).reshape(h, w, *a.shape[2:])
    ys = (np.arange(h) + 0.5) * H / h - 0.5; xs = (np.arange(w) + 0.5) * W / w - 0.5
    X, Y = np.meshgrid(xs, ys)
    return sample(a, X, Y)

def sample(a, x, y, outside=0.0):
    """Bilinear sample of a (H x W x C) at float pixel coords x, y (any shape); zero outside."""
    H, W = a.shape[:2]
    x0 = np.floor(x).astype(np.int64); y0 = np.floor(y).astype(np.int64); fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    def g(yy, xx):
        ok = (xx >= 0) & (xx < W) & (yy >= 0) & (yy < H)
        v = a[np.clip(yy, 0, H - 1), np.clip(xx, 0, W - 1)]
        return np.where(ok[..., None], v, outside)
    return (g(y0, x0) * (1 - fx) * (1 - fy) + g(y0, x0 + 1) * fx * (1 - fy) + g(y0 + 1, x0) * (1 - fx) * fy + g(y0 + 1, x0 + 1) * fx * fy).astype(np.float32)

def save(a, path, alpha=None):
    """Write a PNG. a: H x W x 3 (0..1 file values); alpha: H x W or None (RGB)."""
    h, w = a.shape[:2]
    rgba = np.concatenate([a[..., :3], (alpha if alpha is not None else np.ones((h, w), np.float32))[..., None]], -1)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=alpha is not None)
    img.colorspace_settings.name = 'Non-Color'; img.alpha_mode = 'STRAIGHT'
    img.pixels.foreach_set(np.clip(rgba, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = path; img.file_format = 'PNG'
    sc = bpy.context.scene; s = sc.render.image_settings
    s.color_mode = 'RGBA' if alpha is not None else 'RGB'
    img.save(); bpy.data.images.remove(img)

def bleed(rgb, alpha, sigma=6, passes=3):
    """Push colour out under transparent pixels (no dark halo when mips average the edge)."""
    out = rgb.copy(); m = (alpha > 0.5).astype(np.float32)
    for k in range(passes):
        s = sigma * (3 ** k)
        fill = blur(rgb * m[..., None], s) / np.maximum(blur(m, s), 1e-4)[..., None]
        out = np.where(m[..., None] > 0, out, np.where(blur(m, s)[..., None] > 1e-3, fill, out))
        m = np.maximum(m, (blur(m, s) > 1e-3).astype(np.float32))
    return out

def erode(mask, px=1):
    m = mask.copy()
    for _ in range(px):
        m = np.minimum.reduce([m, np.roll(m, 1, 0), np.roll(m, -1, 0), np.roll(m, 1, 1), np.roll(m, -1, 1)])
    return m

def raster_uv(polys_uv, res):
    """Coverage mask (res x res) of UV polygons (list of N x 2 arrays, 0..1). Row 0 = v 0."""
    m = np.zeros((res, res), np.float32)
    for P in polys_uv:
        P = np.asarray(P) * res
        for k in range(1, len(P) - 1):
            a, b, c = P[0], P[k], P[k + 1]
            x0, x1 = int(max(0, np.floor(min(a[0], b[0], c[0])))), int(min(res - 1, np.ceil(max(a[0], b[0], c[0]))))
            y0, y1 = int(max(0, np.floor(min(a[1], b[1], c[1])))), int(min(res - 1, np.ceil(max(a[1], b[1], c[1]))))
            if x1 < x0 or y1 < y0: continue
            X, Y = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            d = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(d) < 1e-12: continue
            l1 = ((b[1] - c[1]) * (X - c[0]) + (c[0] - b[0]) * (Y - c[1])) / d
            l2 = ((c[1] - a[1]) * (X - c[0]) + (a[0] - c[0]) * (Y - c[1])) / d
            ins = (l1 >= -0.02) & (l2 >= -0.02) & (1 - l1 - l2 >= -0.02)
            m[y0:y1 + 1, x0:x1 + 1] = np.maximum(m[y0:y1 + 1, x0:x1 + 1], ins)
    return m

def normal_from_height(h, strength=2.0):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * strength
    n = np.stack([-dx, -dy, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return (n * 0.5 + 0.5).astype(np.float32)

def renorm(nrgb):
    n = nrgb[..., :3] * 2 - 1; n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    return (n * 0.5 + 0.5).astype(np.float32)

# ----------------------------------------------------------------------------- mesh builder
class MB:
    """Faces with per-corner position, UV, normal and AO (one vertex per corner: cards and ribbons are cheap)."""
    def __init__(self):
        self.p = []; self.uv = []; self.n = []; self.ao = []; self.f = []; self.mat = []
    def face(self, pts, uvs, nrm, ao, mat=0):
        i = len(self.p)
        self.p += [tuple(p) for p in pts]; self.uv += [tuple(u) for u in uvs]
        self.n += [tuple(Vector(n).normalized()) for n in nrm]; self.ao += list(ao)
        self.f.append(tuple(range(i, i + len(pts)))); self.mat.append(mat)
    def tris(self):
        return sum(len(f) - 2 for f in self.f)

def build(name, mb, mats=(), coll=None):
    """Mesh object from an MB, with welded duplicates where position, UV and normal all match (keeps vertex count honest)."""
    P = np.array(mb.p, np.float64); UV = np.array(mb.uv, np.float64); N = np.array(mb.n, np.float64)
    key = np.round(np.hstack([P * 1e5, UV * 1e5, N * 1e3])).astype(np.int64)
    _, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel()
    verts = P[first]
    keep = [k for k, f in enumerate(mb.f) if len({int(inv[i]) for i in f}) == len(f)]     # drop faces that welded shut
    faces = [tuple(int(inv[i]) for i in mb.f[k]) for k in keep]
    me = bpy.data.meshes.new(name); me.from_pydata(verts.tolist(), [], faces)
    # corner index = order of face corners as given (from_pydata keeps it)
    corner = np.concatenate([np.array(mb.f[k]) for k in keep])
    mb_mat = [mb.mat[k] for k in keep]
    uvl = me.uv_layers.new(name='UVMap'); uvl.data.foreach_set('uv', UV[corner].astype(np.float32).ravel())
    col = me.color_attributes.new('Col', 'BYTE_COLOR', 'CORNER')
    ao = np.array(mb.ao, np.float32)[corner]
    col.data.foreach_set('color', np.stack([ao, ao, ao, np.ones_like(ao)], -1).ravel())
    me.polygons.foreach_set('material_index', mb_mat)
    me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
    me.normals_split_custom_set([tuple(v) for v in N[corner]])
    ob = bpy.data.objects.new(name, me)
    (coll or bpy.context.scene.collection).objects.link(ob)
    for m in mats: me.materials.append(m)
    return ob

def export_fbx(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for o in bpy.context.view_layer.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True,
                             mesh_smooth_type='OFF', colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS',
                             bake_space_transform=True, add_leaf_bones=False, bake_anim=False, use_custom_props=False,
                             use_tspace=True, path_mode='STRIP')

def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

# ----------------------------------------------------------------------------- materials (review stand-ins for URP Lit)
def tex_node(nt, path, nonc):
    nd = nt.nodes.new('ShaderNodeTexImage')
    nd.image = bpy.data.images.load(os.path.abspath(path), check_existing=True)
    nd.image.colorspace_settings.name = 'Non-Color' if nonc else 'sRGB'
    nd.image.alpha_mode = 'STRAIGHT'
    return nd

def pbr_material(name, prefix, clip=True, two_sided=True, alpha_blend=False, alpha_scale=1.0, emission=None):
    """Principled from <prefix>_BaseColor/_Normal/_ORM (URP Lit: alpha clip 0.5, Render Face Both)."""
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bc = tex_node(nt, prefix + '_BaseColor.png', False); nm = tex_node(nt, prefix + '_Normal.png', True); orm = tex_node(nt, prefix + '_ORM.png', True)
    sep = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(orm.outputs['Color'], sep.inputs['Color'])
    mul = nt.nodes.new('ShaderNodeMix'); mul.data_type = 'RGBA'; mul.blend_type = 'MULTIPLY'; mul.inputs['Factor'].default_value = 1.0
    nt.links.new(bc.outputs['Color'], mul.inputs['A']); nt.links.new(sep.outputs['Red'], mul.inputs['B'])
    nt.links.new(mul.outputs['Result'], bs.inputs['Base Color'])
    nt.links.new(sep.outputs['Green'], bs.inputs['Roughness']); nt.links.new(sep.outputs['Blue'], bs.inputs['Metallic'])
    nmap = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nm.outputs['Color'], nmap.inputs['Color']); nt.links.new(nmap.outputs['Normal'], bs.inputs['Normal'])
    if clip:
        gt = nt.nodes.new('ShaderNodeMath'); gt.operation = 'GREATER_THAN'; gt.inputs[1].default_value = 0.5
        nt.links.new(bc.outputs['Alpha'], gt.inputs[0]); nt.links.new(gt.outputs[0], bs.inputs['Alpha'])
        m.surface_render_method = 'DITHERED'
    if alpha_blend:
        ms = nt.nodes.new('ShaderNodeMath'); ms.operation = 'MULTIPLY'; ms.inputs[1].default_value = alpha_scale
        nt.links.new(bc.outputs['Alpha'], ms.inputs[0]); nt.links.new(ms.outputs[0], bs.inputs['Alpha'])
        m.surface_render_method = 'BLENDED'
        bs.inputs['Roughness'].default_value = 0.1
    if emission:
        bs.inputs['Emission Color'].default_value = (*emission[0], 1); bs.inputs['Emission Strength'].default_value = emission[1]
    m.use_backface_culling = not two_sided
    return m

def flat_material(name, hexcol, rough=0.85, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value = (*hex2lin(hexcol), 1); bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    return m

# ----------------------------------------------------------------------------- review stage (grey corner stand-ins)
def u2b(x, y, z):
    return Vector((-x, -z, y))

JETTY_Z = 3.0      # jetty height as the corner builder's ivy placeholder has it (B4 not merged yet): ivy and stand-in both use it
CAMS = {'wide': ((1, 1.8, -9), (-1, 3, 6)), 'mid': ((5, 1.7, -2.5), (-1.5, 1.8, 5)), 'close': ((1.6, 1.4, -1.6), (0, 1.0, 0.6))}

def review_scene(eng='BLENDER_EEVEE', jetty=JETTY_Z):
    sc = bpy.context.scene
    sc.render.engine = eng
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.42, 0.55, 0.75, 1); bg.inputs['Strength'].default_value = 0.9
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 4.5; sun.data.color = (1.0, 0.93, 0.82); sun.data.angle = math.radians(1.5)
    # midday, sun high in the south-east (Unity south = Blender +Y): light travels north-west and down
    sun.rotation_euler = (math.radians(38), 0, math.radians(160))
    stand = []
    def box(name, x0, x1, y0, y1, z0, z1, mat):
        bpy.ops.mesh.primitive_cube_add(size=1, location=((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
        o = bpy.context.active_object; o.name = name; o.scale = (x1 - x0, y1 - y0, z1 - z0)
        o.data.materials.append(mat); stand.append(o); return o
    plaster = flat_material('SB_Plaster', '#cbbd9c'); stone = flat_material('SB_Stone', '#9a8f84'); timber = flat_material('SB_Timber', '#3e342c')
    cob = flat_material('SB_Cobble', '#8f8884'); sand = flat_material('SB_Sandstone', '#b49a78'); canvas = flat_material('SB_Canvas', '#c08a3c')
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0)); g = bpy.context.active_object; g.name = 'SB_Ground'; g.data.materials.append(cob); stand.append(g)
    # facade: Unity x -5..2.5, front z 7.5 facing -Z -> Blender x -2.5..5, front y -7.5 facing +Y; upper storey jettied 0.45 toward +Y
    box('SB_Plinth', -2.5, 5, -12, -7.47, 0, 0.6, stone)
    box('SB_Ground', -2.5, 5, -12, -7.5, 0.6, jetty - 0.28, plaster)
    box('SB_JettyBeam', -2.5, 5.0, -12, -7.05, jetty - 0.28, jetty, timber)
    box('SB_Upper', -2.5, 5.0, -12, -7.05, jetty, 6.0, plaster)
    box('SB_Door', 0.6, 1.6, -7.52, -7.4, 0.0, 2.2, timber)
    bpy.ops.mesh.primitive_cylinder_add(radius=1.7, depth=0.55, location=(0, 0, 0.275), vertices=64); o = bpy.context.active_object; o.data.materials.append(sand); stand.append(o)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.22, depth=2.4, location=(0, 0, 1.2), vertices=24); o = bpy.context.active_object; o.data.materials.append(sand); stand.append(o)
    # stall at Unity (4.5, 4.5): a counter and an awning slab (stand-in, not rotated)
    box('SB_Counter', -5.8, -3.2, -5.3, -3.7, 0, 0.9, timber)
    box('SB_Awning', -5.9, -3.1, -5.5, -3.3, 2.6, 2.68, canvas)
    return stand

def camera(name, eye_u, at_u, fov_y=60):
    """Unity-style camera: vertical field of view (Unity default 60)."""
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); bpy.context.scene.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(fov_y); cam.data.clip_start = 0.05
    e = u2b(*eye_u); t = u2b(*at_u); cam.location = e
    cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
    return cam

def render(cam, path, res=960, samples=32):
    sc = bpy.context.scene; sc.camera = cam
    sc.render.resolution_x = res; sc.render.resolution_y = int(res * 9 / 16); sc.render.resolution_percentage = 100
    if sc.render.engine == 'CYCLES':
        sc.cycles.samples = samples; sc.cycles.use_denoising = True
    else:
        sc.eevee.taa_render_samples = samples
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)
