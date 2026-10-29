# sc_ref_palette.py (2026-10-25, asset skills): turn reference images (or a crop of a concept painting) into the numbers the asset skills
# grade against - the main colour masses with their coverage, the value structure (p10 / p50 / p90 as sRGB value), and a ready --targets string
# for sc_concept_match.py. Use it when the concepts don't show the asset (sc-reference): measure 3-6 references, keep the masses they agree on.
#
# Run (plain python3 with Pillow + numpy):
#   python3 Art/Blender/tools/sc_ref_palette.py <img> [<img> ...] [--crop x0%,y0%,x1%,y1%] [--crops crops.json] [--exclude '#hex,...'] [--exclude-de 30] [--k 6]
#                                               [--sheet <out.png>] [--json <out.json>]
#   --crops: a JSON object {file name (or path): "x0,y0,x1,y1"} in percent, a crop per image (overrides --crop for the images it names).
#   --exclude: colours that are not the subject (grass, sky, carpet): the mass nearest each one (within --exclude-de, default 30) is dropped
#   from the targets string (it still shows, marked x, in the masses line), unless that mass is closer to another kept mass than to the
#   excluded colour (a heron's black next to a dark-green background: round 28). Pool only references of the brief's own variant (varnished vs painted boats).
#   e.g. python3 Art/Blender/tools/sc_ref_palette.py refs/willow/*.jpg --crop 20,10,80,90 --sheet scratch/willow_palette.png
#
# Per image and pooled over all images it prints:
#   masses   k-means colours (Lab, farthest-point init, deterministic) sorted by coverage, as hex + share
#   value    p10 / p50 / p90 of sRGB value (max channel), the spread (the rubric's value line), and the warm/cool shift of the lights vs the darks
#   targets  the masses covering >= 5% (sc_concept_match's floor), as a --targets string
# Photos are brighter, more saturated and noisier than the Riverlight paintings: read the masses as hue and value *relations* (moss darker and
# greener than the stone, the lit leaves warmer than the shaded ones), and pull saturation toward the concept README palette before tinting.
# The sheet shows each image beside its palette bars, so a stray mass (sky, a person, a sign) is easy to spot and crop out.

import json, signal, sys
import numpy as np

def opt(name, default, cast=str):
    return cast(sys.argv[sys.argv.index(name) + 1]) if name in sys.argv else default

def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)

def to_lab(rgb):
    lin = srgb_to_lin(rgb)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ M.T / np.array([0.9505, 1.0, 1.089])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[:, 1] - 16, 500 * (f[:, 0] - f[:, 1]), 200 * (f[:, 1] - f[:, 2])], 1)

def kmeans(X, k, iters=20):
    # farthest-point init (deterministic, no empty clusters), then Lloyd
    C = [X[np.argmin(np.abs(X[:, 0] - np.median(X[:, 0])))]]
    for _ in range(1, k):
        d = np.min(((X[:, None, :] - np.array(C)[None]) ** 2).sum(2), 1)
        C.append(X[np.argmax(d)])
    C = np.array(C)
    for _ in range(iters):
        lab = np.argmin(((X[:, None, :] - C[None]) ** 2).sum(2), 1)
        C = np.array([X[lab == j].mean(0) if np.any(lab == j) else C[j] for j in range(k)])
    return lab, C

def load(path, crop):
    from PIL import Image
    im = Image.open(path).convert('RGB')
    if crop:
        w, h = im.size
        x0, y0, x1, y1 = (float(v) / 100 for v in crop.split(','))
        im = im.crop((int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h)))
    im.thumbnail((256, 256))
    return im, np.asarray(im, dtype=np.float32).reshape(-1, 3) / 255.0

def analyse(rgb, k, exclude=(), exclude_de=30.0):
    X = to_lab(rgb)
    lab, _ = kmeans(X, k)
    masses = []
    for j in range(k):
        m = lab == j
        if not np.any(m):
            continue
        c = rgb[m].mean(0)
        masses.append(('#%02x%02x%02x' % tuple(int(round(v * 255)) for v in c), float(m.mean()), float(c.max())))
    masses.sort(key=lambda t: -t[1])
    dropped = set()
    if exclude:
        mlab = to_lab(np.array([[int(h[i:i + 2], 16) / 255 for i in (1, 3, 5)] for h, _, _ in masses]))
        for e in exclude:
            el = to_lab(np.array([[int(e.lstrip('#')[i:i + 2], 16) / 255 for i in (0, 2, 4)]]))[0]
            d = np.sqrt(((mlab - el) ** 2).sum(1))
            j = int(np.argmin(d))
            if d[j] >= exclude_de:
                continue
            # guard: keep it when another mass (not itself excluded) is nearer to it than the excluded colour: it is part of the subject's range
            others = [np.sqrt(((mlab[j] - mlab[q]) ** 2).sum()) for q in range(len(masses)) if q != j and masses[q][0] not in dropped]
            if others and min(others) < d[j]:
                continue
            dropped.add(masses[j][0])
    val = rgb.max(1)
    p10, p50, p90 = (float(np.percentile(val, q)) for q in (10, 50, 90))
    hi, lo = rgb[val >= np.percentile(val, 80)].mean(0), rgb[val <= np.percentile(val, 20)].mean(0)
    warm = float((hi[0] - hi[2]) - (lo[0] - lo[2]))   # > 0: lights warmer than darks (sunlit), < 0: cool lights
    return {'masses': masses, 'p10': p10, 'p50': p50, 'p90': p90, 'spread': p90 - p10, 'light_warmth': warm,
            'targets': ','.join(h for h, s, _ in masses if s >= 0.05 and h not in dropped), 'excluded': sorted(dropped)}

def report(name, a):
    print('== %s' % name)
    print('  masses  ' + '  '.join('%s %4.1f%%%s' % (h, 100 * s, ' x' if h in a.get('excluded', ()) else '') for h, s, _ in a['masses']))
    print('  value   p10 %.2f  p50 %.2f  p90 %.2f  spread %.0f (x100)   lights %s than darks (%+.2f)'
          % (a['p10'], a['p50'], a['p90'], 100 * a['spread'], 'warmer' if a['light_warmth'] > 0 else 'cooler', a['light_warmth']))
    print('  targets --targets \'%s\'' % a['targets'])

def main():
    paths = [a for a in sys.argv[1:] if not a.startswith('--') and a not in {sys.argv[i + 1] for i, x in enumerate(sys.argv[:-1]) if x.startswith('--')}]
    if not paths or '-h' in sys.argv or '--help' in sys.argv:
        print(open(__file__).read().split('\n\n')[0]); sys.exit(0 if ('-h' in sys.argv or '--help' in sys.argv) else 2)
    crop, k = opt('--crop', ''), opt('--k', 6, int)
    import os
    crops = json.load(open(opt('--crops', ''))) if '--crops' in sys.argv else {}
    excl = [e for e in opt('--exclude', '').split(',') if e]
    exde = opt('--exclude-de', 30.0, float)
    ims, pooled, out = [], [], {}
    for p in paths:
        im, rgb = load(p, crops.get(p) or crops.get(os.path.basename(p)) or crop)
        a = analyse(rgb, k, excl, exde); report(p, a); out[p] = a
        ims.append((im, a)); pooled.append(rgb)
    if len(paths) > 1:
        a = analyse(np.concatenate(pooled), k, excl, exde); report('POOLED (%d images)' % len(paths), a); out['pooled'] = a
    if '--json' in sys.argv:
        json.dump(out, open(opt('--json', ''), 'w'), indent=1)
    if '--sheet' in sys.argv:
        from PIL import Image, ImageDraw
        rowh, barw = 200, 120
        sheet = Image.new('RGB', (rowh * 2 + barw, rowh * len(ims)), (40, 40, 40))
        for i, (im, a) in enumerate(ims):
            t = im.copy(); t.thumbnail((rowh * 2, rowh)); sheet.paste(t, (0, i * rowh))
            d, y = ImageDraw.Draw(sheet), i * rowh
            for h, s, _ in a['masses']:
                hh = max(1, int(round(s * rowh)))
                d.rectangle([rowh * 2, y, rowh * 2 + barw, y + hh], fill=h); y += hh
        sheet.save(opt('--sheet', ''))
        print('sheet ->', opt('--sheet', ''))

if __name__ == '__main__':
    if hasattr(signal, "SIGPIPE"):
        signal.signal(signal.SIGPIPE, signal.SIG_DFL)   # quiet under | head
    main()
