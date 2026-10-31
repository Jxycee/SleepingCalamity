"""HeroesHome: the heroes' home in Hollowmere, a fixed landmark (Hollowmere mass-production design 3 and 6; STORY 8.2).

Two buildings the Long Night missed, both old stone from before the Bargain (older and heavier than the town's half-timber),
patched by the four heroes with salvaged planks, canvas and odd shutters. One state for now (lived in).

  HeroesHome_Cistern  the cistern house at the square's north-east corner (layout row HeroesHome_Cistern: 9 x 7.5 m, ground 0).
      Coursed rubble walls 0.75 m thick on a battered plinth, big sandstone quoins, parapet gables with coping and kneelers, a
      stone-slate roof in diminishing courses with a sandstone ridge, a stout chimney on the west gable. South (square) side: an
      arched door with a mismatched plank door standing ajar, Wren's list of every resident painted on a limewashed panel
      (HH_List decal, names are our own placeholders), a bench, a water butt under a timber gutter. Patches: plank-boarded
      breaches (south and west walls), a canvas tarp roped and weighted over missing slates, a plank patch on the north slope,
      mismatched shutters (green, oxide red, blue, bare boards). West yard: washing line on two posts, a covered wood pile.
      Inside: flagstone floor, the arched stair door to the Undercroft (P25; small plank door open) and warm light below (HH_Glow).
  HeroesHome_Annex    the bell-loft annex on the keep terrace beside the bell tower (row HeroesHome_Annex: 5.5 x 5 m, ground 4).
      An old stone ground storey (3 m) with an east door, a salvaged board loft above (mismatched vertical boards, a canvas-patched
      gable opening, a hoist beam with pulley, rope and bucket), roof half stone slate (west) and half boards (east) with a tarp,
      an iron stovepipe.

Contract (design 5.2): Blender z up, metres, pivot at the footprint centre at ground, x = Unity east, y = Unity north (the
builder places the FBX with ArtYaw 180, like the B4 facade: export -Z forward, Y up turns Blender -Y to Unity local +Z).
Children of the root empty <Name>: Frame (all opaque geometry), Decals (FK_Decal_* + HH_List, no shadows), Glow (HH_Glow,
emissive), FOOTPRINT (flat body-size box, material FOOTPRINT), SOCKET_Door (empty, door threshold centre), and on the cistern
house SOCKET_UndercroftStair (stair head). T2: no unique bake; every slot samples the shared B1 tileables on metre UVs (2 m per
tile, like B4) with the B4 slot names (FK_*), plus HH_* slots (tinted B1 tileables, the list decal, the glow) that the Unity
builder maps (HeroesHomeArt.cs). Placed wear lives in the CORNER colour attribute 'wear' (linear 0-1: R grime/soot, G moss,
B polish), read by WearLit. Budget: <= 75k tris per building (PHASE1 3.3; printed).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_heroes_home.py -- --out <scratch>
      [--views square,front,west,yard,annex,stair,close] [--rres 960] [--samples 32] [--no-render] [--export]
  Renders <out>/hh_<view>.png from the play positions (both buildings at their layout places, a greybox terrace wall and bell
  tower behind). --export writes <out>/fbx/<Name>.fbx and copies them with the list texture to
  Assets/_Project/Content/Art/Town/Ring1/HeroesHome/ (SOURCES.md there).

refs: Reference/hollowmere/heroes_home/REFS.md (old rubble walls and stone-slate roofs), Reference/hollowmere/materials (Cotswold
slate roofs), PHASE1 3.1 palette, 3.4 shape language (1.3x arches, 1-3 deg off true, sagging ridges, chamfers).

Status: see Reference/hollowmere/heroes_home/STATUS.md (score, tris, rounds).
"""
import sys, os, math, random, shutil, subprocess
import bpy, bmesh
from mathutils import Vector, Matrix, noise

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
def arg(k, d=None):
    return argv[argv.index(k) + 1] if k in argv else d
OUT = os.path.abspath(arg('--out') or sys.exit('pass --out <scratch dir>')); os.makedirs(OUT, exist_ok=True)
VIEWS = arg('--views', 'square,front,west,yard,annex,stair,close').split(',')
RRES = int(arg('--rres', '960')); SAMPLES = int(arg('--samples', '32'))
RENDER = '--no-render' not in argv; EXPORT = '--export' in argv
REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
TEX = os.path.join(REPO, 'Art', 'Textures')
EXP = os.path.join(REPO, 'Assets', '_Project', 'Content', 'Art', 'Town', 'Ring1', 'HeroesHome')
TILE = 2.0
PAINT_LUM = 0.17                # mean linear luminance of the painted_wood albedo (as sc_facade_kit)
def _lin(h):
    return tuple(((int(h[i:i + 2], 16) / 255) / 12.92) if int(h[i:i + 2], 16) / 255 <= 0.04045 else (((int(h[i:i + 2], 16) / 255 + 0.055) / 1.055) ** 2.4) for i in (1, 3, 5))
PAINTS = {'HH_PaintRed': _lin('#8a3a2a'), 'HH_PaintBlue': _lin('#4d6c8c'), 'HH_PaintOchre': _lin('#b08a3a')}   # faded oxide, blue, ochre
OAK_TINT = (0.80, 0.76, 0.72)
# layout rows (HollowmereLayout.LandmarkRows): centre x, z (Unity) and ground height
SITES = {'HeroesHome_Cistern': (12.5, 11.25, 0.0), 'HeroesHome_Annex': (11.9, 24.0, 4.0)}
STAIR = (15.0 - 12.5, 12.5 - 11.25)   # HollowmereLayout.UndercroftStair in the cistern house's frame

# ----------------------------------------------------------------------------- materials
MATS = {}
def _img(path, nonc):
    im = bpy.data.images.load(path, check_existing=True)
    if nonc:
        im.colorspace_settings.name = 'Non-Color'
    return im

def tile_mat(name, folder, tint=(1, 1, 1), normal=1.0, paint=None):
    """The B4 kit's tile material (as sc_ruin_kit.tile_mat): tileable x tint, wear R grime, G moss, B polish."""
    if name in MATS:
        return MATS[name]
    stem = folder.split('/')[-1]; base = os.path.join(TEX, folder, stem)
    m = bpy.data.materials.new(name); m.use_nodes = True
    N = m.node_tree.nodes; Lk = m.node_tree.links; bsdf = N['Principled BSDF']
    alb = N.new('ShaderNodeTexImage'); alb.image = _img(base + '_albedo.png', False)
    nrm = N.new('ShaderNodeTexImage'); nrm.image = _img(base + '_normal.png', True)
    orm = N.new('ShaderNodeTexImage'); orm.image = _img(base + '_orm.png', True)
    col = N.new('ShaderNodeVertexColor'); col.layer_name = 'wear'
    sep = N.new('ShaderNodeSeparateColor'); Lk.new(col.outputs[0], sep.inputs[0])
    tin = N.new('ShaderNodeMix'); tin.data_type = 'RGBA'; tin.blend_type = 'MULTIPLY'
    tin.inputs[0].default_value = 1.0; tin.inputs[7].default_value = (*tint, 1); Lk.new(alb.outputs[0], tin.inputs[6])
    if paint:   # the B4 neighbours' paint mode (WearLit _SC_PAINT): greyscale(albedo) x paint / PAINT_LUM
        bw = N.new('ShaderNodeRGBToBW'); Lk.new(alb.outputs[0], bw.inputs[0]); Lk.new(bw.outputs[0], tin.inputs[6])
        tin.inputs[7].default_value = (*[c / PAINT_LUM for c in paint], 1)
    gri = N.new('ShaderNodeMix'); gri.data_type = 'RGBA'; gri.inputs[7].default_value = (0.055, 0.045, 0.036, 1)
    Lk.new(sep.outputs[0], gri.inputs[0]); Lk.new(tin.outputs[2], gri.inputs[6])
    pol = N.new('ShaderNodeMix'); pol.data_type = 'RGBA'; pol.blend_type = 'SCREEN'; pol.inputs[7].default_value = (0.30, 0.26, 0.20, 1)
    Lk.new(sep.outputs[2], pol.inputs[0]); Lk.new(gri.outputs[2], pol.inputs[6])
    tco = N.new('ShaderNodeTexCoord')
    nz = N.new('ShaderNodeTexNoise'); nz.inputs['Scale'].default_value = 3.0; nz.inputs['Detail'].default_value = 6
    Lk.new(tco.outputs['Object'], nz.inputs['Vector'])
    mr = N.new('ShaderNodeMapRange'); Lk.new(nz.outputs[0], mr.inputs[0]); mr.inputs[1].default_value = 0.42; mr.inputs[2].default_value = 0.62
    mm = N.new('ShaderNodeMath'); mm.operation = 'MULTIPLY'; Lk.new(sep.outputs[1], mm.inputs[0]); Lk.new(mr.outputs[0], mm.inputs[1])
    mc = N.new('ShaderNodeMix'); mc.data_type = 'RGBA'; mc.inputs[6].default_value = (0.10, 0.13, 0.03, 1); mc.inputs[7].default_value = (0.30, 0.30, 0.04, 1)
    Lk.new(nz.outputs[0], mc.inputs[0])
    mo = N.new('ShaderNodeMix'); mo.data_type = 'RGBA'
    Lk.new(mm.outputs[0], mo.inputs[0]); Lk.new(pol.outputs[2], mo.inputs[6]); Lk.new(mc.outputs[2], mo.inputs[7])
    Lk.new(mo.outputs[2], bsdf.inputs['Base Color'])
    so = N.new('ShaderNodeSeparateColor'); Lk.new(orm.outputs[0], so.inputs[0])
    ra = N.new('ShaderNodeMath'); ra.operation = 'MULTIPLY_ADD'; ra.use_clamp = True
    Lk.new(mm.outputs[0], ra.inputs[0]); ra.inputs[1].default_value = 0.25; Lk.new(so.outputs[1], ra.inputs[2])
    Lk.new(ra.outputs[0], bsdf.inputs['Roughness']); Lk.new(so.outputs[2], bsdf.inputs['Metallic'])
    nm = N.new('ShaderNodeNormalMap'); nm.inputs['Strength'].default_value = normal
    Lk.new(nrm.outputs[0], nm.inputs['Color']); Lk.new(nm.outputs[0], bsdf.inputs['Normal'])
    MATS[name] = m
    return m

def flat_mat(name, col, rough, emit=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = (*col, 1); b.inputs['Emission Strength'].default_value = emit
    MATS[name] = m
    return m

def decal_mat(name, path, opacity=1.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True; m.blend_method = 'BLEND'
    N = m.node_tree.nodes; Lk = m.node_tree.links; b = N['Principled BSDF']
    t = N.new('ShaderNodeTexImage'); t.image = _img(path, False); t.extension = 'CLIP'
    Lk.new(t.outputs['Color'], b.inputs['Base Color'])
    mul = N.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'; mul.inputs[1].default_value = opacity
    Lk.new(t.outputs['Alpha'], mul.inputs[0]); Lk.new(mul.outputs[0], b.inputs['Alpha'])
    b.inputs['Roughness'].default_value = 0.9
    MATS[name] = m
    return m

LIST_PNG = os.path.join(OUT, 'HH_List_BaseColor.png')
SLOT_DEFS = {   # slot -> builder. FK_* = the B4 facade slots (BeautyCornerArt.FacadeSlots); HH_* = HeroesHomeArt.cs
    'FK_Sandstone': lambda: tile_mat('FK_Sandstone', 'B1_town/sandstone'),
    'FK_ChimneyStone': lambda: tile_mat('FK_ChimneyStone', 'B1_town/sandstone', tint=(0.80, 0.74, 0.68)),
    'FK_PlinthJoint': lambda: tile_mat('FK_PlinthJoint', 'B1_town/sandstone', tint=(0.30, 0.28, 0.24)),
    'FK_Oak': lambda: tile_mat('FK_Oak', 'B1_town/oak_timber', tint=OAK_TINT, normal=1.2),
    'FK_OakDoor': lambda: tile_mat('FK_OakDoor', 'B1_town/oak_timber', tint=(1.35, 1.25, 1.15)),
    'FK_RoofTile': lambda: tile_mat('FK_RoofTile', 'B1_town/roof_tile'),
    'FK_Plaster': lambda: tile_mat('FK_Plaster', 'B1_town/plaster'),
    'FK_Shutter': lambda: tile_mat('FK_Shutter', 'B1_town/painted_wood', tint=(0.57, 0.73, 0.28)),
    'FK_Iron': lambda: tile_mat('FK_Iron', 'B1_town/iron'),
    'FK_Glass': lambda: flat_mat('FK_Glass', (0.05, 0.075, 0.095), 0.1),
    'HH_OldStone': lambda: tile_mat('HH_OldStone', 'B1_town/sandstone', tint=(0.66, 0.62, 0.57)),
    'HH_Slate': lambda: tile_mat('HH_Slate', 'B1_town/sandstone', tint=(0.50, 0.47, 0.43), normal=1.4),
    'HH_PlankGrey': lambda: tile_mat('HH_PlankGrey', 'B1_town/oak_timber', tint=(1.05, 1.05, 1.08)),
    'HH_PaintRed': lambda: tile_mat('HH_PaintRed', 'B1_town/painted_wood', paint=PAINTS['HH_PaintRed']),
    'HH_PaintBlue': lambda: tile_mat('HH_PaintBlue', 'B1_town/painted_wood', paint=PAINTS['HH_PaintBlue']),
    'HH_PaintOchre': lambda: tile_mat('HH_PaintOchre', 'B1_town/painted_wood', paint=PAINTS['HH_PaintOchre']),
    'HH_Canvas': lambda: tile_mat('HH_Canvas', 'B1_town/canvas'),
    'HH_ClothMadder': lambda: tile_mat('HH_ClothMadder', 'B1_town/canvas', tint=(0.85, 0.30, 0.22)),
    'HH_ClothBlue': lambda: tile_mat('HH_ClothBlue', 'B1_town/canvas', tint=(0.45, 0.58, 0.80)),
    'HH_ClothLinen': lambda: tile_mat('HH_ClothLinen', 'B1_town/canvas', tint=(1.15, 1.12, 1.05)),
    'HH_Rope': lambda: flat_mat('HH_Rope', (0.32, 0.25, 0.16), 0.9),
    'HH_Glow': lambda: flat_mat('HH_Glow', (1.0, 0.55, 0.22), 0.8, emit=6.0),
    'HH_Dark': lambda: flat_mat('HH_Dark', (0.035, 0.028, 0.022), 0.95),
    'HH_List': lambda: decal_mat('HH_List', LIST_PNG),
    'FK_Decal_drip': lambda: decal_mat('FK_Decal_drip', os.path.join(TEX, 'B3_trims', 'drip', 'drip_albedo.png'), 0.35),
}
DECAL_SLOTS = {'HH_List', 'FK_Decal_drip'}
GLOW_SLOTS = {'HH_Glow'}

# ----------------------------------------------------------------------------- Wren's list (the decal texture)
FIRST = ['Agnes', 'Bram', 'Corla', 'Dunstan', 'Edda', 'Rowan', 'Fennick', 'Gilda', 'Hob', 'Isolde', 'Jory', 'Kett', 'Linnet', 'Mabry', 'Nell',
         'Odo', 'Perrin', 'Quill', 'Rosamund', 'Sefton', 'Tamsin', 'Ulric', 'Verity', 'Wat', 'Yarrow', 'Aldous', 'Bess', 'Cuthbert', 'Dorcas',
         'Elric', 'Flora', 'Gideon', 'Hester', 'Ivo', 'Jonet', 'Kester', 'Lettice', 'Merrin', 'Noll', 'Orla', 'Piers', 'Rhoswen', 'Silas',
         'Thea', 'Umber', 'Wenna', 'Abel', 'Brigid', 'Cador', 'Dilys', 'Ebb', 'Fern']
LAST = ['Pell', 'Tuller', 'Hale', 'Marsh', 'Ostrander', 'Calder', 'Brackwater', 'Fenn', 'Larkin', 'Moss', 'Thatcher', 'Underhill', 'Vane',
        'Wick', 'Ashby', 'Barrow', 'Cobb', 'Dimmock', 'Elderkin', 'Furze', 'Greaves', 'Holloway', 'Kimble', 'Loame', 'Merriweather']

def make_list_texture(path):
    """Wren's painted list: every resident's name in brick-red lime paint, four columns, a heading, a few drips.
    Placeholder names (our own); the heroes' own families sit among them (Hale, Marsh, Ostrander, Calder)."""
    from PIL import Image, ImageDraw, ImageFont, ImageFilter
    W, H = 1024, 1024
    R = random.Random(81)
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    fdir = '/usr/share/fonts/truetype/dejavu/'
    head = ImageFont.truetype(fdir + 'DejaVuSerif-Bold.ttf', 64)
    body = ImageFont.truetype(fdir + 'DejaVuSerif.ttf', 30)
    paint = (92, 34, 22)
    d.text((W // 2, 50), 'HOLLOWMERE', font=head, fill=paint + (240,), anchor='mt')
    d.text((W // 2, 124), 'every one of you', font=ImageFont.truetype(fdir + 'DejaVuSerif.ttf', 32), fill=paint + (220,), anchor='mt')
    d.line([(150, 172), (874, 176)], fill=paint + (200,), width=5)
    names = []
    for i in range(120):
        names.append('%s %s' % (FIRST[(i * 7) % len(FIRST)], LAST[(i * 11 + i // 5) % len(LAST)]))
    names[3] = 'Corla Hale'; names[17] = 'Dunstan Marsh'; names[44] = 'Edda Ostrander'; names[71] = 'Rowan Calder'
    cols, rows = 4, 30
    for i, n in enumerate(names):
        c, r = divmod(i, rows)
        x = 34 + c * 248 + R.randint(-4, 4); y = 200 + r * 27 + R.randint(-2, 2)
        layer = Image.new('RGBA', (240, 34), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((2, 2), n, font=ImageFont.truetype(fdir + 'DejaVuSerif.ttf', 24 if len(n) < 15 else 21), fill=paint + (R.randint(190, 245),))
        layer = layer.rotate(R.uniform(-1.6, 1.6), resample=Image.BICUBIC)
        img.alpha_composite(layer, (x, y))
        if R.random() < 0.10:   # a drip from a loaded brush
            dx = x + R.randint(10, 120); dl = R.randint(8, 30)
            d.line([(dx, y + 24), (dx, y + 24 + dl)], fill=paint + (150,), width=2)
    # brush texture: alpha broken by a fine noise so the paint reads as lime paint on render, not print
    a = img.split()[3]
    nz = Image.effect_noise((W, H), 70).convert('L').filter(ImageFilter.GaussianBlur(0.8))
    a = Image.composite(a, Image.new('L', (W, H), 0), nz.point(lambda v: 255 if v > 70 else 0))
    img.putalpha(a.filter(ImageFilter.GaussianBlur(0.5)))
    # colour everywhere = paint (straight alpha), so mip edges don't fringe
    rgb = Image.new('RGB', (W, H), paint); out = Image.merge('RGBA', (*rgb.split(), img.split()[3]))
    out.save(path)

# ----------------------------------------------------------------------------- geometry
class Geo:
    """Verts + faces; per face: slot, wear tag, UV offset. UVs are metre box projections (TILE m per repeat)."""
    def __init__(self, name):
        self.name = name; self.v = []; self.f = []; self.slot = []; self.tag = []; self.uvo = []
    def add(self, verts, faces, slot, tag='stone', R=None):
        o = len(self.v); self.v += [Vector(p) for p in verts]
        off = (R.random() * 4, R.random() * 4) if R else (0.0, 0.0)
        for f in faces:
            self.f.append(tuple(i + o for i in f)); self.slot.append(slot); self.tag.append(tag); self.uvo.append(off)
    def tris(self):
        return sum(len(f) - 2 for f in self.f)

def M4(o, U, N, Z=Vector((0, 0, 1))):
    """Frame matrix: local x along U, y along -N (into the wall), z up; origin o."""
    U = Vector(U).normalized(); Nn = Vector(N).normalized(); Zz = Vector(Z).normalized()
    m = Matrix(((U.x, -Nn.x, Zz.x, o[0]), (U.y, -Nn.y, Zz.y, o[1]), (U.z, -Nn.z, Zz.z, o[2]), (0, 0, 0, 1)))
    return m

def rot(yaw=0.0, tx=0.0, ty=0.0):
    return Matrix.Rotation(yaw, 4, 'Z') @ Matrix.Rotation(tx, 4, 'X') @ Matrix.Rotation(ty, 4, 'Y')

CUBE_F = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]   # bottom, top, -y, +x, +y, -x
def box(g, slot, size, xf, tag='wood', R=None, cham=0.0, drop=()):
    """Cuboid centred at xf's origin, size in local axes; optional chamfer (cut corners on the vertical edges via an 8-gon
    section when cham > 0 would cost more: here a cheap bevel by pulling the top/bottom rims in). drop: face indices to skip."""
    sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
    vs = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz), (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
    if cham:
        vs = [(x - math.copysign(cham, x) * 0.5, y - math.copysign(cham, y) * 0.5, z) if z > 0 else (x, y, z) for x, y, z in vs]
    g.add([xf @ Vector(p) for p in vs], [f for i, f in enumerate(CUBE_F) if i not in drop], slot, tag, R)

def stone(g, slot, size, xf, R, soft=0.2, wob=0.012, bulge=0.02, tag='stone', back=True):
    """A pillow block (cube grid 2x2 per face, edges and corners pulled in, face bulge, wobble). Local -y = the show face;
    back=False drops the +y face (buried in the wall)."""
    n = 2; idx = {}; verts = []
    def vid(i, j, k):
        key = (i, j, k)
        if key not in idx:
            c = [2 * i / n - 1, 2 * j / n - 1, 2 * k / n - 1]
            edge = sum(1 for a in c if abs(a) > 0.999)
            pull = 1.0 - soft * max(0, edge - 1) / 2.0
            p = Vector((c[0] * pull * size[0] / 2, c[1] * pull * size[1] / 2, c[2] * pull * size[2] / 2))
            if c[1] < -0.999:
                p.y -= bulge * (1 - c[0] ** 2) * (1 - c[2] ** 2)
            p += Vector((R.uniform(-wob, wob), R.uniform(-wob, wob) * 0.5, R.uniform(-wob, wob) * 0.6))
            idx[key] = len(verts); verts.append(xf @ p)
        return idx[key]
    faces = []
    for a in range(n):
        for b in range(n):
            faces.append((vid(a, b, n), vid(a + 1, b, n), vid(a + 1, b + 1, n), vid(a, b + 1, n)))
            faces.append((vid(a, 0, b), vid(a + 1, 0, b), vid(a + 1, 0, b + 1), vid(a, 0, b + 1)))
            if back:
                faces.append((vid(a, n, b), vid(a, n, b + 1), vid(a + 1, n, b + 1), vid(a + 1, n, b)))
            faces.append((vid(0, a, b), vid(0, a, b + 1), vid(0, a + 1, b + 1), vid(0, a + 1, b)))
            faces.append((vid(n, a, b), vid(n, a + 1, b), vid(n, a + 1, b + 1), vid(n, a, b + 1)))
            faces.append((vid(a, b, 0), vid(a, b + 1, 0), vid(a + 1, b + 1, 0), vid(a + 1, b, 0)))
    g.add(verts, faces, slot, tag, R)

def cyl(g, slot, a, b, r, sides=8, tag='wood', R=None, caps=True, cap_slot=None, r2=None):
    a = Vector(a); b = Vector(b); ax = (b - a).normalized()
    t = ax.orthogonal().normalized(); s = ax.cross(t)
    r2 = r if r2 is None else r2
    vs = []
    for i in range(sides):
        ang = 2 * math.pi * i / sides
        d = t * math.cos(ang) + s * math.sin(ang)
        vs.append(a + d * r); vs.append(b + d * r2)
    fs = [(2 * i, 2 * ((i + 1) % sides), 2 * ((i + 1) % sides) + 1, 2 * i + 1) for i in range(sides)]
    g.add(vs, fs, slot, tag, R)
    if caps:
        o = len(g.v)
        g.add([vs[2 * i] for i in range(sides)], [tuple(reversed(range(sides)))], cap_slot or slot, tag, R)
        g.add([vs[2 * i + 1] for i in range(sides)], [tuple(range(sides))], cap_slot or slot, tag, R)

def quad_grid(g, slot, P, nu, nv, tag='cloth', R=None, two=False):
    """P(u, v) -> Vector on [0,1]^2."""
    vs = [P(i / nu, j / nv) for j in range(nv + 1) for i in range(nu + 1)]
    fs = [(j * (nu + 1) + i, j * (nu + 1) + i + 1, (j + 1) * (nu + 1) + i + 1, (j + 1) * (nu + 1) + i) for j in range(nv) for i in range(nu)]
    g.add(vs, fs, slot, tag, R)
    if two:
        g.add(vs, [tuple(reversed(f)) for f in fs], slot, tag, R)

def plank(g, slot, a, b, w, t, up, R, tag='wood', wob=0.01):
    """A board from a to b (centre line), width w along 'up' x dir, thickness t along the face normal (up x dir)."""
    a = Vector(a); b = Vector(b); d = (b - a); L = d.length; d.normalize()
    up = Vector(up).normalized(); nrm = d.cross(up).normalized(); up = nrm.cross(d).normalized()
    xf = Matrix(((d.x, nrm.x, up.x, 0), (d.y, nrm.y, up.y, 0), (d.z, nrm.z, up.z, 0), (0, 0, 0, 1)))
    xf = Matrix.Translation((a + b) / 2) @ xf @ rot(R.uniform(-wob, wob), 0, 0)
    box(g, slot, (L, t, w), xf, tag, R)

# ----------------------------------------------------------------------------- walls
class Opening:
    def __init__(self, u0, u1, z0, z1, kind='window', arch=False):
        self.u0, self.u1, self.z0, self.z1, self.kind, self.arch = u0, u1, z0, z1, kind, arch
        self.r = (u1 - u0) / 2; self.uc = (u0 + u1) / 2; self.spring = z1 - self.r if arch else z1
    def hits(self, u0, u1, z0, z1, pad):
        if self.arch:
            ring = 0.42
            if u1 > self.u0 - ring - pad and u0 < self.u1 + ring + pad and z1 > self.z0 - pad and z0 < self.spring:
                return u1 > self.u0 - pad and u0 < self.u1 + pad or (z0 < self.spring and (u1 > self.u0 - ring - pad and u0 < self.u1 + ring + pad) and False)
            # arch ring zone: any corner inside r + ring
            for (u, z) in ((u0, z0), (u1, z0), (u0, z1), (u1, z1), ((u0 + u1) / 2, (z0 + z1) / 2), ((u0 + u1) / 2, z0)):
                if z > self.spring - 0.01 and math.hypot(u - self.uc, z - self.spring) < self.r + ring + pad:
                    return True
            return False
        return u1 > self.u0 - pad and u0 < self.u1 + pad and z1 > self.z0 - pad and z0 < self.z1 + pad
    def inside(self, u, z):
        if self.arch and z > self.spring:
            return math.hypot(u - self.uc, z - self.spring) < self.r
        return self.u0 < u < self.u1 and self.z0 < z < self.z1

class Patch:
    """A breach in the stone, boarded over by the heroes: blocks inside a ragged ellipse are left out, boards cover it."""
    def __init__(self, uc, zc, ru, rz, seed):
        self.uc, self.zc, self.ru, self.rz, self.seed = uc, zc, ru, rz, seed
    def inside(self, u, z):
        ang = math.atan2(z - self.zc, u - self.uc)
        k = 1.0 + 0.22 * math.sin(ang * 3 + self.seed) + 0.12 * math.sin(ang * 7 + 2 * self.seed)
        return ((u - self.uc) / (self.ru * k)) ** 2 + ((z - self.zc) / (self.rz * k)) ** 2 < 1.0

class Wall:
    """One stone wall face: o = outer face start corner at ground, U along the face, N outward; top(u) = wall top (gable)."""
    def __init__(self, g, o, U, N, L, top, R, openings=(), patches=(), q0=True, q1=True, plinth=0.48, stone_slot='HH_OldStone',
                 base=0.0, course=(0.30, 0.52)):
        self.g, self.o, self.U, self.N, self.L, self.top, self.R = g, Vector(o), Vector(U).normalized(), Vector(N).normalized(), L, top, R
        self.ops, self.patches, self.q0, self.q1, self.plinth, self.slot, self.base, self.course = list(openings), list(patches), q0, q1, plinth, stone_slot, base, course
        self.xf = M4(self.o, self.U, self.N)
    def P(self, u, z, d=0.0):
        return self.o + self.U * u + Vector((0, 0, z)) + self.N * d
    def blocked(self, u0, u1, z0, z1, pad=0.03):
        if any(o.hits(u0, u1, z0, z1, pad) for o in self.ops):
            return True
        for p in self.patches:
            if any(p.inside(u, z) for u, z in ((u0, z0), (u1, z0), (u0, z1), (u1, z1), ((u0 + u1) / 2, (z0 + z1) / 2))):
                return True
        return False
    def block(self, u0, u1, z0, z1, proud, slot=None, depth=0.34, soft=0.2):
        R = self.R
        uc, zc = (u0 + u1) / 2, (z0 + z1) / 2
        dz0 = R.uniform(0.0, 0.03); dz1 = R.uniform(0.0, 0.07)   # irregular bed and top: old rubble courses, not brick
        zc = (z0 + dz0 + z1 - dz1) / 2
        xf = self.xf @ Matrix.Translation((uc, -(proud - depth / 2), zc)) @ rot(R.uniform(-0.03, 0.03), R.uniform(-0.02, 0.02), R.uniform(-0.04, 0.04))
        stone(self.g, slot or self.slot, (u1 - u0 - 0.05, depth, z1 - z0 - 0.045 - dz0 - dz1), xf, R, soft=0.3 if slot is None else 0.18, bulge=0.035, wob=0.02, back=False)
    def build(self):
        R = self.R; z = self.base; ci = 0
        while True:
            h = self.plinth if ci == 0 else R.uniform(*self.course)
            z1 = z + h
            if z1 > max(self.top(0), self.top(self.L), self.top(self.L / 2)) + 0.05:
                break
            proud = 0.12 if ci == 0 else 0.0
            u = 0.0
            if ci % 2:
                u = -R.uniform(0.0, 0.3)
            while u < self.L - 0.02:
                qz0 = self.q0 and u <= 0.0001 and ci > 0
                ln = (R.uniform(0.95, 1.45) if R.random() < 0.18 else R.uniform(0.45, 1.0)) * (1.2 if ci == 0 else 1.0)
                slot = None
                if qz0 or (self.q1 and ci > 0 and u + ln > self.L - (0.55 if ci % 2 else 0.95)):
                    pass
                u0 = max(u, 0.0); u1 = min(u + ln, self.L)
                if self.q0 and ci > 0 and u0 < 0.001:
                    u1 = 0.95 if ci % 2 == 0 else 0.55; slot = 'FK_Sandstone'
                if self.q1 and ci > 0 and u1 > self.L - 0.001 - 0.95:
                    qq = 0.55 if ci % 2 == 0 else 0.95
                    if u1 > self.L - qq - 0.25:
                        u1 = self.L
                        if u0 > self.L - qq - 0.05:
                            slot = 'FK_Sandstone'
                        else:
                            # split: a filler then the quoin
                            self._place(u0, self.L - qq, z, z1, proud + R.uniform(0, 0.025), None)
                            u0 = self.L - qq; slot = 'FK_Sandstone'
                self._place(u0, u1, z, z1, proud + (0.025 if slot else R.uniform(-0.01, 0.045)), slot)
                u = u1
            z = z1; ci += 1
    def _place(self, u0, u1, z0, z1, proud, slot):
        if u1 - u0 < 0.12:
            return
        t0, t1 = self.top(u0), self.top(u1)
        if z1 > min(t0, t1) + 0.02:
            if z0 > min(t0, t1) - 0.12:
                return
            z1 = min(t0, t1)
        if self.blocked(u0, u1, z0, z1):
            # try a short block hugging the opening (jambs cover the rest)
            return
        self.block(u0, u1, z0, z1, proud, slot)
    def core(self, inner=0.75, liner_slot='FK_PlinthJoint'):
        """Mortar bed behind the blocks (shows in the joints) as a grid with the openings cut out; grid lines snap to opening
        edges. Plus the wall's top (a flat cap under the coping) is left to the roof or coping pieces."""
        us = sorted(set([0.0, self.L] + [i * 0.3 for i in range(1, int(self.L / 0.3) + 1) if i * 0.3 < self.L - 0.05]
                        + [x for o in self.ops for x in (o.u0, o.u1) if 0 < x < self.L]))
        zt = max(self.top(u) for u in us)
        zs = sorted(set([self.base] + [self.base + i * 0.3 for i in range(1, int((zt - self.base) / 0.3) + 2)]
                        + [x for o in self.ops for x in (o.z0, o.spring) if self.base < x < zt]))
        d = -0.035
        for i in range(len(us) - 1):
            for j in range(len(zs) - 1):
                u0, u1, z0, z1 = us[i], us[i + 1], zs[j], zs[j + 1]
                ta, tb = self.top(u0), self.top(u1)
                if z0 >= max(ta, tb):
                    continue
                corners = [(u0, z0), (u1, z0), (u1, min(z1, tb)), (u0, min(z1, ta))]
                if any(o.inside(u, z) or (o.arch and z > o.spring - 0.01 and math.hypot(u - o.uc, z - o.spring) < o.r + 0.02 and o.u0 - 0.01 <= u <= o.u1 + 0.01)
                       for o in self.ops for u, z in [((u0 + u1) / 2, (z0 + min(z1, ta, tb)) / 2)] + corners[:0]):
                    continue
                if any(o.u0 - 0.001 <= (u0 + u1) / 2 <= o.u1 + 0.001 and o.z0 - 0.001 <= (z0 + z1) / 2 <= (o.z1 + 0.001) for o in self.ops):
                    continue
                vs = [self.P(u, min(zz, self.top(u)), d) for u, zz in ((u0, z0), (u1, z0), (u1, z1), (u0, z1))]
                self.g.add(vs, [(0, 1, 2, 3) if self._facing_ok() else (3, 2, 1, 0)], 'FK_PlinthJoint', 'core')
    def _facing_ok(self):
        # quad (u0,z0),(u1,z0),(u1,z1),(u0,z1): normal = U x Z; we want it along N
        return self.U.cross(Vector((0, 0, 1))).dot(self.N) > 0
    def dress(self, depth=0.75):
        """Jambs, sills, lintels / voussoirs and reveals for every opening."""
        R = self.R
        for o in self.ops:
            jd = depth + 0.02
            zt = o.spring
            z = o.z0 if o.kind == 'door' else o.z0
            # jambs: alternating long / short dressed stones, show face 2 cm proud, inner edge flush with the opening
            k = 0
            while z < zt - 0.05:
                h = min(R.uniform(0.32, 0.42), zt - z)
                for side in (0, 1):
                    w = 0.46 if (k + side) % 2 == 0 else 0.30
                    uo = o.u0 - w if side == 0 else o.u1
                    xf = self.xf @ Matrix.Translation((uo + w / 2, jd / 2 - 0.025, z + h / 2)) @ rot(0, 0, R.uniform(-0.01, 0.01))
                    stone(self.g, 'FK_Sandstone', (w - 0.02, jd, h - 0.02), xf, R, soft=0.12, bulge=0.012, back=False)
                z += h; k += 1
            if o.kind != 'door':   # sill
                xf = self.xf @ Matrix.Translation((o.uc, jd / 2 - 0.09, o.z0 - 0.09)) @ rot(0, math.radians(-4), 0)
                stone(self.g, 'FK_Sandstone', (o.u1 - o.u0 + 0.34, jd + 0.1, 0.17), xf, R, soft=0.1, bulge=0.01)
            else:   # worn threshold step
                xf = self.xf @ Matrix.Translation((o.uc, jd / 2 - 0.22, 0.07))
                stone(self.g, 'FK_Sandstone', (o.u1 - o.u0 + 0.5, jd + 0.45, 0.16), xf, R, soft=0.15, bulge=-0.02, tag='step')
            if o.arch:   # voussoirs, 1.3x chunky, odd count, keystone a touch proud
                n = 9 if o.r > 0.6 else 7
                ring = 0.40
                for i in range(n):
                    a0 = math.pi * i / n; a1 = math.pi * (i + 1) / n
                    key = i == n // 2
                    vs = []
                    for (a, rr) in ((a0, o.r), (a1, o.r), (a1, o.r + ring + (0.06 if key else 0)), (a0, o.r + ring + (0.06 if key else 0))):
                        aa = math.pi - a
                        for dd in (0.02 + (0.02 if key else 0), -jd + 0.02):
                            uu = o.uc + math.cos(aa) * rr; zz = o.spring + math.sin(aa) * rr
                            vs.append(self.P(uu, zz, dd))
                    # inset each wedge slightly (joints)
                    c = sum(vs, Vector()) / 8
                    vs = [c + (p - c) * 0.965 for p in vs]
                    fs = [(0, 2, 4, 6), (1, 7, 5, 3), (0, 1, 3, 2), (2, 3, 5, 4), (4, 5, 7, 6), (6, 7, 1, 0)]
                    if not self._facing_ok():
                        fs = [tuple(reversed(f)) for f in fs]
                    self.g.add(vs, fs, 'FK_Sandstone', 'stone', R)
            else:   # lintel
                xf = self.xf @ Matrix.Translation((o.uc, jd / 2 - 0.03, o.z1 + 0.17))
                stone(self.g, 'FK_Sandstone', (o.u1 - o.u0 + 0.62, jd, 0.34), xf, R, soft=0.12, bulge=0.015, back=False)
            # head/reveal liner behind the dressing (dark inner wall), so the deep reveal never shows sky
            if not o.arch:
                box(self.g, 'FK_PlinthJoint', (o.u1 - o.u0, depth, 0.05), self.xf @ Matrix.Translation((o.uc, depth / 2, o.z1 + 0.02)), 'core')

def inner_liner(g, w, t, height):
    """The wall's inside face (dark lime-wash) at depth t, with the same openings (mirrored along the face), so doors and windows
    open into the room instead of onto a plane."""
    Lin = w.L - 2 * t
    o = w.o - w.N * t + w.U * (w.L - t)
    ops = [Opening(w.L - t - op.u1, w.L - t - op.u0, op.z0, op.z1, op.kind, op.arch) for op in w.ops]
    iw = Wall(g, o, -w.U, -w.N, Lin, lambda u: height, w.R, ops, [])
    iw.core()

# ----------------------------------------------------------------------------- roof
def slate_roof(g, R, x0, x1, y_eave, y_ridge, z_eave, z_ridge, side, sag=0.0, skip=lambda x, s: False, slot='HH_Slate', ridge_slot='FK_Sandstone',
               course0=0.34, course1=0.20, thick=0.05):
    """One stone-slate slope from the eave line (y_eave, z_eave) to the ridge (y_ridge, z_ridge), along x0..x1. Diminishing
    courses (wide at the eave, narrow at the ridge, Cotswold practice), each course a row of slates of random width,
    lapped and tilted; skip(x, s) leaves holes (patches). Plus the deck under it (battens hidden) and the soffit."""
    run = Vector((0, y_ridge - y_eave, z_ridge - z_eave)); Ls = run.length; d = run.normalized()
    nrm = Vector((1, 0, 0)).cross(d) * (1 if side > 0 else -1)
    if nrm.z < 0:
        nrm = -nrm
    def S(x, s, off=0.0):   # point on the slope: x along ridge, s from eave (0) to ridge (Ls)
        t = (x - x0) / max(1e-6, (x1 - x0))
        sg = -sag * 4 * t * (1 - t) * (s / Ls)
        return Vector((x, y_eave, z_eave)) + d * s + nrm * (off + sg)
    # deck (one surface + soffit), with the sag
    nx = 10
    quad_grid(g, 'FK_Oak', lambda u, v: S(x0 + (x1 - x0) * u, Ls * v, -0.02), nx, 4, tag='wood', R=R, two=False)
    quad_grid(g, 'FK_Oak', lambda u, v: S(x0 + (x1 - x0) * (1 - u), Ls * v, -0.14), nx, 4, tag='wood', R=R, two=False)
    # eave fascia
    for i in range(nx):
        xa = x0 + (x1 - x0) * i / nx; xb = x0 + (x1 - x0) * (i + 1) / nx
        vs = [S(xa, 0, -0.14), S(xb, 0, -0.14), S(xb, 0, -0.02), S(xa, 0, -0.02)]
        fn = (vs[1] - vs[0]).cross(vs[3] - vs[0])
        g.add(vs, [(0, 1, 2, 3) if fn.dot(-d) > 0 else (3, 2, 1, 0)], 'FK_Oak', 'wood')
    s = -0.06; ci = 0
    while s < Ls - 0.12:
        t = s / Ls; c = course0 + (course1 - course0) * t
        c = c * R.uniform(0.92, 1.08)
        x = x0 - (R.uniform(0, 0.2) if ci % 2 else 0.0)
        while x < x1 - 0.02:
            w = R.uniform(0.26, 0.52) * (1.15 - 0.3 * t)
            xa = max(x, x0); xb = min(x + w, x1)
            if xb - xa > 0.08 and not skip((xa + xb) / 2, s + c / 2):
                lap = c * 1.7   # each slate is longer than the course (it laps under the next)
                p0 = S((xa + xb) / 2, s + lap / 2, 0.0)
                tilt = math.atan2(thick, lap) * 1.0
                xf = Matrix.Translation(p0) @ Matrix(((1, 0, 0, 0), (0, d.y, nrm.y, 0), (0, d.z, nrm.z, 0), (0, 0, 0, 1))).to_4x4() \
                    @ rot(R.uniform(-0.035, 0.035), -tilt + R.uniform(-0.02, 0.02), R.uniform(-0.03, 0.03))
                box(g, slot, (xb - xa - 0.015, lap, thick * R.uniform(0.8, 1.3)), xf @ Matrix.Translation((0, 0, thick * 0.6)), 'slate', R, cham=0.02, drop=(0,))
            x += w
        s += c; ci += 1
    return S, Ls, d, nrm

def ridge_cap(g, R, x0, x1, y, z, sag=0.0, slot='FK_Sandstone', seg=0.55):
    x = x0
    while x < x1 - 0.05:
        xb = min(x + seg, x1); t = ((x + xb) / 2 - x0) / max(1e-6, x1 - x0)
        zz = z - sag * 4 * t * (1 - t)
        xf = Matrix.Translation(((x + xb) / 2, y, zz + 0.02)) @ rot(0, math.radians(45), R.uniform(-0.03, 0.03)) @ rot(R.uniform(-0.02, 0.02), 0, 0)
        stone(g, slot, (xb - x - 0.02, 0.30, 0.30), Matrix.Translation(((x + xb) / 2, y, zz + 0.04)) @ Matrix.Rotation(math.radians(45), 4, 'X') @ rot(R.uniform(-0.02, 0.02)), R, soft=0.25, bulge=0.0, tag='stone')
        x = xb

# ----------------------------------------------------------------------------- dressing pieces
def door_leaf(g, R, w, h, xf, slots, arch_r=None, tag='door'):
    """A ledged plank door (local x across, z up, front face toward local -y at y=0). Mismatched boards from slots; top cut to
    the arch when arch_r (spring at h - arch_r). Ledges and iron straps on the front."""
    x = -w / 2; i = 0
    while x < w / 2 - 0.02:
        bw = min(R.uniform(0.15, 0.24), w / 2 - x)
        xc = x + bw / 2
        top = h
        if arch_r:
            spring = h - arch_r; dx = abs(xc) if abs(xc) < arch_r else arch_r
            top = spring + math.sqrt(max(0.0, arch_r ** 2 - dx ** 2)) - 0.02
        top -= R.uniform(0, 0.03)
        sl = slots[R.randrange(len(slots))] if i % 3 else slots[0]
        box(g, sl, (bw - 0.012, 0.05, top), xf @ Matrix.Translation((xc, 0.025, top / 2)) @ rot(0, 0, R.uniform(-0.008, 0.008)), tag, R, cham=0.01)
        x += bw; i += 1
    for zl in (0.35, h * 0.5, h - (arch_r or 0) - 0.25):
        box(g, 'FK_Iron', (w * 0.82, 0.012, 0.055), xf @ Matrix.Translation((-w * 0.05, -0.006, zl)), 'metal', R)
        cyl(g, 'FK_Iron', xf @ Vector((w * 0.36, -0.01, zl)), xf @ Vector((w * 0.36, -0.03, zl)), 0.022, 6, 'metal', R)

def shutter(g, R, w, h, xf, slot, boards=3):
    """A board shutter (local x across, z up) with a Z brace, front toward -y."""
    bw = w / boards
    for i in range(boards):
        box(g, slot, (bw - 0.01, 0.04, h - R.uniform(0, 0.04)), xf @ Matrix.Translation((-w / 2 + bw * (i + 0.5), 0, h / 2)) @ rot(0, 0, R.uniform(-0.01, 0.01)), 'wood', R, cham=0.008)
    for zz in (0.18, h - 0.18):
        box(g, slot, (w * 0.92, 0.035, 0.09), xf @ Matrix.Translation((0, 0.037, zz)), 'wood', R)
    box(g, 'FK_Iron', (0.16, 0.01, 0.035), xf @ Matrix.Translation((-w / 2 + 0.05, -0.024, h * 0.25)), 'metal', R)
    box(g, 'FK_Iron', (0.16, 0.01, 0.035), xf @ Matrix.Translation((-w / 2 + 0.05, -0.024, h * 0.75)), 'metal', R)

def window_fill(g, R, wall, o, depth=0.5):
    """Frame and dark glass set back in the reveal."""
    xf = wall.xf
    w = o.u1 - o.u0; h = o.z1 - o.z0
    box(g, 'FK_Glass', (w, 0.02, h), xf @ Matrix.Translation((o.uc, depth, o.z0 + h / 2)), 'glass', R, drop=(0, 1))
    for (cx, cz, sx, sz) in ((o.uc, o.z0 + 0.03, w, 0.06), (o.uc, o.z1 - 0.03, w, 0.06), (o.u0 + 0.03, o.z0 + h / 2, 0.06, h), (o.u1 - 0.03, o.z0 + h / 2, 0.06, h), (o.uc, o.z0 + h / 2, 0.05, h)):
        box(g, 'FK_Oak', (sx, 0.07, sz), xf @ Matrix.Translation((cx, depth - 0.04, cz)), 'wood', R)

def open_shutters(g, R, wall, o, slots, ang=(8, 12)):
    """Shutters hinged at the opening's edges and folded back against the wall, a few degrees off it (slots[i] None = missing)."""
    w = (o.u1 - o.u0) / 2 + 0.06; h = o.z1 - o.z0 - 0.04
    for side, sl in enumerate(slots):
        if sl is None:
            continue
        th = math.radians(ang[side]) * (1 if side == 0 else -1)
        hu = o.u0 if side == 0 else o.u1
        xf = wall.xf @ Matrix.Translation((hu, -0.08, o.z0 + 0.02)) @ Matrix.Rotation(th, 4, 'Z') @ Matrix.Translation(((-w / 2) if side == 0 else (w / 2), 0, 0))
        shutter(g, R, w, h, xf, sl)

def closed_shutters(g, R, wall, o, slot):
    w = o.u1 - o.u0 - 0.02; h = o.z1 - o.z0 - 0.02
    shutter(g, R, w, h, wall.xf @ Matrix.Translation((o.uc, 0.18, o.z0 + 0.01)), slot, boards=4)

def board_patch(g, R, wall, p, slots, vertical=True):
    """Salvaged boards nailed over a breach, with two battens and nail heads."""
    u0, u1 = p.uc - p.ru * 1.25, p.uc + p.ru * 1.25; z0, z1 = p.zc - p.rz * 1.2, p.zc + p.rz * 1.2
    u = u0; i = 0
    while u < u1:
        bw = R.uniform(0.16, 0.28)
        ztop = z1 - R.uniform(0, 0.25); zbot = z0 + R.uniform(0, 0.2)
        sl = slots[(i * 7 + R.randrange(3)) % len(slots)]
        xf = wall.xf @ Matrix.Translation((u + bw / 2, -0.07, (ztop + zbot) / 2)) @ rot(0, 0, R.uniform(-0.03, 0.03))
        box(g, sl, (bw - 0.012, 0.04, ztop - zbot), xf, 'wood', R, cham=0.01)
        u += bw; i += 1
    for zz in (z0 + 0.25, z1 - 0.3):
        xf = wall.xf @ Matrix.Translation(((u0 + u) / 2, -0.11, zz)) @ rot(0, 0, R.uniform(-0.06, 0.06))
        box(g, 'HH_PlankGrey', (u - u0 + 0.1, 0.04, 0.12), xf, 'wood', R, cham=0.01)

def bench(g, R, xf):
    for xx in (-0.62, 0.62):
        stone(g, 'HH_OldStone', (0.26, 0.36, 0.42), xf @ Matrix.Translation((xx, 0, 0.21)), R, soft=0.25, tag='stone')
    for k, yy in enumerate((-0.09, 0.09)):
        box(g, 'FK_OakDoor' if k else 'FK_Oak', (1.75, 0.17, 0.07), xf @ Matrix.Translation((0, yy, 0.455)) @ rot(R.uniform(-0.01, 0.01), 0, R.uniform(-0.01, 0.01)), 'seat', R, cham=0.015)

def barrel(g, R, p, r=0.38, h=0.9):
    n = 14
    for i in range(n):
        a0 = 2 * math.pi * i / n; a1 = 2 * math.pi * (i + 1) / n
        vs = []
        for zz, rr in ((0, r * 0.88), (h * 0.5, r), (h, r * 0.88)):
            for a in (a0, a1):
                vs.append(Vector(p) + Vector((math.cos(a) * rr, math.sin(a) * rr, zz)))
        g.add(vs, [(0, 1, 3, 2), (2, 3, 5, 4)], 'FK_Oak', 'wood', R)
    g.add([Vector(p) + Vector((math.cos(2 * math.pi * i / n) * r * 0.85, math.sin(2 * math.pi * i / n) * r * 0.85, h - 0.06)) for i in range(n)], [tuple(range(n))], 'FK_Glass', 'glass')
    for zz, rr in ((0.12, r * 0.93), (h - 0.12, r * 0.93), (h * 0.5 - 0.1, r * 1.005), (h * 0.5 + 0.1, r * 1.005)):
        cyl(g, 'FK_Iron', Vector(p) + Vector((0, 0, zz - 0.025)), Vector(p) + Vector((0, 0, zz + 0.025)), rr + 0.012, n, 'metal', R, caps=False)

def woodpile(g, R, x0, x1, y0, y1, z0, h, axis='y'):
    """Split logs stacked between x0..x1 (rows along x), log length along y (y0..y1); end faces in light oak."""
    zz = z0 + 0.08; row = 0
    while zz < z0 + h:
        x = x0 + (0.06 if row % 2 else 0.0)
        while x < x1 - 0.08:
            r = R.uniform(0.07, 0.11)
            ln = y1 - y0 - R.uniform(0, 0.12)
            ya = y0 + R.uniform(0, 0.06)
            cyl(g, 'FK_Oak', (x + r, ya, zz + r * 0.9), (x + r + R.uniform(-0.02, 0.02), ya + ln, zz + r * 0.9 + R.uniform(-0.02, 0.02)), r, 6, 'wood', R, cap_slot='FK_OakDoor')
            x += 2 * r + 0.005
        zz += 0.17; row += 1

def rope(g, a, b, sag, n=10, r=0.012):
    a = Vector(a); b = Vector(b); pts = []
    for i in range(n + 1):
        t = i / n; p = a.lerp(b, t); p.z -= sag * 4 * t * (1 - t); pts.append(p)
    for i in range(n):
        cyl(g, 'HH_Rope', pts[i], pts[i + 1], r, 4, 'rope', None, caps=False)
    return pts

def cloth(g, R, p_top_left, width, drop, along, slot, sway=0.04):
    a = Vector(p_top_left); al = Vector(along).normalized(); nr = al.cross(Vector((0, 0, 1))).normalized()
    ph = R.random() * 6
    def P(u, v):
        return a + al * (u * width) + Vector((0, 0, -drop * v * (1 + 0.06 * math.sin(u * 7 + ph)))) + nr * (sway * v * math.sin(u * 4 + ph) + 0.03 * math.sin(v * 3 + u * 9))
    quad_grid(g, slot, P, 4, 3, 'cloth', R, two=True)
    for u in (0.1, 0.9):   # pegs
        box(g, 'FK_OakDoor', (0.02, 0.025, 0.08), Matrix.Translation(a + al * (u * width) + Vector((0, 0, 0.0))), 'wood', R)

def tarp(g, R, S, x0, x1, s0, s1, slot='HH_Canvas'):
    """A canvas tarp lying on a roof slope (S(x, s, off) from slate_roof), billowing slightly, edges draped, roped and weighted."""
    ph = R.random() * 5
    def P(u, v):
        x = x0 + (x1 - x0) * u; s = s0 + (s1 - s0) * v
        lift = 0.10 + 0.05 * math.sin(u * 5 + ph) * math.sin(v * 4) - 0.08 * (abs(u - 0.5) * 2) ** 4 - 0.05 * (abs(v - 0.5) * 2) ** 4
        return S(x, s, lift)
    quad_grid(g, slot, P, 8, 6, 'cloth', R, two=True)
    for u in (0.2, 0.55, 0.85):   # weight stones on the tarp
        p = P(u, 0.08)
        stone(g, 'HH_OldStone', (0.28, 0.24, 0.16), Matrix.Translation(p + Vector((0, 0, 0.06))) @ rot(R.uniform(0, 3)), R, soft=0.35)
    for v in (0.3, 0.75):
        rope(g, P(0.0, v), P(1.0, v) , -0.02, 8, 0.01)

# ----------------------------------------------------------------------------- the cistern house
HX, HY = 4.5, 3.75
T = 0.75
EAVE = 4.4                     # wall head (S and N walls)
PITCH = math.radians(40)
RIDGE = EAVE + HY * math.tan(PITCH)   # about 7.55

def gable_top(u, L):
    return EAVE + (L / 2 - abs(u - L / 2)) * math.tan(PITCH)

def build_cistern():
    R = random.Random(0x4E57)
    g = Geo('HeroesHome_Cistern'); dec = Geo('Decals'); glow = Geo('Glow')
    # --- walls (outer faces): S (y=-HY, U +x, N -y), E (x=+HX, U +y), N (y=+HY, U -x), W (x=-HX, U -y)
    door = Opening(HX - 0.8, HX + 0.8, 0.0, 2.75, 'door', arch=True)
    sw = Opening(HX + 1.9, HX + 2.75, 1.25, 2.30)
    s_up = Opening(HX + 1.55, HX + 2.25, 3.05, 3.75)                    # small upper light (blue single shutter)
    patch_s = Patch(HX + 3.7, 3.35, 0.45, 0.42, 1.3)                       # boarded breach high on the right
    S_ = Wall(g, (-HX, -HY, 0), (1, 0, 0), (0, -1, 0), 2 * HX, lambda u: EAVE, random.Random(11), [door, sw, s_up], [patch_s])
    ew = Opening(2.1, 2.9, 1.3, 2.25)
    E_ = Wall(g, (HX, -HY, 0), (0, 1, 0), (1, 0, 0), 2 * HY, lambda u: gable_top(u, 2 * HY), random.Random(12), [ew], [])
    nw = Opening(5.2, 5.9, 1.5, 2.3)
    patch_n = Patch(2.6, 2.4, 0.55, 0.5, 4.1)
    N_ = Wall(g, (HX, HY, 0), (-1, 0, 0), (0, 1, 0), 2 * HX, lambda u: EAVE, random.Random(13), [nw], [patch_n])
    ww = Opening(4.3, 5.15, 1.3, 2.25)
    patch_w = Patch(2.1, 4.6, 0.6, 0.48, 2.2)                              # an old breach in the west gable, boarded
    W_ = Wall(g, (-HX, HY, 0), (0, -1, 0), (-1, 0, 0), 2 * HY, lambda u: gable_top(u, 2 * HY), random.Random(14), [ww], [patch_w])
    walls = [S_, E_, N_, W_]
    for w in walls:
        w.build(); w.core(); w.dress()
    # inner liner (dark lime-washed inside), floor, ceiling: so the open door and the windows never show sky or holes
    ti = T
    for w in walls:
        inner_liner(g, w, ti, EAVE)
    # reveal walls of the door (the jamb stones cover 0.77 m of the 0.75 m wall)
    box(g, 'FK_Oak', (2 * HX - 2 * ti, 2 * HY - 2 * ti, 0.06), Matrix.Translation((0, 0, EAVE - 0.4)), 'wood', R, drop=(1,))
    for i in range(5):   # ceiling beams
        box(g, 'FK_Oak', (0.24, 2 * HY - 2 * ti, 0.26), Matrix.Translation((-HX + ti + 0.6 + i * 1.75, 0, EAVE - 0.55)), 'wood', R, drop=(1,))
    # floor with the stair well cut out (stair head at STAIR, going down to the north)
    sx0, sx1, sy0, sy1 = STAIR[0] - 0.6, STAIR[0] + 0.6, STAIR[1] - 0.9, HY - ti
    fx0, fx1, fy0, fy1 = -HX + ti, HX - ti, -HY + ti, HY - ti
    for (a, b, c, d) in ((fx0, sx0, fy0, fy1), (sx1, fx1, fy0, fy1), (sx0, sx1, fy0, sy0)):
        xx = 0.0; xs = []
        # flagstones: a few big slabs
        x = a
        while x < b - 0.05:
            xb = min(x + R.uniform(0.6, 1.0), b); y = c
            while y < d - 0.05:
                yb = min(y + R.uniform(0.5, 0.9), d)
                stone(g, 'FK_Sandstone', (xb - x - 0.03, yb - y - 0.03, 0.12), Matrix.Translation(((x + xb) / 2, (y + yb) / 2, 0.06)), R, soft=0.15, bulge=0.0, tag='floor')
                y = yb
            x = xb
    # the stair: an arched stone doorway at the stair head facing the front door (south), steps down to the north
    sd = Opening(STAIR[0] - 0.55, STAIR[0] + 0.55, 0.12, 2.25, 'door', arch=True)
    sw_ = Wall(g, (STAIR[0] - 1.3, sy0, 0.12), (1, 0, 0), (0, -1, 0), 2.6, lambda u: EAVE - 0.6, random.Random(21), [], [], q0=False, q1=False, plinth=0.3)
    sw_.ops = [Opening(1.3 - 0.55, 1.3 + 0.55, 0.12, 2.25, 'door', arch=True)]
    sw_.dress(depth=0.45)
    # stone face around the arch (a short screen wall over the stair, 2.6 m wide)
    sw_.base = 0.12; sw_.build()
    box(g, 'FK_PlinthJoint', (2.6, 0.02, EAVE - 0.72), Matrix.Translation((STAIR[0], sy0 + 0.47, 0.12 + (EAVE - 0.72) / 2)), 'core', R, drop=(0, 1))
    # steps going down (and a side wall each side) to a warm-lit landing
    nst = 8
    for i in range(nst):
        zt = 0.12 - 0.21 * (i + 1); y0 = sy0 + 0.47 + i * 0.28
        stone(g, 'FK_Sandstone', (1.1, 0.30, 0.22), Matrix.Translation((STAIR[0], y0 + 0.14, zt + 0.11 - 0.0)), R, soft=0.15, bulge=-0.01, tag='step')
    for xx in (STAIR[0] - 0.62, STAIR[0] + 0.62):
        box(g, 'FK_PlinthJoint', (0.04, 2.5, 2.4), Matrix.Translation((xx, sy0 + 1.6, -0.9)), 'core', R)
    # warm light below: a glowing back wall + floor at the stair foot
    box(glow, 'HH_Glow', (1.2, 0.04, 1.9), Matrix.Translation((STAIR[0], sy0 + 0.47 + nst * 0.28 + 0.15, -1.55 + 0.95)), 'glow', R)
    box(glow, 'HH_Glow', (1.2, 1.0, 0.04), Matrix.Translation((STAIR[0], sy0 + 0.47 + nst * 0.28 - 0.3, -1.62)), 'glow', R)
    # the little arched stair door, standing open against the screen wall
    xf = Matrix.Translation((STAIR[0] + 0.58, sy0 - 0.02, 0.12)) @ Matrix.Rotation(math.radians(-100), 4, 'Z') @ Matrix.Translation((-0.52, 0, 0))
    door_leaf(g, R, 1.04, 2.1, xf, ['FK_OakDoor', 'FK_Oak'], arch_r=0.52)
    # --- the front door: mismatched planks, ajar (inward), salvaged painted boards among the oak
    xf = Matrix.Translation((door.u0 - HX + 0.05, -HY + 0.55, 0.14)) @ Matrix.Rotation(math.radians(80), 4, 'Z') @ Matrix.Translation((1.5 / 2, 0, 0))   # hinged west, swung in
    door_leaf(g, R, 1.5, 2.6, xf, ['FK_Oak', 'FK_OakDoor', 'HH_PaintOchre', 'FK_Shutter', 'HH_PlankGrey'], arch_r=0.75)
    # windows: frames + glass, mismatched shutters
    window_fill(g, R, S_, sw, 0.45); open_shutters(g, R, S_, sw, ['FK_Shutter', 'HH_PaintRed'])
    window_fill(g, R, S_, s_up, 0.45); open_shutters(g, R, S_, s_up, [None, 'HH_PaintBlue'])
    window_fill(g, R, E_, ew, 0.45); closed_shutters(g, R, E_, ew, 'HH_PlankGrey')
    window_fill(g, R, W_, ww, 0.45); open_shutters(g, R, W_, ww, ['HH_PaintBlue', 'FK_Oak'], ang=(6, 35))
    # north window stuffed with canvas
    quad_grid(g, 'HH_Canvas', lambda u, v: N_.P(nw.u0 + (nw.u1 - nw.u0) * u, nw.z0 + (nw.z1 - nw.z0) * v, -0.25 + 0.06 * math.sin(u * 3.1) * math.sin(v * 3.1)), 3, 3, 'cloth', R, two=True)
    # boarded breaches
    board_patch(g, R, S_, patch_s, ['FK_Oak', 'HH_PlankGrey', 'FK_OakDoor', 'HH_PaintRed'])
    board_patch(g, R, W_, patch_w, ['HH_PlankGrey', 'FK_Oak', 'HH_PaintOchre', 'FK_OakDoor'])
    board_patch(g, R, N_, patch_n, ['FK_Oak', 'HH_PlankGrey'])
    # --- parapet gables: coping along the rake, kneelers at the foot, a finial at the apex
    for gx, sgn in ((HX, 1), (-HX, -1)):
        for side in (-1, 1):
            n = 8
            for i in range(n):
                t0, t1 = i / n, (i + 1) / n
                ya = side * (HY + 0.15) * (1 - t0); yb = side * (HY + 0.15) * (1 - t1)
                za = EAVE + (HY + 0.15 - abs(ya)) * math.tan(PITCH) + 0.1; zb = EAVE + (HY + 0.15 - abs(yb)) * math.tan(PITCH) + 0.1
                c = Vector((gx - sgn * T / 2 + sgn * 0.06, (ya + yb) / 2, (za + zb) / 2 + 0.08))
                L = math.hypot(yb - ya, zb - za)
                ang = math.atan2(zb - za, yb - ya)
                xf = Matrix.Translation(c) @ Matrix.Rotation(ang, 4, 'X') @ Matrix.Rotation(math.radians(90), 4, 'Z')
                stone(g, 'FK_Sandstone', (T + 0.16, L - 0.02, 0.24), xf, R, soft=0.18, bulge=0.01)
            stone(g, 'FK_Sandstone', (T + 0.22, 0.62, 0.52), Matrix.Translation((gx - sgn * T / 2 + sgn * 0.06, side * (HY - 0.1), EAVE + 0.2)), R, soft=0.2)
        stone(g, 'FK_Sandstone', (0.5, 0.5, 0.5), Matrix.Translation((gx - sgn * T / 2 + sgn * 0.06, 0, RIDGE + 0.42)) @ Matrix.Rotation(math.radians(45), 4, 'Z'), R, soft=0.3)
    # gable wall tops under the coping (fill the stepped course ends)
    for gx, sgn in ((HX, 1), (-HX, -1)):
        for side in (-1, 1):
            vs = [Vector((gx + sgn * 0.0, side * HY, EAVE)), Vector((gx, 0, RIDGE)), Vector((gx - sgn * T, 0, RIDGE)), Vector((gx - sgn * T, side * HY, EAVE))]
            fs = [(0, 1, 2, 3)]
            fn = (vs[1] - vs[0]).cross(vs[3] - vs[0])
            g.add(vs, fs if fn.z > 0 else [(3, 2, 1, 0)], 'FK_PlinthJoint', 'core')
    # S and N wall heads: a sandstone string course under the eave
    for yy in (-HY, HY):
        x = -HX
        while x < HX - 0.05:
            xb = min(x + R.uniform(0.7, 1.1), HX)
            stone(g, 'FK_Sandstone', (xb - x - 0.02, T + 0.12, 0.22), Matrix.Translation(((x + xb) / 2, yy - math.copysign(T / 2 - 0.06, yy), EAVE + 0.06)), R, soft=0.15, bulge=0.0)
            x = xb
    # --- roof: two stone-slate slopes between the parapets; tarp over missing slates (south), boards (north)
    rx0, rx1 = -HX + T - 0.02, HX - T + 0.02
    sag = 0.18
    tarp_box = (0.6, 2.6, 1.2, 2.9)            # x0, x1, s0, s1 on the south slope
    planks_box = (-2.9, -1.3, 0.9, 2.6)       # on the north slope
    def skip_s(x, s):
        return tarp_box[0] + 0.15 < x < tarp_box[1] - 0.15 and tarp_box[2] + 0.15 < s < tarp_box[3] - 0.15
    def skip_n(x, s):
        return planks_box[0] + 0.1 < x < planks_box[1] - 0.1 and planks_box[2] + 0.1 < s < planks_box[3] - 0.1
    ov = 0.45
    ye = HY + ov; ze = EAVE + 0.15 - ov * math.tan(PITCH)
    S_s, Ls, d_s, n_s = slate_roof(g, R, rx0, rx1, -ye, 0.0, ze, RIDGE + 0.12, -1, sag, skip_s)
    S_n, Ln, d_n, n_n = slate_roof(g, R, rx0, rx1, ye, 0.0, ze, RIDGE + 0.12, 1, sag, skip_n)
    ridge_cap(g, R, rx0, rx1, 0.0, RIDGE + 0.18, sag)
    tarp(g, R, S_s, *tarp_box)
    # north slope board patch: boards laid down the slope
    x = planks_box[0]
    while x < planks_box[1]:
        bw = R.uniform(0.2, 0.3)
        plank(g, R.choice(['FK_Oak', 'HH_PlankGrey', 'FK_OakDoor']), S_n(x + bw / 2, planks_box[2], 0.06), S_n(x + bw / 2, planks_box[3], 0.06), bw - 0.015, 0.035, n_n, R)
        x += bw
    # timber gutter along the south eave, falling to the water butt at the SE corner
    gy = -ye - 0.12; gz0 = ze - 0.18
    for (xa, xb) in ((rx0 - 0.1, 0.0), (0.0, HX + 0.05)):
        za = gz0 + 0.05 * (1 - (xa - rx0) / (HX - rx0)); zb = gz0 + 0.05 * (1 - (xb - rx0) / (HX - rx0))
        plank(g, 'FK_Oak', (xa, gy, za), (xb, gy, zb), 0.16, 0.03, (0, 0, 1), R)
        plank(g, 'FK_Oak', (xa, gy - 0.07, za + 0.06), (xb, gy - 0.07, zb + 0.06), 0.12, 0.025, (0, 1, 0), R)
    for xx in (-2.5, 0.6, 3.4):
        box(g, 'FK_Iron', (0.03, 0.4, 0.03), Matrix.Translation((xx, gy + 0.12, gz0 + 0.02)), 'metal', R)
    cyl(g, 'FK_Oak', (HX - 0.05, gy, gz0 - 0.02), (HX - 0.3, -HY - 0.45, 1.05), 0.06, 6, 'wood', R)   # a wooden downspout
    barrel(g, R, (HX - 0.3, -HY - 0.45, 0.0))
    # chimney on the west gable apex
    cx = -HX + T / 2 - 0.05; ch0 = RIDGE - 0.6; ch1 = RIDGE + 1.6
    z = ch0; k = 0
    while z < ch1:
        h = R.uniform(0.28, 0.36)
        for (dx, dy, sx, sy) in ((0, -0.42, 1.0, 0.18), (0, 0.42, 1.0, 0.18), (-0.41, 0, 0.18, 0.66), (0.41, 0, 0.18, 0.66)):
            stone(g, 'HH_OldStone' if k % 3 else 'FK_Sandstone', (sx + 0.02, sy + 0.02, h - 0.02), Matrix.Translation((cx + dx, dy, z + h / 2)) @ rot(R.uniform(-0.02, 0.02)), R, soft=0.18)
        z += h; k += 1
    stone(g, 'FK_Sandstone', (1.22, 1.08, 0.16), Matrix.Translation((cx, 0, z + 0.08)), R, soft=0.15)
    box(g, 'HH_Dark', (0.66, 0.5, 0.05), Matrix.Translation((cx, 0, z + 0.02)), 'soot', R)
    for dy in (-0.2, 0.22):
        cyl(g, 'FK_RoofTile', (cx, dy, z + 0.16), (cx, dy, z + 0.6 + (0.08 if dy > 0 else 0)), 0.13, 8, 'pot', R, caps=False, r2=0.10)
    # --- Wren's list: a limewashed panel on the south wall, left of the door, the names painted on it
    lu0, lu1, lz0, lz1 = 0.35, 3.0, 0.82, 3.35
    def lim(u, v):
        uu = lu0 + (lu1 - lu0) * u; zz = lz0 + (lz1 - lz0) * v
        e = min(u, 1 - u, v, 1 - v)
        rag = (0.045 * math.sin(u * 9 + v * 13) + 0.02 * math.sin(v * 21 + u * 5)) * max(0.0, 1 - e * 8)
        return S_.P(uu + rag, zz, 0.05 + 0.006 * math.sin(u * 17) * math.sin(v * 13))
    quad_grid(g, 'FK_Plaster', lim, 20, 20, 'plaster', R)
    pad = 0.12
    quad_grid(dec, 'HH_List', lambda u, v: S_.P(lu0 + pad + (lu1 - lu0 - 2 * pad) * u, lz0 + pad + (lz1 - lz0 - 2 * pad) * v, 0.064), 1, 1, 'decal')
    # a paint pot and brush on a ledge stone under the list (Wren keeps it up to date)
    cyl(g, 'FK_Iron', S_.P(2.55, 0.0, -0.32) + Vector((0, 0, 0.0)), S_.P(2.55, 0.0, -0.32) + Vector((0, 0, 0.2)), 0.09, 8, 'metal', R)
    cyl(g, 'FK_Oak', S_.P(2.55, 0.18, -0.30), S_.P(2.62, 0.42, -0.36), 0.012, 5, 'wood', R)
    # drips under the window sills
    for (wall, o) in ((S_, sw), (E_, ew), (W_, ww)):
        quad_grid(dec, 'FK_Decal_drip', lambda u, v, wall=wall, o=o: wall.P(o.u0 - 0.1 + (o.u1 - o.u0 + 0.2) * u, o.z0 - 1.2 + 1.05 * v, 0.045), 1, 1, 'decal')
    # --- dressing
    bench(g, R, Matrix.Translation((1.15, -HY - 0.36, 0.0)))
    cyl(g, 'FK_Oak', (2.1, -HY - 0.38, 0.0), (2.1, -HY - 0.38, 0.34), 0.15, 10, 'wood', R)   # a bucket by the bench
    cyl(g, 'FK_Iron', (2.1, -HY - 0.38, 0.27), (2.1, -HY - 0.38, 0.31), 0.155, 10, 'metal', R, caps=False)
    # wood pile against the west wall under a lean-to of salvaged boards
    woodpile(g, R, -HX - 0.62, -HX - 0.05, -0.2, 2.4, 0.0, 1.25)
    for (yy) in (-0.35, 1.1, 2.55):
        box(g, 'FK_Oak', (0.12, 0.12, 1.6), Matrix.Translation((-HX - 0.78, yy, 0.8)) @ rot(0, 0, R.uniform(-0.03, 0.03)), 'wood', R, cham=0.015)
    y = -0.5
    while y < 2.7:
        bw = R.uniform(0.2, 0.3)
        plank(g, R.choice(['FK_Oak', 'HH_PlankGrey']), (-HX - 0.95, y + bw / 2, 1.55), (-HX - 0.02, y + bw / 2, 1.85), bw - 0.012, 0.03, (0, 1, 0), R)
        y += bw
    # washing line: two posts in the west yard, a sagging rope, the heroes' washing
    p1 = Vector((-HX - 1.9, -2.9, 0)); p2 = Vector((-HX - 1.6, 2.9, 0))
    for p in (p1, p2):
        cyl(g, 'FK_Oak', p, p + Vector((0.03, 0, 2.25)), 0.06, 7, 'wood', R)
        box(g, 'FK_Oak', (0.5, 0.07, 0.07), Matrix.Translation(p + Vector((0.03, 0, 2.1))), 'wood', R)
    pts = rope(g, p1 + Vector((0.03, 0, 2.08)), p2 + Vector((0.03, 0, 2.08)), 0.22, 12)
    along = (p2 - p1).normalized()
    items = [(0.08, 0.55, 0.65, 'HH_ClothLinen'), (0.20, 0.42, 0.5, 'HH_ClothMadder'), (0.33, 0.9, 0.95, 'HH_ClothLinen'), (0.52, 0.36, 0.42, 'HH_ClothBlue'),
             (0.64, 0.5, 0.6, 'HH_Canvas'), (0.76, 0.3, 0.32, 'HH_ClothMadder'), (0.86, 0.45, 0.55, 'HH_ClothBlue')]
    for t, wd, dr, sl in items:
        i = int(t * 12); pa = pts[i].lerp(pts[i + 1], t * 12 - i)
        cloth(g, R, pa + Vector((0, 0, -0.01)), wd, dr, along, sl)
    return g, dec, glow

# ----------------------------------------------------------------------------- the bell-loft annex
AX, AY = 2.75, 2.5
A_STONE = 3.0
A_LOFT = 5.2
A_PITCH = math.radians(45)
A_RIDGE = A_LOFT + AX * math.tan(A_PITCH)

def build_annex():
    R = random.Random(0xA11E)
    g = Geo('HeroesHome_Annex'); dec = Geo('Decals'); glow = Geo('Glow')
    door = Opening(AY - 0.6, AY + 0.6, 0.0, 2.35, 'door')
    S_win = Opening(AX - 0.35 + 0.6, AX + 0.35 + 0.6, 1.2, 2.1)
    patch_e = Patch(4.1, 1.9, 0.4, 0.38, 0.7)
    S_ = Wall(g, (-AX, -AY, 0), (1, 0, 0), (0, -1, 0), 2 * AX, lambda u: A_STONE, random.Random(31), [S_win], [], course=(0.30, 0.44))
    E_ = Wall(g, (AX, -AY, 0), (0, 1, 0), (1, 0, 0), 2 * AY, lambda u: A_STONE, random.Random(32), [door], [patch_e], course=(0.30, 0.44))
    N_win = Opening(2.4, 3.0, 1.4, 2.1)
    N_ = Wall(g, (AX, AY, 0), (-1, 0, 0), (0, 1, 0), 2 * AX, lambda u: A_STONE, random.Random(33), [N_win], [], course=(0.30, 0.44))
    W_ = Wall(g, (-AX, AY, 0), (0, -1, 0), (-1, 0, 0), 2 * AY, lambda u: A_STONE, random.Random(34), [], [], course=(0.30, 0.44))
    for w in (S_, E_, N_, W_):
        w.build(); w.core(); w.dress(depth=0.6)
    ti = 0.6
    for w in (S_, E_, N_, W_):
        inner_liner(g, w, ti, A_STONE)
    box(g, 'FK_Sandstone', (2 * AX - 2 * ti, 2 * AY - 2 * ti, 0.1), Matrix.Translation((0, 0, 0.05)), 'floor', R)
    # wall head: a timber sole plate on the stone, the loft frame (corner posts, mid posts, top plates)
    lean = math.radians(1.3)
    LX, LY = AX - 0.12, AY - 0.12
    def Lp(x, y, z):   # loft point, leaning a touch to the east
        return Vector((x + (z - A_STONE) * math.tan(lean), y, z))
    for (a, b) in (((-LX, -LY), (LX, -LY)), ((LX, -LY), (LX, LY)), ((LX, LY), (-LX, LY)), ((-LX, LY), (-LX, -LY))):
        plank(g, 'FK_Oak', Lp(a[0], a[1], A_STONE + 0.12), Lp(b[0], b[1], A_STONE + 0.12), 0.24, 0.26, (0, 0, 1), R)
        plank(g, 'FK_Oak', Lp(a[0], a[1], A_LOFT - 0.1), Lp(b[0], b[1], A_LOFT - 0.1), 0.24, 0.26, (0, 0, 1), R)
    for (x, y) in ((-LX, -LY), (LX, -LY), (LX, LY), (-LX, LY), (0, -LY), (0, LY), (LX, 0), (-LX, 0)):
        plank(g, 'FK_Oak', Lp(x, y, A_STONE + 0.2), Lp(x, y, A_LOFT - 0.2), 0.22, 0.22, (1, 0, 0) if abs(y) > 0.1 else (0, 1, 0), R)
    # board cladding between: mismatched vertical boards (some painted, salvaged), the south gable boarded to the roof line
    def clad(u0, u1, P, top, slots, skip=lambda u, z: False):
        u = u0
        while u < u1 - 0.03:
            bw = min(R.uniform(0.17, 0.27), u1 - u); uc = u + bw / 2
            z0 = A_STONE + 0.25; z1 = top(uc) - R.uniform(0.0, 0.05)
            if not skip(uc, (z0 + z1) / 2):
                sl = slots[R.randrange(len(slots))]
                a, b = P(uc, z0), P(uc, z1)
                plank(g, sl, a, b, bw - 0.012, 0.035, (P(uc + 1, z0) - a), R, wob=0.012)
            u += bw
    eave_top = lambda u: A_LOFT - 0.22
    gable = lambda u: A_LOFT + (AX - abs(u)) * math.tan(A_PITCH) - 0.15
    boards = ['FK_Oak', 'FK_Oak', 'HH_PlankGrey', 'FK_OakDoor', 'HH_PlankGrey', 'HH_PaintBlue', 'FK_Shutter']
    hole = lambda u, z: -0.55 < u < 0.55 and A_LOFT - 0.1 < z < A_LOFT + 0.9
    clad(-AX + 0.02, AX - 0.02, lambda u, z: Lp(u, -AY - 0.03, z), gable, boards, hole)                  # south (gable, to the square)
    clad(-AX + 0.02, AX - 0.02, lambda u, z: Lp(-u, AY + 0.03, z), gable, boards)                         # north gable
    clad(-AY + 0.02, AY - 0.02, lambda u, z: Lp(AX + 0.03, u, z), eave_top, boards)                        # east
    clad(-AY + 0.02, AY - 0.02, lambda u, z: Lp(-AX - 0.03, -u, z), eave_top, ['FK_Oak', 'HH_PlankGrey'])  # west (against the tower)
    # the south gable opening, patched with canvas, a small window with odd shutters lower in the loft
    quad_grid(g, 'HH_Canvas', lambda u, v: Lp(-0.6 + 1.2 * u, -AY - 0.02 + 0.05 * math.sin(u * 3.14) * math.sin(v * 3.14), A_LOFT - 0.15 + 1.1 * v), 4, 4, 'cloth', R, two=True)
    box(g, 'HH_Dark', (2 * AX - 0.3, 2 * AY - 0.3, A_LOFT - A_STONE), Matrix.Translation((0, 0, (A_STONE + A_LOFT) / 2)), 'core', R)   # loft interior (dark)
    # loft window (east): a square hole with a single red shutter, frame
    lw = (-0.9, -0.2, 3.75, 4.45)
    for (cy, cz, sy, sz) in (((lw[0] + lw[1]) / 2, lw[2], 0.8, 0.08), ((lw[0] + lw[1]) / 2, lw[3], 0.8, 0.08), (lw[0], (lw[2] + lw[3]) / 2, 0.08, 0.78), (lw[1], (lw[2] + lw[3]) / 2, 0.08, 0.78)):
        box(g, 'FK_Oak', (0.12, sy, sz), Matrix.Translation(Lp(AX + 0.07, cy, cz)), 'wood', R)
    box(g, 'FK_Glass', (0.02, 0.62, 0.62), Matrix.Translation(Lp(AX + 0.06, (lw[0] + lw[1]) / 2, (lw[2] + lw[3]) / 2)), 'glass', R)
    shutter(g, R, 0.7, 0.72, Matrix.Translation(Lp(AX + 0.1, lw[0] - 0.02, lw[2])) @ Matrix.Rotation(math.radians(-90 - 15), 4, 'Z') @ Matrix.Translation((-0.35, 0, 0)), 'HH_PaintRed')
    # roof: west slope stone slates, east slope boards (heroes' repair) with a tarp; ridge along y
    ov = 0.4
    xe = AX + ov; ze = A_LOFT - ov * math.tan(A_PITCH)
    ry0, ry1 = -AY - 0.35, AY + 0.35
    def slope_pts(side):
        return lambda y, s, off=0.0: None
    # slate_roof works along x; build in a rotated frame (x->y): generate in a temp Geo, then rotate 90 deg about z
    tmp = Geo('tmp')
    S_w, Lw, dw, nwv = slate_roof(tmp, R, ry0, ry1, -xe, 0.0, ze, A_RIDGE + 0.1, -1, 0.10, lambda x, s: False, course0=0.30, course1=0.20)
    rz = Matrix.Rotation(math.radians(-90), 4, 'Z')   # slope's -y (west after rotation)... x -> -y
    rz = Matrix(((0, -1, 0, 0), (1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))   # (x, y) -> (-y, x): eave at -xe goes to x = +xe? fixed below
    rz = Matrix(((0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))    # mirror swap x<->y: eave y=-xe -> x=-xe (west), ridge along y
    o = len(g.v)
    for f, sl, tg, uo in zip(tmp.f, tmp.slot, tmp.tag, tmp.uvo):
        pass
    vs = [rz @ v for v in tmp.v]
    fs = [tuple(reversed(f)) for f in tmp.f]   # the swap mirrors: flip the winding
    base = len(g.v); g.v += vs
    for f, sl, tg, uo in zip(fs, tmp.slot, tmp.tag, tmp.uvo):
        g.f.append(tuple(i + base for i in f)); g.slot.append(sl); g.tag.append(tg); g.uvo.append(uo)
    Sw = lambda yy, s, off=0.0: rz @ S_w(yy, s, off)
    # east slope: deck + boards running down the slope + battens + tarp
    run = Vector((-xe, 0, A_RIDGE + 0.1 - ze)); Le = run.length; de = run.normalized(); ne = Vector((de.z, 0, -de.x)).normalized()
    if ne.z < 0:
        ne = -ne
    def Se(yy, s, off=0.0):
        t = (yy - ry0) / (ry1 - ry0)
        return Vector((xe, yy, ze)) + de * s + ne * (off - 0.10 * 4 * t * (1 - t) * (s / Le))
    quad_grid(g, 'FK_Oak', lambda u, v: Se(ry0 + (ry1 - ry0) * (1 - u), Le * v, -0.02), 8, 3, 'wood', R)
    quad_grid(g, 'FK_Oak', lambda u, v: Se(ry0 + (ry1 - ry0) * u, Le * v, -0.12), 8, 3, 'wood', R)
    yy = ry0
    while yy < ry1 - 0.05:
        bw = min(R.uniform(0.2, 0.3), ry1 - yy)
        plank(g, R.choice(['FK_Oak', 'HH_PlankGrey', 'FK_Oak', 'FK_OakDoor']), Se(yy + bw / 2, -0.05, 0.03), Se(yy + bw / 2, Le - 0.05, 0.03), bw - 0.012, 0.035, ne, R)
        yy += bw
    for s in (0.4, Le * 0.5, Le - 0.35):
        plank(g, 'HH_PlankGrey', Se(ry0 + 0.1, s, 0.075), Se(ry1 - 0.1, s, 0.075), 0.1, 0.04, ne, R)
    def Se_x(x, s, off=0.0):   # tarp(): S(x, s, off) with x along the ridge
        return Se(x, s, off + 0.06)
    tarp(g, R, Se_x, -1.2, 0.6, 1.0, 2.6)
    ridge = Matrix(((0, 1, 0, 0), (1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
    tmp2 = Geo('tmp2'); ridge_cap(tmp2, R, ry0, ry1, 0.0, A_RIDGE + 0.16, 0.10)
    base = len(g.v); g.v += [ridge @ v for v in tmp2.v]
    for f, sl, tg, uo in zip(tmp2.f, tmp2.slot, tmp2.tag, tmp2.uvo):
        g.f.append(tuple(i + base for i in reversed(f))); g.slot.append(sl); g.tag.append(tg); g.uvo.append(uo)
    # barge boards on the gables
    for yy, sg in ((ry0 + 0.02, -1), (ry1 - 0.02, 1)):
        for side in (-1, 1):
            a = Vector((side * xe, yy, ze - 0.05)); b = Vector((0, yy, A_RIDGE + 0.05))
            plank(g, 'FK_Oak', a, b, 0.24, 0.05, (0, 0, 1) if False else Vector((-side * math.sin(A_PITCH), 0, math.cos(A_PITCH))), R)
    # hoist beam out of the south gable apex with a pulley, rope and bucket
    hb0 = Vector((0, -AY + 0.4, A_RIDGE - 0.75)); hb1 = Vector((0, -AY - 1.15, A_RIDGE - 0.75))
    plank(g, 'FK_Oak', hb0, hb1, 0.22, 0.22, (1, 0, 0), R)
    plank(g, 'FK_Oak', Vector((0, -AY - 0.02, A_RIDGE - 1.6)), Vector((0, -AY - 0.75, A_RIDGE - 0.86)), 0.12, 0.12, (1, 0, 0), R)   # brace
    cyl(g, 'FK_Oak', hb1 + Vector((-0.04, 0.12, -0.2)), hb1 + Vector((0.04, 0.12, -0.2)), 0.11, 10, 'wood', R)
    rope(g, hb1 + Vector((0, 0.06, -0.3)), Vector((0, -AY - 1.0, 1.25)), 0.0, 2, 0.014)
    cyl(g, 'FK_Oak', Vector((0, -AY - 1.0, 0.95)), Vector((0, -AY - 1.0, 1.25)), 0.14, 10, 'wood', R)
    # iron stovepipe through the east slope, with a hat
    sp0 = Se(0.9, Le * 0.55, 0.0); sp1 = sp0 + Vector((0, 0, 1.25))
    cyl(g, 'FK_Iron', sp0 - Vector((0, 0, 0.3)), sp1, 0.09, 10, 'metal', R, caps=False)
    cyl(g, 'FK_Iron', sp1 + Vector((0, 0, 0.12)), sp1 + Vector((0, 0, 0.22)), 0.22, 10, 'metal', R, r2=0.04)
    box(g, 'HH_Dark', (0.12, 0.12, 0.01), Matrix.Translation(sp1 + Vector((0, 0, 0.05))), 'soot', R)
    # door: salvaged planks with a painted board, in the stone storey's east face; worn step
    xf = E_.xf @ Matrix.Translation((door.uc, 0.32, 0.12))
    door_leaf(g, R, 1.18, 2.22, xf, ['FK_Oak', 'HH_PaintRed', 'FK_OakDoor', 'HH_PlankGrey'])
    window_fill(g, R, S_, S_win, 0.38); open_shutters(g, R, S_, S_win, ['FK_Shutter', 'HH_PaintOchre'], ang=(10, 25))
    window_fill(g, R, N_, N_win, 0.38)
    board_patch(g, R, E_, patch_e, ['FK_Oak', 'HH_PlankGrey', 'FK_OakDoor'])
    # dressing: a stack of salvaged boards and a small wood pile against the north wall, a crate by the door
    woodpile(g, R, -AX + 0.2, AX - 0.6, AY + 0.05, AY + 0.6, 0.0, 0.85)
    box(g, 'FK_Oak', (0.55, 0.5, 0.45), Matrix.Translation((AX + 0.45, -AY + 0.9, 0.225)) @ rot(0.25), 'wood', R, cham=0.02)
    quad_grid(dec, 'FK_Decal_drip', lambda u, v: S_.P(S_win.u0 - 0.1 + (S_win.u1 - S_win.u0 + 0.2) * u, S_win.z0 - 1.1 + 0.95 * v, 0.045), 1, 1, 'decal')
    return g, dec, glow

# ----------------------------------------------------------------------------- mesh, UVs, wear
def wear_rgb(p, n, tag, slot, top_z):
    nz = noise.noise(p * 0.9) * 0.5 + 0.5; nf = noise.noise(p * 3.1 + Vector((5, 1, 2))) * 0.5 + 0.5
    r = gr = b = 0.0
    if tag in ('stone', 'core', 'floor', 'step', 'plaster'):
        r = 0.62 * max(0.0, 1 - p.z / 0.75) ** 1.4 + 0.12 * nz            # splash-back band at the foot
        r += 0.18 * max(0.0, 1 - abs(p.z - top_z) / 0.9) * nf              # weathering under the eaves
        gr = (0.55 if n.z > 0.55 else 0.0) * nf + 0.35 * max(0.0, 1 - p.z / 0.9) * (0.5 + 0.5 * max(0.0, n.y)) * nz
        if tag == 'step':
            b = 0.55
        if tag == 'plaster':
            r = 0.18 + 0.22 * nf + 0.3 * max(0.0, 1 - (p.z - 0.8) / 0.6); gr = 0.0
    elif tag == 'slate':
        r = 0.12 + 0.2 * nz; gr = (0.75 * nf if nz > 0.45 else 0.15 * nf) * (1.0 if n.z > 0.3 else 0.3)
    elif tag in ('wood', 'door'):
        r = 0.25 * nz + 0.3 * max(0.0, 1 - p.z / 0.5)
        gr = 0.25 * nf * (1 if n.z > 0.6 else 0)
        if tag == 'door':
            b = 0.25 * nf
    elif tag == 'seat':
        b = 0.5; r = 0.1
    elif tag == 'soot':
        r = 1.0
    elif tag == 'cloth':
        r = 0.08 * nz
    elif tag == 'pot':
        r = 0.6 * nz
    return (min(1, r), min(1, gr), min(1, b), 1.0)

def to_object(geo, coll, top_z):
    me = bpy.data.meshes.new(geo.name)
    me.from_pydata([tuple(v) for v in geo.v], [], geo.f)
    slots = []
    for s in geo.slot:
        if s not in slots:
            slots.append(s)
    for s in slots:
        me.materials.append(SLOT_DEFS[s]())
    me.validate(clean_customdata=False); me.update()
    if len(me.polygons) != len(geo.f):
        raise RuntimeError('%s: validate dropped %d faces' % (geo.name, len(geo.f) - len(me.polygons)))
    me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER'); me.uv_layers.new(name='UVMap')
    uvl = me.uv_layers['UVMap']; col = me.color_attributes['wear']   # fetch after both exist (adding a layer moves the others)
    for pi, poly in enumerate(me.polygons):
        poly.material_index = slots.index(geo.slot[pi])
        n = poly.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        ou, ov = geo.uvo[pi]
        decal = geo.slot[pi] in DECAL_SLOTS
        for k, li in enumerate(poly.loop_indices):
            p = me.vertices[me.loops[li].vertex_index].co
            if decal:   # decals: 0..1 over the quad
                uvl.data[li].uv = [(0, 0), (1, 0), (1, 1), (0, 1)][k % 4]
            else:
                if ax == 2:
                    u, v = p.x, p.y
                elif ax == 0:
                    u, v = p.y * (1 if n.x > 0 else -1), p.z
                else:
                    u, v = p.x * (-1 if n.y > 0 else 1), p.z
                uvl.data[li].uv = (u / TILE + ou, v / TILE + ov)
            col.data[li].color = wear_rgb(p, n, geo.tag[pi], geo.slot[pi], top_z)
    me.color_attributes.active_color = col
    ob = bpy.data.objects.new(geo.name, me); coll.objects.link(ob)
    for poly in me.polygons:
        poly.use_smooth = False
    return ob

def footprint(coll, sx, sy):
    hx, hy = sx / 2, sy / 2
    vs = [(-hx, -hy, 0), (hx, -hy, 0), (hx, hy, 0), (-hx, hy, 0), (-hx, -hy, 0.1), (hx, -hy, 0.1), (hx, hy, 0.1), (-hx, hy, 0.1)]
    me = bpy.data.meshes.new('FOOTPRINT'); me.from_pydata(vs, [], CUBE_F)
    me.materials.append(bpy.data.materials.get('FOOTPRINT') or bpy.data.materials.new('FOOTPRINT'))
    ob = bpy.data.objects.new('FOOTPRINT', me); coll.objects.link(ob); ob.hide_render = True
    return ob

def assemble(name, parts, size, sockets, top_z):
    coll = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(coll)
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.empty_display_type = 'PLAIN_AXES'
    frame, dec, glow = parts
    frame.name = 'Frame'; dec.name = 'Decals'; glow.name = 'Glow'
    objs = [to_object(frame, coll, top_z), to_object(dec, coll, top_z), to_object(glow, coll, top_z), footprint(coll, *size)]
    for sname, loc in sockets:
        e = bpy.data.objects.new(sname, None); coll.objects.link(e); e.empty_display_type = 'ARROWS'; e.empty_display_size = 0.4; e.location = loc
        objs.append(e)
    for o in objs:
        o.parent = root; o['base'] = o.name.split('.')[0]; o.name = name + '__' + o['base']
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs[:3])
    ext = [max(abs(v.co[i]) for o in objs[:1] for v in o.data.vertices) for i in range(3)]
    print('BUILDING %s: tris %d (frame %d, decals %d, glow %d), extent x %.2f y %.2f z %.2f'
          % (name, tris, frame.tris(), dec.tris(), glow.tris(), ext[0], ext[1], ext[2]))
    return dict(name=name, root=root, objs=[root] + objs, coll=coll, tris=tris)

def export(b):
    path = os.path.join(OUT, 'fbx', b['name'] + '.fbx'); os.makedirs(os.path.dirname(path), exist_ok=True)
    kids = [o for o in b['objs'] if 'base' in o]
    loc = b['root'].location.copy(); b['root'].location = (0, 0, 0)
    for o in kids:
        o.name = o['base']
    bpy.ops.object.select_all(action='DESELECT')
    for o in b['objs']:
        o.hide_set(False); o.select_set(True)
    bpy.context.view_layer.objects.active = b['root']
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', colors_type='LINEAR', apply_scale_options='FBX_SCALE_UNITS',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, add_leaf_bones=False, bake_anim=False,
                             use_custom_props=False, use_tspace=False, path_mode='STRIP')
    for o in kids:
        o.name = b['name'] + '__' + o['base']
    b['root'].location = loc
    os.makedirs(EXP, exist_ok=True)
    shutil.copy(path, os.path.join(EXP, b['name'] + '.fbx'))
    print('EXPORT', path, '->', EXP)

# ----------------------------------------------------------------------------- review
def stage():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'None'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.45, 0.58, 0.78, 1); bg.inputs['Strength'].default_value = 0.85
    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 4.2; sun.data.color = (1.0, 0.93, 0.82); sun.data.angle = math.radians(1.5)
    sun.rotation_euler = (math.radians(42), 0, math.radians(200))   # from the south-south-west, as the corner's midday
    # ground: cobble tileable on the square, a terrace wall at z 19.6 rising 4 m, the keep terrace, a greybox bell tower
    cob = tile_mat('SB_Cobble', 'B1_town/cobble')
    gg = Geo('SB_Ground')
    def ground_quad(x0, x1, y0, y1, z):
        gg.add([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], [(0, 1, 2, 3)], 'SB_Cobble', 'floor')
    ground_quad(-10, 40, -20, 19.6, 0.0); ground_quad(-10, 40, 19.6, 45, 4.0)
    gg.add([(-10, 19.6, 0), (40, 19.6, 0), (40, 19.6, 4), (-10, 19.6, 4)], [(0, 1, 2, 3)], 'FK_ChimneyStone', 'stone')
    for x0, x1, y0, y1, z0, z1 in ((2.35, 8.85, 20.75, 27.25, 4, 21), (5.6 - 2.5 - 2.5, 5.6 + 2.5 - 2.5, 0, 0, 0, 0)):
        if x1 > x0:
            vs = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
            gg.add(vs, CUBE_F, 'FK_ChimneyStone', 'stone')
    SLOT_DEFS['SB_Cobble'] = lambda: cob
    x, z, y = SITES['HeroesHome_Cistern']
    lamp = bpy.data.objects.new('StairLight', bpy.data.lights.new('StairLight', 'POINT')); sc.collection.objects.link(lamp)
    lamp.data.energy = 120; lamp.data.color = (1.0, 0.62, 0.30); lamp.data.shadow_soft_size = 0.3
    lamp.location = (x + STAIR[0], z + STAIR[1] + 0.4, y - 0.8)
    lamp2 = bpy.data.objects.new('StairHeadLight', bpy.data.lights.new('StairHeadLight', 'POINT')); sc.collection.objects.link(lamp2)
    lamp2.data.energy = 45; lamp2.data.color = (1.0, 0.70, 0.42); lamp2.data.shadow_soft_size = 0.2
    lamp2.location = (x + STAIR[0], z + STAIR[1] - 0.2, y + 1.9)
    ob = to_object(gg, sc.collection, 99)
    return sun

def cam_at(name, eye, at, fov=42):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name)); bpy.context.scene.collection.objects.link(cam)
    cam.data.sensor_fit = 'VERTICAL'; cam.data.angle = math.radians(fov); cam.data.clip_start = 0.05
    e = Vector(eye); t = Vector(at); cam.location = e
    cam.rotation_euler = (t - e).to_track_quat('-Z', 'Y').to_euler()
    return cam

def render(cam, path):
    sc = bpy.context.scene; sc.camera = cam
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 9 / 16); sc.render.resolution_percentage = 100
    sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)

# camera eye / target in Unity world (x, y up, z) -> Blender (x, z, y)
VIEWS_DEF = {
    'square': ((1.5, 1.7, -2.0), (12.0, 4.0, 13.0), 50),        # from the fountain: the whole home, the annex up on the terrace
    'front': ((10.0, 1.65, 1.5), (12.5, 2.4, 8.0), 55),         # the door, the list, the bench (mid distance)
    'west': ((2.0, 1.7, 9.0), (9.0, 2.2, 11.5), 55),            # the west yard: washing line, wood pile
    'yard': ((19.5, 1.7, 3.0), (13.0, 3.0, 11.0), 55),          # from the Keep Lane mouth (south-east)
    'annex': ((19.2, 5.7, 20.6), (11.8, 6.6, 24.5), 55),        # on the terrace walk, looking at the annex door and loft
    'stair': ((11.0, 1.6, 4.6), (15.0, 1.0, 11.6), 40),         # through the open door: the stair arch, warm light below
    'close': ((10.5, 1.6, 5.8), (10.9, 2.0, 7.5), 40),          # Wren's list close up
}

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_list_texture(LIST_PNG)
    bs = []
    c = assemble('HeroesHome_Cistern', build_cistern(), (2 * HX, 2 * HY),
                 [('SOCKET_Door', (0.0, -HY, 0.14)), ('SOCKET_UndercroftStair', (STAIR[0], STAIR[1], 0.12))], RIDGE)
    a = assemble('HeroesHome_Annex', build_annex(), (2 * AX, 2 * AY), [('SOCKET_Door', (AX, 0.0, 0.12))], A_RIDGE)
    bs = [c, a]
    if EXPORT:
        for b in bs:
            export(b)
        shutil.copy(LIST_PNG, os.path.join(EXP, 'HH_List_BaseColor.png'))
    with open(os.path.join(OUT, 'hh_stats.txt'), 'w') as f:
        for b in bs:
            f.write('%s tris %d\n' % (b['name'], b['tris']))
    if RENDER:
        for b in bs:
            x, z, y = SITES[b['name']]
            b['root'].location = (x, z, y)
        stage()
        for v in VIEWS:
            e, t, fov = VIEWS_DEF[v]
            cam = cam_at('cam_' + v, (e[0], e[2], e[1]), (t[0], t[2], t[1]), fov)
            render(cam, os.path.join(OUT, 'hh_%s.png' % v))

main()
if "--dbg" in argv: exec(open(arg("--dbg")).read())
