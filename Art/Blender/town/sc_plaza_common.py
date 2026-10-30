"""Shared helpers for the Hollowmere beauty-corner ground (B6): sc_cobbles.py and sc_flag_ring.py import this.

Layout and cameras come from docs/art/beauty-corner-brief.md section 1 (Unity metres, +Z north, origin = fountain centre).
Generators author in Blender axes (z up); Unity (x, y, z) = Blender (-x, z, -y), so Blender = (-x_u, -z_u, y_u)
(sc-asset-craft 2). The Unity cameras face north (+Z_u), which is -Y in Blender.

Ground height: the plaza dishes 5 cm toward the fountain (`dish(r)`, Unity y): 0 at r >= 8.7 m, -0.05 m at the basin
foot (r = 1.7 m). The basin (B10) therefore stands on y = -0.05 at its foot.

Review renders: Cycles CPU, a 5200 K midday sun plus a cool sky, AgX, Unity's default 60 deg vertical FOV. The facade,
basin and stall are flat-shaded stand-ins (other batches own them) so the ground is judged in the corner's light.
"""
import math, os
import bpy
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import sc_aaa_kit as K
from mathutils import Vector, Matrix

BASIN_R = 1.7                 # basin outer radius (B10: diameter 3.4 m)
RING_W = 1.2                  # flagstone ring width (B6)
RING_R = BASIN_R + RING_W     # 2.9: the cobbles start here
PATCH_U = (-8.0, 8.0, -6.0, 8.0)   # Unity x0, x1, z0, z1

# capture cameras, Unity coordinates (brief section 1)
CAMS_U = {'wide': ((1.0, 1.8, -9.0), (-1.0, 3.0, 6.0)),
          'mid': ((5.0, 1.7, -2.5), (-1.5, 1.8, 5.0)),
          'close': ((1.6, 1.4, -1.6), (0.0, 1.0, 0.6))}
SUN_DIR_U = (-0.45, -0.80, 0.40)   # light travels down, toward north-west: lit fronts, short midday shadows


def u2b(p):
    """Unity point -> Blender point."""
    return Vector((-p[0], -p[2], p[1]))


def b2u(p):
    return (-p[0], p[2], -p[1])


def rot_b2u_quat(Mb):
    """Blender 3x3 rotation -> Unity quaternion (x, y, z, w) under the mirror mapping above."""
    M = Matrix(((-1, 0, 0), (0, 0, 1), (0, -1, 0)))           # Blender -> Unity
    Ru = M @ Mb.to_3x3() @ M.inverted()
    q = Ru.to_quaternion()
    return (q.x, q.y, q.z, q.w)


def smoothstep(x):
    x = min(max(x, 0.0), 1.0)
    return x * x * (3 - 2 * x)


def dish(r):
    """Ground height (m) at distance r from the fountain centre: -5 cm at the basin foot, 0 beyond 8.7 m."""
    return -0.05 * (1.0 - smoothstep((r - BASIN_R) / 7.0))


def figure_and_door(sc, at_u=(-3.2, 0.0, 5.6)):
    """1.75 m figure and a 2.2 m door frame for scale (sc-asset-review 1)."""
    p = u2b(at_u)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(p.x, p.y, 0.875))
    o = bpy.context.active_object; o.name = 'figure_1.75m'; o.data.materials.append(flat_mat('fig', (0.12, 0.12, 0.13)))
    return o


def flat_mat(name, col, rough=0.8):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    bs = m.node_tree.nodes['Principled BSDF']; bs.inputs['Base Color'].default_value = (*col, 1); bs.inputs['Roughness'].default_value = rough
    return m


def box_u(name, x0, x1, y0, y1, z0, z1, col):
    """Axis box given in Unity extents."""
    a, b = u2b((x0, y0, z0)), u2b((x1, y1, z1))
    c = (a + b) / 2; s = Vector([abs(b[i] - a[i]) for i in range(3)])
    bpy.ops.mesh.primitive_cube_add(size=1, location=c); o = bpy.context.active_object; o.name = name
    o.scale = s; bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(flat_mat('M_' + name, col)); return o


def standins(basin=True):
    """Neutral stand-ins for the other batches' pieces (not shipped, not judged)."""
    obs = []
    if basin:
        bpy.ops.mesh.primitive_cylinder_add(radius=BASIN_R, depth=0.6, vertices=96, location=(0, 0, 0.25))
        o = bpy.context.active_object; o.name = 'standin_basin'; o.data.materials.append(flat_mat('M_basin', (0.46, 0.38, 0.27))); obs.append(o)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.22, depth=2.4, location=(0, 0, 1.2))
        o = bpy.context.active_object; o.name = 'standin_spout'; o.data.materials.append(flat_mat('M_basin', (0.46, 0.38, 0.27))); obs.append(o)
    obs.append(box_u('standin_plinth', -5.0, 2.5, -0.2, 0.6, 7.5, 9.0, (0.20, 0.18, 0.16)))
    obs.append(box_u('standin_facade', -5.0, 2.5, 0.6, 6.0, 7.5, 9.0, (0.55, 0.47, 0.33)))
    obs.append(box_u('standin_upper', -5.0, 2.5, 3.2, 6.0, 7.05, 7.5, (0.55, 0.47, 0.33)))
    obs.append(box_u('standin_door', -1.85, -0.75, 0.0, 2.2, 7.45, 7.5, (0.10, 0.07, 0.05)))
    st = box_u('standin_stall', 3.2, 5.8, 0.0, 0.9, 3.7, 5.3, (0.30, 0.20, 0.12))
    aw = box_u('standin_awning', 3.0, 6.0, 2.5, 2.7, 3.5, 5.5, (0.55, 0.35, 0.12))
    for o in (st, aw):
        c = u2b((4.5, 0, 4.5)); o.matrix_world = Matrix.Translation((c.x, c.y, 0)) @ Matrix.Rotation(math.radians(-200), 4, 'Z') @ Matrix.Translation((-c.x, -c.y, 0)) @ o.matrix_world
    obs += [st, aw]
    return obs


def light_world(sc, mood='midday'):
    """5200 K sun + cool sky dome (camera rays see a pale sky gradient)."""
    for o in [o for o in sc.objects if o.type == 'LIGHT']: bpy.data.objects.remove(o)
    sd = bpy.data.lights.new('sun', 'SUN'); sd.angle = math.radians(1.2)
    d = u2b(SUN_DIR_U).normalized()
    if mood == 'lowsun':
        d = u2b((-0.75, -0.30, 0.55)).normalized(); sd.energy = 3.2; sd.color = (1.0, 0.72, 0.48)
    else:
        sd.energy = 5.0; sd.color = (1.0, 0.84, 0.68)
    so = bpy.data.objects.new('sun', sd); sc.collection.objects.link(so)
    so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.world = bpy.data.worlds.new('W'); sc.world.use_nodes = True
    wn, wl = sc.world.node_tree.nodes, sc.world.node_tree.links
    bg = wn['Background']; tc = wn.new('ShaderNodeTexCoord'); sep = wn.new('ShaderNodeSeparateXYZ'); rmp = wn.new('ShaderNodeValToRGB')
    wl.new(tc.outputs['Generated'], sep.inputs[0]); wl.new(sep.outputs['Z'], rmp.inputs[0])
    rmp.color_ramp.elements[0].position = 0.48; rmp.color_ramp.elements[0].color = (0.17, 0.21, 0.26, 1)
    rmp.color_ramp.elements[1].position = 0.75; rmp.color_ramp.elements[1].color = (0.36, 0.50, 0.75, 1)
    wl.new(rmp.outputs['Color'], bg.inputs['Color']); bg.inputs['Strength'].default_value = 0.6 if mood == 'midday' else 0.4


def render_cams(sc, out, stem, views, rres=960, samples=48, mood='midday'):
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples; sc.cycles.use_denoising = True
    sc.render.resolution_x = rres; sc.render.resolution_y = int(rres * 9 / 16)
    sc.render.image_settings.file_format = 'JPEG'; sc.render.image_settings.quality = 92
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    light_world(sc, mood)
    sc.render.threads_mode = 'AUTO'; sc.cycles.max_bounces = 6
    paths = []
    for v in views:
        pos, look = CAMS_U[v]
        cd = bpy.data.cameras.new(v); cd.sensor_fit = 'VERTICAL'; cd.angle_y = math.radians(60.0)
        cam = bpy.data.objects.new(v, cd); sc.collection.objects.link(cam)
        cam.location = u2b(pos); cam.rotation_euler = (u2b(look) - u2b(pos)).to_track_quat('-Z', 'Y').to_euler()
        sc.camera = cam; sc.render.filepath = os.path.join(out, f'{stem}_{mood}_{v}.jpg')
        bpy.ops.render.render(write_still=True); print('RENDER', sc.render.filepath); paths.append(sc.render.filepath)
    return paths


def in_any_frame(p_u, margin=0.15):
    """True if a Unity ground point projects inside any capture camera's frame (16:9, 60 deg vertical)."""
    for pos, look in CAMS_U.values():
        P, L = Vector(pos), Vector(look)
        f = (L - P).normalized(); r = f.cross(Vector((0, 1, 0))).normalized(); u = r.cross(f)
        d = Vector(p_u) - P; z = d.dot(f)
        if z <= 0.05: continue
        ty = math.tan(math.radians(30)) * (1 + margin); tx = ty * 16 / 9
        if abs(d.dot(r) / z) < tx and abs(d.dot(u) / z) < ty: return True
    return False


def bed_material(name):
    """Dirt and grit in the joints. Periodic in UV (one tile = 2 m) through a 4D torus, so the same nodes bake
    the CobbleBed tileable and shade the bed mesh; the `damp` vertex colour darkens and greens it (render only)."""
    m, T = K.new_mat(name); T.grime = None
    uv = T.sep((T.node('ShaderNodeTexCoord'), 'UV'))
    R = 2.0 / (2 * math.pi)
    def torus():
        a = T.math('MULTIPLY', (uv, 'X'), 2 * math.pi); b = T.math('MULTIPLY', (uv, 'Y'), 2 * math.pi)
        v = T.comb(T.math('MULTIPLY', T.math('COSINE', a), R), T.math('MULTIPLY', T.math('SINE', a), R), T.math('MULTIPLY', T.math('COSINE', b), R))
        return v, T.math('MULTIPLY', T.math('SINE', b), R)
    v, w = torus()
    def n4(scale, detail=4.0):
        nd = T.node('ShaderNodeTexNoise', noise_dimensions='4D'); T.set(nd, 'Vector', v); T.set(nd, 'W', w)
        nd.inputs['Scale'].default_value = scale; nd.inputs['Detail'].default_value = detail; return (nd, 'Fac')
    def vor4(scale):
        nd = T.node('ShaderNodeTexVoronoi', voronoi_dimensions='4D'); T.set(nd, 'Vector', v); T.set(nd, 'W', w)
        nd.inputs['Scale'].default_value = scale; return nd
    dn = n4(6.0); fine = n4(60.0, 2.0)
    col = T.ramp(dn, [(0.35, (0.030, 0.026, 0.021)), (0.65, (0.065, 0.055, 0.043))])
    g = vor4(140.0)                                           # grit: 3-7 mm pebbles
    peb = T.math('MULTIPLY', T.smoothstep((g, 'Distance'), 0.42, 0.22), T.smoothstep((T.sep((g, 'Color')), 'X'), 0.35, 0.45))
    col = T.mix(T.math('MULTIPLY', peb, 0.9), col, T.mix((T.sep((g, 'Color')), 'Y'), (0.12, 0.115, 0.11), (0.24, 0.22, 0.20)))
    g2 = vor4(40.0)                                           # a few bigger stones (1-2 cm)
    peb2 = T.math('MULTIPLY', T.smoothstep((g2, 'Distance'), 0.30, 0.18), T.smoothstep((T.sep((g2, 'Color')), 'X'), 0.82, 0.86))
    col = T.mix(peb2, col, (0.19, 0.175, 0.16))
    damp = T.sep((T.node('ShaderNodeVertexColor', layer_name='damp'), 'Color'))
    dm = (damp, 'X')
    col = T.mix(T.math('MULTIPLY', dm, 0.55), col, T.hsv(col, 0.5, 1.0, 0.55))
    moss = T.math('MULTIPLY', T.math('MULTIPLY', T.math('ADD', 0.4, T.math('MULTIPLY', dm, 0.6)), T.math('SUBTRACT', 1.0, (damp, 'Y'))), T.smoothstep(T.math('ADD', dn, T.math('MULTIPLY', fine, 0.3)), 0.42, 0.6))
    col = T.mix(T.math('MULTIPLY', moss, 0.85), col, T.mix(fine, (0.075, 0.095, 0.022), (0.20, 0.22, 0.06)))
    dust = T.math('MULTIPLY', (damp, 'Y'), T.smoothstep(dn, 0.25, 0.6))       # worn path: dry, dusty, no moss
    col = T.mix(T.math('MULTIPLY', dust, 0.8), col, T.mix(fine, (0.13, 0.115, 0.095), (0.19, 0.17, 0.14)))
    rough = T.mixf(peb, 0.95, 0.7)
    rough = T.mixf(dm, rough, 0.6)
    h = T.math('ADD', T.math('MULTIPLY', peb, 0.6), T.math('ADD', T.math('MULTIPLY', peb2, 1.0), T.math('MULTIPLY', fine, 0.2)))
    T.output(col, rough, 0.0, T.bump(h, 0.6, 0.004))
    return m
