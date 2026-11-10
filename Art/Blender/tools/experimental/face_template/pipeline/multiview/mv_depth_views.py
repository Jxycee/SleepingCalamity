"""Depth (and silhouette) guide renders of a clay head from the turnaround angles, for SDXL + ControlNet-depth multi-view generation.
usage (trellis2 venv python):  python mv_depth_views.py <mesh.npz|template.npz> <out_dir> [--tile 576] [--views front,q34l,left,right,q34r,back,top]
Mesh: either a pipeline npz (v, f) or the Tobin template npz (head/basis + body/basis are used, Tobin 'basis' pose).
Own numpy z-buffer (no Blender): perspective camera, subject centre c0, distance D, vertical extent EXT metres at the subject. Depth: near = white, background = black (the
convention ControlNet-depth was trained on). Yaw convention = clay_views.py: yaw 0 front (camera at -y), +90 = subject's left side (nose points to image LEFT), -90 = right side.
Writes depth_<view>.png, sil_<view>.png, and guide_cam.json (camera so the silhouettes can be compared to generated views in the same frame)."""
import sys, os, json, argparse, numpy as np, cv2

C0 = np.array([0.0, -0.02, 1.64]); D = 1.25; EXT = 0.56
YAW = dict(front=0, q34l=40, left=90, right=-90, q34r=-40, back=180)


def load(path):
    d = np.load(path)
    if 'v' in d.files: return d['v'].astype(np.float64), d['f'].astype(np.int64)
    vs, fs, off = [], [], 0
    for k in ('head',):
        v, f = d[k + '/basis'], d[k + '/faces']; vs.append(v); fs.append(f + off); off += len(v)
    return np.concatenate(vs).astype(np.float64), np.concatenate(fs).astype(np.int64)


def camera(view, tile):
    if view == 'top':
        pos = C0 + np.array([0, 0, D]); fwd = np.array([0, 0, -1.0]); up = np.array([0, -1.0, 0])      # nose to image top
    else:
        th = np.radians(YAW[view]); pos = C0 + D * np.array([np.sin(th), -np.cos(th), 0]); fwd = C0 - pos; fwd /= np.linalg.norm(fwd); up = np.array([0, 0, 1.0])
    right = np.cross(fwd, up); right /= np.linalg.norm(right); up = np.cross(right, fwd)
    f = 0.5 * tile / (EXT / 2 / D)         # focal in px: EXT metres span the tile height at distance D
    return pos, fwd, right, up, f


def render(v, f, view, tile):
    pos, fwd, right, up, fl = camera(view, tile)
    rel = v - pos; z = rel @ fwd; x = rel @ right; y = rel @ up
    px = fl * x / z + tile / 2; py = tile / 2 - fl * y / z
    zb = np.full((tile, tile), np.inf)
    for t in f:
        a, b, c = t
        if min(z[a], z[b], z[c]) <= 0.05: continue
        xs = (px[a], px[b], px[c]); ys = (py[a], py[b], py[c])
        x0, x1 = int(max(np.floor(min(xs)), 0)), int(min(np.ceil(max(xs)), tile - 1)); y0, y1 = int(max(np.floor(min(ys)), 0)), int(min(np.ceil(max(ys)), tile - 1))
        if x0 > x1 or y0 > y1: continue
        den = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(den) < 1e-12: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        l0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / den
        l1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / den
        l2 = 1 - l0 - l1; m = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
        if not m.any(): continue
        zz = l0 * z[a] + l1 * z[b] + l2 * z[c]
        sub = zb[y0:y1 + 1, x0:x1 + 1]; upd = m & (zz < sub); sub[upd] = zz[upd]
    sil = np.isfinite(zb)
    dep = np.zeros((tile, tile), np.float32); zn, zf = D - 0.17, D + 0.22
    dep[sil] = 255 * np.clip((zf - zb[sil]) / (zf - zn), 0.08, 1.0) ** 1.0
    dep = cv2.GaussianBlur(dep, (0, 0), 0.8)
    return np.clip(dep, 0, 255).astype(np.uint8), (sil * 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('mesh'); ap.add_argument('out'); ap.add_argument('--tile', type=int, default=576)
    ap.add_argument('--views', default='front,q34l,left,right,q34r,back,top'); a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True); v, f = load(a.mesh)
    for view in a.views.split(','):
        dep, sil = render(v, f, view, a.tile)
        cv2.imwrite(f'{a.out}/depth_{view}.png', dep); cv2.imwrite(f'{a.out}/sil_{view}.png', sil); print('wrote', view, flush=True)
    json.dump(dict(c0=C0.tolist(), D=D, ext=EXT, tile=a.tile, yaw=YAW), open(a.out + '/guide_cam.json', 'w'))


if __name__ == '__main__':
    main()
