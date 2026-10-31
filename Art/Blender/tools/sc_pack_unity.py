"""[Ported from FishGame, M8; --orm mode added.] Pack an aaa_kit baked set for Unity URP Lit (metallic workflow).
python pack_unity.py <tex_dir> <Prefix> <out_dir>
Writes <Prefix>_Albedo.png (sRGB), <Prefix>_MetallicSmoothness.png (R metallic, A smoothness = 1 - roughness),
<Prefix>_Normal.png (OpenGL / Y+, what Blender bakes and Unity expects; import as Normal map),
<Prefix>_Occlusion.png, and <Prefix>_Emission.png when present.
--orm <orm.png> <out_prefix>: split an ORM into URP Lit maps. --to-orm <tex_dir> <Prefix> <out_dir> [<OutName>]: pack a
baked set into <OutName>_BaseColor / _Normal / _ORM (the Town export format, beauty-corner brief 4).
"""
import os, sys, shutil
import numpy as np
from PIL import Image

if sys.argv[1] == '--orm':
    # python sc_pack_unity.py --orm <orm.png> <out_prefix>: glTF-style ORM (R occlusion, G roughness, B metallic)
    # -> <out_prefix>_MetallicSmoothness.png (R metallic, A smoothness) + <out_prefix>_Occlusion.png (URP Lit).
    o = np.asarray(Image.open(sys.argv[2]).convert('RGB'))
    ms = np.dstack([o[..., 2], o[..., 2], o[..., 2], 255 - o[..., 1]]).astype(np.uint8)
    Image.fromarray(ms, 'RGBA').save(sys.argv[3] + '_MetallicSmoothness.png')
    Image.fromarray(np.dstack([o[..., 0]] * 3).astype(np.uint8)).save(sys.argv[3] + '_Occlusion.png')
    print('packed ORM ->', sys.argv[3]); sys.exit(0)
if sys.argv[1] == '--to-orm':
    # python sc_pack_unity.py --to-orm <tex_dir> <Prefix> <out_dir> [<OutName>]: a generator's baked set
    # (<Prefix>_BaseColor/_Normal/_AO/_Roughness[/_Metallic].png) -> <OutName>_BaseColor, _Normal, _ORM
    # (R occlusion, G roughness, B metallic; B 0 when there is no metallic map) for the Town/Cellar exports.
    src, pre, out = sys.argv[2], sys.argv[3], sys.argv[4]
    name = sys.argv[5] if len(sys.argv) > 5 else pre
    os.makedirs(out, exist_ok=True)
    P = lambda k: os.path.join(src, f'{pre}_{k}.png')
    shutil.copy(P('BaseColor'), os.path.join(out, f'{name}_BaseColor.png'))
    shutil.copy(P('Normal'), os.path.join(out, f'{name}_Normal.png'))
    L = lambda k: np.asarray(Image.open(P(k)).convert('L'))
    ao, r = L('AO'), L('Roughness')
    m = L('Metallic') if os.path.exists(P('Metallic')) else np.zeros_like(r)
    Image.fromarray(np.dstack([ao, r, m]).astype(np.uint8), 'RGB').save(os.path.join(out, f'{name}_ORM.png'))
    print('ORM', pre, '->', os.path.join(out, name)); sys.exit(0)
src, pre, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
P = lambda k: os.path.join(src, f'{pre}_{k}.png')
shutil.copy(P('BaseColor'), os.path.join(out, f'{pre}_Albedo.png'))
shutil.copy(P('Normal'), os.path.join(out, f'{pre}_Normal.png'))
shutil.copy(P('AO'), os.path.join(out, f'{pre}_Occlusion.png'))
if os.path.exists(P('Emission')): shutil.copy(P('Emission'), os.path.join(out, f'{pre}_Emission.png'))
m = np.asarray(Image.open(P('Metallic')).convert('L'))
r = np.asarray(Image.open(P('Roughness')).convert('L'))
ms = np.dstack([m, m, m, 255 - r]).astype(np.uint8)
Image.fromarray(ms, 'RGBA').save(os.path.join(out, f'{pre}_MetallicSmoothness.png'))
print('packed', pre, '->', out)
