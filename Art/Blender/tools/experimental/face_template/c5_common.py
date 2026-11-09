"""Round-3 (copy of c2_common with round-3 paths) shared helpers: paths + vectorised triangle rasterisers. numpy/scipy only.  Template data: D, round-2 data: D2."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from r5_common import *   # D, D2, FIT (=fit5), TGT, B4, O4, TEX, GLB, Tpl, umeyama, vertex_normals ...
B2 = B4; O2 = O4   # names kept from c2_* so the copied steps read unchanged

def raster_uv(faces, uv_loops, R):
    """UV-space rasteriser. Returns (fid (R,R) int32, -1 = empty; bary (R,R,3) float32). uv_loops: (F,3,2) in 0..1, v up."""
    pix = np.stack([uv_loops[..., 0] * R - 0.5, (1 - uv_loops[..., 1]) * R - 0.5], -1)
    fid = -np.ones((R, R), np.int32); bary = np.zeros((R, R, 3), np.float32)
    for f in range(len(faces)):
        p = pix[f]; x0, y0 = np.floor(p.min(0)).astype(int) - 1; x1, y1 = np.ceil(p.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, R - 1), min(y1, R - 1)
        if x1 < x0 or y1 < y0: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-9: continue
        l0 = ((p[1, 1] - p[2, 1]) * (gx - p[2, 0]) + (p[2, 0] - p[1, 0]) * (gy - p[2, 1])) / d
        l1 = ((p[2, 1] - p[0, 1]) * (gx - p[2, 0]) + (p[0, 0] - p[2, 0]) * (gy - p[2, 1])) / d
        l2 = 1 - l0 - l1; m = (l0 >= -1e-4) & (l1 >= -1e-4) & (l2 >= -1e-4)
        fid[gy[m], gx[m]] = f; bary[gy[m], gx[m]] = np.stack([l0, l1, l2], -1)[m]
    return fid, bary

def interp(fid, bary, faces, vals):
    """per-texel interpolation of a per-vertex attribute (V,k)."""
    vals = np.asarray(vals, np.float64); k = vals.shape[1] if vals.ndim > 1 else 1
    v = vals.reshape(len(vals), k)
    out = np.zeros(fid.shape + (k,), np.float64); m = fid >= 0
    tri = faces[fid[m]]
    out[m] = (v[tri] * bary[m][..., None]).sum(1)
    return out[..., 0] if vals.ndim == 1 else out

def depth_map(P, faces, px, py, R):
    """nearest-surface y (camera at -y looking +y) per screen pixel; px,py per-vertex screen coords (R x R)."""
    dm = np.full((R, R), np.inf, np.float32); y = P[:, 1]
    for f in range(len(faces)):
        i = faces[f]; xs = px[i]; ys = py[i]
        x0, x1 = int(max(np.floor(xs.min()) - 1, 0)), int(min(np.ceil(xs.max()) + 1, R - 1))
        y0, y1 = int(max(np.floor(ys.min()) - 1, 0)), int(min(np.ceil(ys.max()) + 1, R - 1))
        if x1 < x0 or y1 < y0: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-9: continue
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        l2 = 1 - l0 - l1; m = (l0 >= -1e-4) & (l1 >= -1e-4) & (l2 >= -1e-4)
        z = l0 * y[i[0]] + l1 * y[i[1]] + l2 * y[i[2]]
        sub = dm[y0:y1 + 1, x0:x1 + 1]; upd = m & (z < sub); sub[upd] = z[upd]
    return dm

def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
