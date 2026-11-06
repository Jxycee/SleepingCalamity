"""Procedural cabbage-leaf texture set for the garden beds (sc_garden.py): numpy + Pillow, deterministic, no Blender needed.

One 1024 x 1024 atlas per map: the BOTTOM half (v 0..0.5) is the outer leaf (blue-green, #5b7a5a at the rim, paler #9fb486 toward the base), the TOP half (v 0.5..1)
is the pale heart variant (2026-11-05: stacked so a UV unit is the same on U and V). Inside a half, u runs across the leaf and v along it (0 at the stalk, 1 at the tip), so the midrib is the centre line u = 0.5.
  Cabbage_BaseColor.png  sRGB albedo: pale cream-green midrib, 6 pairs of paler side veins curving out to the rim, faint large-scale mottle, no baked light
  Cabbage_Normal.png     OpenGL (Y+) tangent normal from a height map (raised midrib and veins, subtle savoy pucker between them) via sc_texgrade.normal_from_height
  Cabbage_ORM.png        R occlusion 1, G roughness 0.55, B metal 0

  python3 Art/Blender/town/sc_cabbage_tex.py <out dir>
"""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools'))
import sc_texgrade as tg

H = 512
NPAIR = 6


def _hex(h):
    h = h.lstrip('#'); return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], np.float32)


def _smooth(a, sigma):
    return tg.blur(a, sigma)


def _half(heart, seed):
    """-> (albedo srgb HxWx3, height HxW) for one leaf half."""
    y, x = np.mgrid[0:H, 0:H].astype(np.float32)
    u = (x + 0.5) / H; v = 1.0 - (y + 0.5) / H
    u2 = (u - 0.5) * 2.0; a = np.abs(u2)
    # midrib: wide at the stalk, thin at the tip
    wr = 0.075 * (1 - 0.7 * v) + 0.014
    rib = np.exp(-(u2 / wr) ** 2)
    # side veins: vein k leaves the midrib at v0 and climbs toward the rim; fades near the rim and where the blade ends
    vein = np.zeros_like(u); fine = np.zeros_like(u)
    for k in range(NPAIR):
        v0 = 0.10 + 0.60 * k / (NPAIR - 1)
        lift = 0.34 - 0.10 * k / (NPAIR - 1)
        vv = v0 + lift * a ** 1.35
        d = np.abs(v - vv) * np.cos(np.arctan(1.35 * lift * a ** 0.35))
        wv = 0.0125 * (1 - 0.45 * a) + 0.003
        prof = np.exp(-(d / wv) ** 2) * np.clip(1.25 - 1.15 * a ** 2, 0, 1) * np.clip((a - 0.04) / 0.08, 0, 1)
        vein = np.maximum(vein, prof)
        # faint secondary veinlets half way between
        d2 = np.abs(v - (vv + 0.5 * 0.14 * (1 - 0.3 * a))) * 0.9
        fine = np.maximum(fine, np.exp(-(d2 / 0.006) ** 2) * np.clip(1.0 - a ** 2, 0, 1) * np.clip((a - 0.1) / 0.1, 0, 1))
    # colour
    rim_c = _hex('#a9bf86') if heart else _hex('#5b7a5a')
    base_c = _hex('#d9e3ab') if heart else _hex('#9fb486')
    vein_c = _hex('#f0f2d4') if heart else _hex('#cfd9ab')
    mot = tg.fbm_periodic(H, seed, beta=3.2, kmin=1, kmax=6) - 0.5            # large scale, about +-0.2
    mot2 = tg.fbm_periodic(H, seed + 1, beta=2.2, kmin=3, kmax=24) - 0.5
    t = np.clip((1 - v) ** 1.7 * 0.95 - 0.10 * a ** 2 + mot * 0.22, 0, 1)    # pale toward the base, blue-green toward the rim and tip
    col = rim_c[None, None, :] * (1 - t[..., None]) + base_c[None, None, :] * t[..., None]
    col *= (1.0 + mot2 * 0.12 + mot * 0.16)[..., None]
    col[..., 2] *= (1.0 + mot * 0.08)                                         # slight blue / yellow drift
    cw = np.clip(rib * 0.95 + vein * 0.85 + fine * 0.18, 0, 1)[..., None]
    col = col * (1 - cw) + vein_c[None, None, :] * (np.minimum(col, 1) * 0.35 + 0.65) * cw
    col = np.clip(col, 0, 1)
    # height: raised rib and veins, subtle savoy pucker (bumps between the veins, weaker at the rib)
    n1 = tg.fbm_periodic(H, seed + 2, beta=1.8, kmin=6, kmax=34) - 0.5
    pucker = n1 * (0.55 + 0.45 * np.clip(a * 2, 0, 1)) * (1 - np.clip(vein * 1.2 + rib, 0, 1) * 0.6)
    hgt = 0.45 + 0.42 * rib + 0.26 * vein + 0.07 * fine + 0.30 * pucker
    hgt = _smooth(hgt.astype(np.float32), 1.1)
    return col.astype(np.float32), hgt.astype(np.float32)


def _wide(a):
    """512 x 512 half -> 1024 wide x 512 tall (Lanczos, per channel)."""
    chans = [a] if a.ndim == 2 else [a[..., k] for k in range(a.shape[2])]
    out = [np.asarray(Image.fromarray(c.astype(np.float32), 'F').resize((2 * H, H), Image.LANCZOS)) for c in chans]
    return out[0] if a.ndim == 2 else np.stack(out, -1)


def make(out):
    """Atlas 1024 x 1024: outer leaf = bottom half of the image (v 0..0.5), heart = top half (v 0.5..1), each 1024 x 512. A UV unit is the same on U and V,
    so a leaf mapped at one metres-to-UV scale (sc_garden.cabbage_uv_scale) is not stretched."""
    os.makedirs(out, exist_ok=True)
    cols, hs = [], []
    for heart, seed in ((False, 11), (True, 29)):
        c, h = _half(heart, seed); cols.append(_wide(c)); hs.append(_wide(h))
    # image rows run top-down: v 0.5..1 (heart) on top, v 0..0.5 (outer) below
    alb = np.concatenate([cols[1], cols[0]], 0)
    nrm = np.concatenate([tg.normal_from_height(h, 8.0) for h in (hs[1], hs[0])], 0)
    orm = np.zeros((2 * H, 2 * H, 3), np.float32); orm[..., 0] = 1.0; orm[..., 1] = 0.55
    for n, arr in (('BaseColor', alb), ('Normal', nrm), ('ORM', orm)):
        Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB').save(os.path.join(out, 'Cabbage_%s.png' % n), optimize=True)
    return [os.path.join(out, 'Cabbage_%s.png' % n) for n in ('BaseColor', 'Normal', 'ORM')]


if __name__ == '__main__':
    print('\n'.join(make(os.path.abspath(sys.argv[1]))))
