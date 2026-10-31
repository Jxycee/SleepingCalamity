"""sc_tex_to_unity.py (M8 part 2): copies the graded B1-B3 sets into the Unity project and packs them for URP Lit.
python Art/Blender/textures/sc_tex_to_unity.py [--textures Art/Textures] [--out Assets/_Project/Content/Textures]
Per material <name> in each batch folder it writes <out>/<batch>/<name>/
  <name>_albedo.png    byte copy of the graded albedo (RGBA for decals: alpha = opacity)
  <name>_normal.png    byte copy (OpenGL Y+; the importer flags it as a normal map)
  <name>_ms.png        URP Lit Metallic map: R metallic, A smoothness = 1 - roughness (sc_pack_unity.py --orm logic)
  <name>_occ.png       URP Lit Occlusion map (the AO channel, grey)
  <name>_mask.png      only when <name>_height.png exists (the terrain ground set): URP Terrain Lit mask map, R metallic, G occlusion,
                       B height (for the terrain's height blend), A smoothness
Import settings come from the Editor's LookTestTextureImporter (an AssetPostprocessor keyed on the file suffix), not from hand-edited .meta files.
Deterministic: unchanged inputs give byte-identical outputs, and files whose bytes would not change are left alone (no LFS churn, no reimport).
"""
import argparse, os, shutil
import numpy as np
from PIL import Image


def write_if_changed(path, make):
    tmp = path + '.tmp'
    make(tmp)
    if os.path.exists(path) and open(path, 'rb').read() == open(tmp, 'rb').read():
        os.remove(tmp); return False
    os.replace(tmp, path); return True


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--textures', default='Art/Textures'); ap.add_argument('--out', default='Assets/_Project/Content/Textures')
    ap.add_argument('--only', default='', help='comma list of material names to pack (Pillow versions encode PNGs differently: repack only what changed)')
    a = ap.parse_args()
    only = set(a.only.split(',')) if a.only else None
    n = 0
    for batch in sorted(os.listdir(a.textures)):
        bdir = os.path.join(a.textures, batch)
        if not os.path.isdir(bdir) or batch.startswith('_'):
            continue
        for name in sorted(os.listdir(bdir)):
            d = os.path.join(bdir, name)
            if not os.path.isdir(d) or (only and name not in only):
                continue
            o = os.path.join(a.out, batch, name); os.makedirs(o, exist_ok=True)
            for kind in ('albedo', 'normal'):
                src = os.path.join(d, f'{name}_{kind}.png'); dst = os.path.join(o, f'{name}_{kind}.png')
                n += write_if_changed(dst, lambda t, s=src: shutil.copyfile(s, t))
            orm = np.asarray(Image.open(os.path.join(d, f'{name}_orm.png')).convert('RGB'))
            def make_ms(t):
                ms = np.dstack([orm[..., 2], orm[..., 2], orm[..., 2], 255 - orm[..., 1]]).astype(np.uint8)
                Image.fromarray(ms, 'RGBA').save(t, format='PNG', optimize=True)
            def make_occ(t):
                Image.fromarray(np.dstack([orm[..., 0]] * 3).astype(np.uint8), 'RGB').save(t, format='PNG', optimize=True)
            n += write_if_changed(os.path.join(o, f'{name}_ms.png'), make_ms)
            n += write_if_changed(os.path.join(o, f'{name}_occ.png'), make_occ)
            hp = os.path.join(d, f'{name}_height.png')
            if os.path.exists(hp):
                hgt = np.asarray(Image.open(hp).convert('L'))
                def make_mask(t):
                    mk = np.dstack([orm[..., 2], orm[..., 0], hgt, 255 - orm[..., 1]]).astype(np.uint8)
                    Image.fromarray(mk, 'RGBA').save(t, format='PNG', optimize=True)
                n += write_if_changed(os.path.join(o, f'{name}_mask.png'), make_mask)
            print('packed', batch, name)
    print('files written:', n)


if __name__ == '__main__':
    main()
