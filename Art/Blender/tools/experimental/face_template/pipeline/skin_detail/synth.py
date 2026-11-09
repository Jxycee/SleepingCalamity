"""Procedural skin micro-structure in torch (CUDA when available). Everything is evaluated from a 3D position in millimetres, so the result has physical scale, is
continuous across UV island borders, and (with `period`) can also be made seamlessly tileable. Heights are in mm of relief.
Layers: pores (Worley F1 pits, ~40-60% of cells active, random depth), skin lines (Worley F2-F1 furrow network at two scales = the lozenge pattern of real skin),
fBm value noise (fine undulation), and a very low-amplitude chroma mottling field."""
import torch, math

DEV = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
M32 = 0xFFFFFFFF

def _hash(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 374761393 + 1013904223)
    h = h & M32
    h = ((h ^ (h >> 15)) * 2246822519) & M32
    h = ((h ^ (h >> 13)) * 3266489917) & M32
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF).float() / 16777216.0

def _wrap(i, period):
    return torch.remainder(i, period) if period else i

def worley(p, cell, seed=0, period=None):
    """p (N,3) mm. Returns F1, F2 (in cell units), id of the nearest cell (0..1 random). period = number of cells per axis (tiling) or None."""
    q = p / cell; base = torch.floor(q).long()
    f1 = torch.full((p.shape[0],), 9.0, device=p.device); f2 = f1.clone(); rid = torch.zeros_like(f1)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            for dz in (-1, 0, 1):
                c = base + torch.tensor([dx, dy, dz], device=p.device)
                cw = _wrap(c, period)
                jit = torch.stack([_hash(cw[:, 0], cw[:, 1], cw[:, 2], seed + k) for k in (1, 2, 3)], 1)
                d = ((q - (c.float() + jit)) ** 2).sum(1).sqrt()
                r = _hash(cw[:, 0], cw[:, 1], cw[:, 2], seed + 7)
                lt = d < f1
                f2 = torch.where(lt, f1, torch.minimum(f2, d)); rid = torch.where(lt, r, rid); f1 = torch.where(lt, d, f1)
    return f1, f2, rid

def vnoise(p, cell, seed=0, period=None):
    """3D value noise in -1..1 (smoothstep trilinear)."""
    q = p / cell; b = torch.floor(q); f = q - b; b = b.long(); s = f * f * (3 - 2 * f)
    acc = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                c = _wrap(b + torch.tensor([dx, dy, dz], device=p.device), period)
                v = _hash(c[:, 0], c[:, 1], c[:, 2], seed) * 2 - 1
                w = (s[:, 0] if dx else 1 - s[:, 0]) * (s[:, 1] if dy else 1 - s[:, 1]) * (s[:, 2] if dz else 1 - s[:, 2])
                acc = acc + v * w
    return acc

def fbm(p, cell, octaves, seed=0, period=None, gain=0.5):
    a = 1.0; tot = 0; norm = 0
    for o in range(octaves):
        pr = None if period is None else period * (2 ** o)
        tot = tot + a * vnoise(p, cell / (2 ** o), seed + 11 * o, pr); norm += a; a *= gain
    return tot / norm

def skin_layers(p_mm, pore_mm=0.8, line_mm=1.6, tile_mm=None, seed=1, pore_density=0.55, pore_depth=0.040, line_depth=0.015, line_width=0.15):
    """p_mm (N,3) float32 tensor on DEV. tile_mm: if set, every layer is periodic over tile_mm (all cell sizes must divide it: use the defaults with 12.8).
    Returns dict of per-point arrays: height (mm), pore (0..1 pit weight), line (0..1.5 furrow weight), low (-1..1 mottling)."""
    P = (lambda cell: None) if tile_mm is None else (lambda cell: int(round(tile_mm / cell)))
    f1, f2, rid = worley(p_mm, pore_mm, seed, P(pore_mm))
    act = (rid < pore_density).float(); depth = 0.5 + rid / max(pore_density, 1e-3)
    rad = 0.20 + 0.10 * _hash((rid * 4096).long(), (rid * 777).long(), (rid * 31).long(), seed + 5)
    pore = torch.exp(-(f1 / rad) ** 2) * act * depth.clamp(0.4, 1.6) / 1.3
    h = -pore_depth * pore
    line = torch.zeros_like(h)
    for k, (lm, amp) in enumerate(((line_mm, 1.0), (line_mm * 0.5, 0.55))):
        g1, g2, _ = worley(p_mm, lm, seed + 100 + k * 17, P(lm))
        ln = torch.exp(-((g2 - g1) / line_width) ** 2) * amp; line = line + ln; h = h - line_depth * ln
    h = h + 0.012 * fbm(p_mm, 3.2, 3, seed + 300, P(3.2))
    low = fbm(p_mm, 6.4, 2, seed + 500, P(6.4))
    return dict(height=h, pore=pore, line=line.clamp(0, 1.5), low=low)

def to_np(t): return t.detach().cpu().numpy()
