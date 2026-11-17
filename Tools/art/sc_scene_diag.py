# sc_scene_diag.py (2026-11-17, judging v2): objective image diagnostics that SUPPORT human / agent judging of whole-scene game screenshots
# (eye-level views of a town). It is never a score: it measures a few things a tired eye misses (flat depth, tiling, empty ground, no fine
# detail) so the judge can point at them. Read each number against a reference frame or an earlier round of the same camera, not in isolation.
#
# Run (plain python3 with Pillow + numpy; no scipy / cv2):
#   python3 Tools/art/sc_scene_diag.py <img> [<img> ...] [--json out.json] [--sheet out.png] [--horizon 0.45] [--bands 0.25,0.45,0.70]
#                                      [--empty-thr 0.012]
#   --bands: the three edges (fraction of frame height) between sky | far | mid | near. --horizon replaces only the far/mid edge.
#
# What it prints per image (all luminance is Rec.709 on sRGB-decoded values, reported sRGB-encoded 0-1):
#   bands     p10 / p50 / p90 per band, plus saturation. NOTE: bands are IMAGE ROWS, not a depth buffer. A tall tree or a hill breaks the
#             assumption; read the depth figures as a proxy for "does the picture layer from near to far".
#   hist      whole-frame luminance histogram, 16 bins. clip = share of pixels > 0.98, crush = share < 0.02 (blown sky, crushed shadow).
#   depth     step between consecutive bands: dV = difference of medians, dC = ratio of (p90-p10) spreads (next / previous).
#             haze_c = 1 - far spread / near spread, haze_s = 1 - far saturation / near saturation (aerial perspective: far lower contrast and
#             colour than near). Big (0.3+) = layered depth, ~0 = flat, negative = far is punchier than near.
#   tiling    near and mid bands: luminance with the low-frequency trend removed, FFT autocorrelation, highest secondary peak outside a 16 px
#             radius (local maximum), normalised to the zero-lag value (0-1) with its offset (dx,dy) in px. High (0.3+) with the same offset
#             in several frames = a visibly repeating texture. Low (<0.15) = no obvious repeat.
#   empty     frame split 32x18; a cell is empty when the std of its high-passed luminance is under --empty-thr (default 0.012). The sky band
#             is skipped. Share of empty cells in mid and near, and the largest 4-connected empty patch as a share of the whole frame.
#             High = bare ground / flat walls with nothing to read (undressed areas).
#   spectrum  radial power spectrum of the Hann-windowed luminance: share of energy low (<1/32 cyc/px), mid (1/32-1/8), high (>1/8), and the
#             high share per band (far / mid / near). Low high-share = soft, blurry or unspoilt-by-detail image; a far high share that exceeds
#             near's usually means noise or aliasing, not detail.

import json, sys
import numpy as np
from PIL import Image, ImageDraw

def opt(name, default, cast=str):
    return cast(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default

def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)

def box_blur(a, r):
    # mean over a (2r+1)^2 window via 2D cumulative sums, edge-replicated
    k = 2 * r + 1
    c = np.pad(a, ((r, r), (r, r)), mode='edge').cumsum(0).cumsum(1)
    c = np.pad(c, ((1, 0), (1, 0)))
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)

def pct(v, qs=(10, 50, 90)):
    return [float(x) for x in np.percentile(v, qs)]

def band_slices(h, edges):
    ys = [0] + [int(round(e * h)) for e in edges] + [h]
    return dict(zip(('sky', 'far', 'mid', 'near'), [(ys[i], ys[i + 1]) for i in range(4)]))

def tiling(lum, rmin=16):
    x = lum - box_blur(lum, 32)
    x = x - x.mean()
    h, w = x.shape
    F = np.fft.fft2(x, s=(2 * h, 2 * w))
    ac = np.fft.fftshift(np.fft.ifft2(np.abs(F) ** 2).real)
    cy, cx = h, w
    ac = ac / max(ac[cy, cx], 1e-12)
    ly, lx = h // 3, w // 3
    win = ac[cy - ly:cy + ly + 1, cx - lx:cx + lx + 1]
    p = np.pad(win, 2, constant_values=-1)
    is_max = np.ones(win.shape, bool)
    for dy in range(-2, 3):
        for dx in range(-2, 3):
            if dy or dx:
                is_max &= win >= p[2 + dy:2 + dy + win.shape[0], 2 + dx:2 + dx + win.shape[1]]
    yy, xx = np.mgrid[-ly:ly + 1, -lx:lx + 1]
    ok = is_max & (yy ** 2 + xx ** 2 > rmin ** 2)
    if not ok.any():
        return {'peak': 0.0, 'dx': 0, 'dy': 0}
    v = np.where(ok, win, -1)
    i = np.unravel_index(np.argmax(v), v.shape)
    return {'peak': float(max(v[i], 0)), 'dx': int(xx[i]), 'dy': int(yy[i])}

def spectrum(lum):
    h, w = lum.shape
    x = (lum - lum.mean()) * np.outer(np.hanning(h), np.hanning(w))
    P = np.abs(np.fft.fft2(x)) ** 2
    f = np.sqrt(np.fft.fftfreq(h)[:, None] ** 2 + np.fft.fftfreq(w)[None, :] ** 2)
    P[0, 0] = 0
    t = max(P.sum(), 1e-12)
    return {'low': float(P[f < 1 / 32].sum() / t), 'mid': float(P[(f >= 1 / 32) & (f <= 1 / 8)].sum() / t),
            'high': float(P[f > 1 / 8].sum() / t)}

def empty_cells(lum, edges, thr):
    H, W = lum.shape
    gx, gy = 32, 18
    hp = lum - box_blur(lum, 4)
    ch, cw = H // gy, W // gx
    sd = hp[:ch * gy, :cw * gx].reshape(gy, ch, gx, cw).std(axis=(1, 3))
    empty = sd < thr
    rows = (np.arange(gy) + 0.5) / gy
    notsky = rows >= edges[0]
    empty &= notsky[:, None]
    res = {}
    for name, lo, hi in (('mid', edges[1], edges[2]), ('near', edges[2], 1.0)):
        m = (rows >= lo) & (rows < hi)
        res[name] = float(empty[m].mean()) if m.any() else 0.0
    seen = np.zeros_like(empty)
    best = 0
    for sy in range(gy):
        for sx in range(gx):
            if empty[sy, sx] and not seen[sy, sx]:
                stack, n = [(sy, sx)], 0
                seen[sy, sx] = True
                while stack:
                    y, x = stack.pop()
                    n += 1
                    for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
                        if 0 <= ny < gy and 0 <= nx < gx and empty[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))
                best = max(best, n)
    res['largest_patch'] = best / (gx * gy)
    return res

def analyse(path, edges, thr):
    rgb = np.asarray(Image.open(path).convert('RGB'), dtype=np.float64) / 255.0
    lin = srgb_to_lin(rgb)
    lum = lin_to_srgb(lin @ np.array([0.2126, 0.7152, 0.0722]))
    mx, mn = rgb.max(2), rgb.min(2)
    sat = np.where(mx > 1e-6, (mx - mn) / np.maximum(mx, 1e-6), 0)
    H = lum.shape[0]
    sl = band_slices(H, edges)
    R = {'file': str(path), 'size': [rgb.shape[1], H], 'edges': list(edges), 'bands': {}}
    for k, (a, b) in sl.items():
        p10, p50, p90 = pct(lum[a:b])
        R['bands'][k] = {'p10': p10, 'p50': p50, 'p90': p90, 'spread': p90 - p10, 'sat': float(sat[a:b].mean())}
    R['hist16'] = [float(v) for v in np.histogram(lum, bins=16, range=(0, 1))[0] / lum.size]
    R['clip'] = float((lum > 0.98).mean())
    R['crush'] = float((lum < 0.02).mean())
    names = ['sky', 'far', 'mid', 'near']
    R['depth'] = {'steps': {}}
    for a, b in zip(names[:-1], names[1:]):
        A, B = R['bands'][a], R['bands'][b]
        R['depth']['steps'][a + '>' + b] = {'dV': B['p50'] - A['p50'], 'dC': B['spread'] / max(A['spread'], 1e-6)}
    f, n = R['bands']['far'], R['bands']['near']
    R['depth']['haze_c'] = 1 - f['spread'] / max(n['spread'], 1e-6)
    R['depth']['haze_s'] = 1 - f['sat'] / max(n['sat'], 1e-6)
    R['tiling'] = {k: tiling(lum[slice(*sl[k])]) for k in ('mid', 'near')}
    R['empty'] = empty_cells(lum, edges, thr)
    R['spectrum'] = spectrum(lum)
    R['spectrum']['high_by_band'] = {k: spectrum(lum[slice(*sl[k])])['high'] for k in ('far', 'mid', 'near')}
    return R

def block(R):
    B, d, t, e, s = R['bands'], R['depth'], R['tiling'], R['empty'], R['spectrum']
    out = [f"== {R['file']}  {R['size'][0]}x{R['size'][1]}  (bands are image rows, not a depth buffer)"]
    for k in ('sky', 'far', 'mid', 'near'):
        b = B[k]
        out.append(f"  {k:<5} lum p10/50/90 {b['p10']:.3f}/{b['p50']:.3f}/{b['p90']:.3f}  spread {b['spread']:.3f}  sat {b['sat']:.3f}")
    out.append('  hist16 ' + ' '.join(f'{v * 100:.0f}' for v in R['hist16']) + f"   clip>0.98 {R['clip']:.3f}  crush<0.02 {R['crush']:.3f}")
    out.append('  depth  ' + '  '.join(f"{k} dV {v['dV']:+.3f} dC x{v['dC']:.2f}" for k, v in d['steps'].items()))
    out.append(f"  haze   contrast {d['haze_c']:+.2f}  saturation {d['haze_s']:+.2f}  (big = layered, ~0 = flat)")
    out.append('  tiling ' + '  '.join(f"{k} {v['peak']:.2f} @({v['dx']},{v['dy']})" for k, v in t.items()))
    out.append(f"  empty  mid {e['mid']:.2f}  near {e['near']:.2f}  largest patch {e['largest_patch']:.3f} of frame")
    out.append(f"  freq   low {s['low']:.3f} mid {s['mid']:.3f} high {s['high']:.4f}   high by band " +
               ' '.join(f"{k} {v:.4f}" for k, v in s['high_by_band'].items()))
    return '\n'.join(out)

def sheet(results, out):
    W, Hh, TW = 1000, 360, 640
    im = Image.new('RGB', (W, Hh * len(results)), (24, 24, 24))
    dr = ImageDraw.Draw(im)
    for i, R in enumerate(results):
        im.paste(Image.open(R['file']).convert('RGB').resize((TW, Hh), Image.LANCZOS), (0, i * Hh))
        B, d, t, e, s = R['bands'], R['depth'], R['tiling'], R['empty'], R['spectrum']
        lines = [R['file'].replace('\\', '/').split('/')[-1], '',
                 'band median lum / sat'] + [f"  {k:<5} {B[k]['p50']:.3f} / {B[k]['sat']:.3f}" for k in ('sky', 'far', 'mid', 'near')] + [
                 '', f"haze contrast {d['haze_c']:+.2f}  sat {d['haze_s']:+.2f}",
                 f"tiling mid {t['mid']['peak']:.2f}  near {t['near']['peak']:.2f}",
                 f"  near offset ({t['near']['dx']},{t['near']['dy']})",
                 f"empty mid {e['mid']:.2f}  near {e['near']:.2f}",
                 f"  largest patch {e['largest_patch']:.3f}",
                 f"HF share {s['high']:.4f}", '  ' + ' '.join(f"{k} {v:.4f}" for k, v in s['high_by_band'].items()),
                 f"clip {R['clip']:.3f}  crush {R['crush']:.3f}", '', 'diagnostics only, not a score']
        for j, ln in enumerate(lines):
            dr.text((TW + 10, i * Hh + 8 + j * 14), ln, fill=(230, 230, 230))
    im.save(out)

def main():
    flags = {'--json', '--sheet', '--horizon', '--bands', '--empty-thr'}
    args, skip = [], False
    for i, a in enumerate(sys.argv[1:], 1):
        if a in flags:
            skip = True
        elif skip:
            skip = False
        else:
            args.append(a)
    if not args:
        sys.exit('usage: sc_scene_diag.py <img> ... [--json f] [--sheet f]')
    edges = [float(v) for v in opt('--bands', '0.25,0.45,0.70').split(',')]
    if '--horizon' in sys.argv:
        edges[1] = opt('--horizon', 0.45, float)
    thr = opt('--empty-thr', 0.012, float)
    results = [analyse(p, edges, thr) for p in args]
    for R in results:
        print(block(R))
    if '--json' in sys.argv:
        with open(opt('--json', ''), 'w') as f:
            json.dump(results, f, indent=1)
    if '--sheet' in sys.argv:
        sheet(results, opt('--sheet', ''))

if __name__ == '__main__':
    main()
