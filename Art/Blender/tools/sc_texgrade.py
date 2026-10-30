"""sc_texgrade.py (M8): numpy/Pillow helpers that turn CC0 scans into graded, tileable PBR sets for the locked style.
Plain python (numpy + Pillow, no scipy, no Blender). Every operation wraps around the image edges, so a seamless scan stays seamless.
Used by Art/Blender/textures/sc_tex_batches.py. See sc-texture-paint for the method.

  load_srgb / load_gray      read a scan at the working resolution
  blur                       periodic gaussian (FFT)
  fbm_periodic               tileable noise (spectrum-shaped white noise)
  grade_albedo               de-light, contrast, hue pull toward a PHASE1 3.1 key hue, chroma
  normal_from_height         OpenGL (Y+) tangent normal from a height field, periodic
  scale_normal, mix_normals  normal strength and detail blending
  seam_ratio                 wrap-edge difference vs interior neighbour difference (about 1.0 = seamless)
"""
import numpy as np
from PIL import Image

RES = 2048


def srgb2lin(x):
    x = np.asarray(x, dtype=np.float32)
    return np.where(x <= 0.04045, x / 12.92, ((x + 0.055) / 1.055) ** 2.4).astype(np.float32)


def lin2srgb(x):
    x = np.clip(np.asarray(x, dtype=np.float32), 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * x ** (1 / 2.4) - 0.055).astype(np.float32)


def hex2lin(h):
    h = h.lstrip('#')
    return srgb2lin(np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)], dtype=np.float32))


def load_srgb(path, res=RES):
    im = Image.open(path).convert('RGB')
    if im.size != (res, res):
        im = im.resize((res, res), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def load_gray(path, res=RES):
    im = Image.open(path).convert('L')
    if im.size != (res, res):
        im = im.resize((res, res), Image.LANCZOS)
    return np.asarray(im, dtype=np.float32) / 255.0


def save_rgb(a, path):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB').save(path, optimize=True)


def save_rgba(a, path):
    Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(path, optimize=True)


def blur(a, sigma):
    """Periodic gaussian blur by FFT. a: HxW or HxWxC."""
    if a.ndim == 3:
        return np.stack([blur(a[..., c], sigma) for c in range(a.shape[2])], -1)
    h, w = a.shape
    fy = np.fft.fftfreq(h)[:, None]; fx = np.fft.rfftfreq(w)[None, :]
    k = np.exp(-2.0 * (np.pi ** 2) * (sigma ** 2) * (fx ** 2 + fy ** 2)).astype(np.float32)
    return np.fft.irfft2(np.fft.rfft2(a) * k, s=a.shape).astype(np.float32)


def fbm_periodic(res, seed, beta=2.4, kmin=1.0, kmax=None):
    """Tileable noise in 0..1 (mean 0.5, ~3 sigma inside). beta: spectral slope (higher = smoother, larger features)."""
    rng = np.random.default_rng(seed)
    w = rng.standard_normal((res, res)).astype(np.float32)
    F = np.fft.rfft2(w)
    fy = np.fft.fftfreq(res)[:, None] * res; fx = np.fft.rfftfreq(res)[None, :] * res
    r = np.sqrt(fx ** 2 + fy ** 2); r[0, 0] = 1.0
    amp = r ** (-beta / 2.0)
    amp[0, 0] = 0.0
    amp[r < kmin] = 0.0
    if kmax:
        amp[r > kmax] = 0.0
    n = np.fft.irfft2(F * amp, s=(res, res)).astype(np.float32)
    n = n / (n.std() + 1e-8)
    return np.clip(0.5 + n / 6.0, 0.0, 1.0)


def smoothstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def luminance(lin):
    return lin[..., 0] * 0.2126 + lin[..., 1] * 0.7152 + lin[..., 2] * 0.0722


def grade_albedo(src_srgb, target_hex, hue_keep=0.6, delight=0.7, contrast=1.0, chroma=1.0, sigma=140, vmin=0.0):
    """Scan albedo (sRGB 0..1) -> graded albedo (sRGB 0..1).
    - delight: divide out the scan's large-scale light/shade (low-frequency luminance) by this share (0 = off, 1 = flat)
    - contrast: power on the luminance structure around its mean (1 = keep, >1 stronger structure)
    - hue_keep: 1 = keep the scan's own colour variation, gain-matched to the target mean (brick tan vs orange);
                0 = take only the scan's luminance structure and paint the target colour
    - chroma: saturation about the luminance after grading (+10-15% only on foliage, sky, painted wood, cloth, accents)
    - vmin: floor on the result (no mass is pure black)
    The mean linear colour of the result equals the target (before chroma), so the batch measures against the key hues."""
    lin = srgb2lin(src_srgb)
    L = luminance(lin) + 1e-5
    if delight > 0:
        low = blur(L, sigma)
        f = np.clip((L.mean() / np.maximum(low, 1e-4)) ** delight, 0.55, 1.8)
        lin = lin * f[..., None]
        L = luminance(lin) + 1e-5
    Lm = L.mean()
    lin = lin * ((L / Lm) ** (contrast - 1.0))[..., None]
    L = luminance(lin) + 1e-5
    tgt = hex2lin(target_hex)
    # (a) gain mode: the scan's colour, scaled per channel so its mean is the target
    gain = lin * (tgt / np.maximum(lin.reshape(-1, 3).mean(0), 1e-5))
    # (b) paint mode: the target colour modulated by the scan's luminance structure
    paint = tgt[None, None, :] * (L / L.mean())[..., None]
    out = gain * hue_keep + paint * (1.0 - hue_keep)
    out = out * (tgt / np.maximum(out.reshape(-1, 3).mean(0), 1e-5))   # re-pin the mean after mixing
    if chroma != 1.0:
        Lo = luminance(out)[..., None]
        out = np.maximum(Lo + (out - Lo) * chroma, 0.0)
    out = np.maximum(out, srgb2lin(np.float32(vmin)) if vmin > 0 else 0.0)
    return lin2srgb(out)


def normal_from_height(h, strength=2.0):
    """OpenGL (Y+ up in the image) tangent-space normal from height (0..1), periodic differences. strength ~ height range in texel units."""
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5
    nx = -dx * strength; ny = dy * strength; nz = np.ones_like(h)
    n = np.stack([nx, ny, nz], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def scale_normal(n_rgb, s):
    xy = (n_rgb[..., :2] * 2 - 1) * s
    z = np.sqrt(np.clip(1.0 - (xy ** 2).sum(-1), 0.0, 1.0))
    n = np.concatenate([xy, z[..., None]], -1)
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    return n * 0.5 + 0.5


def mix_normals(a_rgb, b_rgb):
    """Whiteout blend of two tangent normals (detail b over base a)."""
    a = a_rgb * 2 - 1; b = b_rgb * 2 - 1
    n = np.stack([a[..., 0] + b[..., 0], a[..., 1] + b[..., 1], a[..., 2] * b[..., 2]], -1)
    n /= np.maximum(np.linalg.norm(n, axis=-1, keepdims=True), 1e-6)
    return n * 0.5 + 0.5


def seam_ratio(a):
    """Mean abs difference across the wrap edges divided by the mean abs difference of interior neighbours (both axes). ~1 seamless; >2 visible seam."""
    g = a if a.ndim == 2 else a.mean(-1)
    ex = np.abs(g[:, 0] - g[:, -1]).mean(); ix = np.abs(g[:, 1:] - g[:, :-1]).mean()
    ey = np.abs(g[0, :] - g[-1, :]).mean(); iy = np.abs(g[1:, :] - g[:-1, :]).mean()
    return float(max(ex / max(ix, 1e-6), ey / max(iy, 1e-6)))


def make_tileable(a, margin=0.5):
    """Fallback for a non-seamless source: offset by half, cross-fade the old seams with a smooth mask (loses a little detail in the blended band)."""
    h, w = a.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    wx = 1 - np.abs(xx / (w - 1) * 2 - 1); wy = 1 - np.abs(yy / (h - 1) * 2 - 1)
    m = smoothstep(np.minimum(wx, wy), 0.0, margin)[..., None] if a.ndim == 3 else smoothstep(np.minimum(wx, wy), 0.0, margin)
    shifted = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    return a * m + shifted * (1 - m)


def tile3(a, size=1024):
    """3x3 tiling of an image, resized to size px, for the tiling/seam review."""
    img = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))
    t = Image.new('RGB', (img.width * 3, img.height * 3))
    for i in range(3):
        for j in range(3):
            t.paste(img, (i * img.width, j * img.height))
    return t.resize((size, size), Image.LANCZOS)
