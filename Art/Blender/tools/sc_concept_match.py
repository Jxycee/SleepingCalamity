# sc_concept_match.py (2026-10-25, asset skills): measures how close a model's rendered colours are to a crop of its concept painting. Plain Python 3
# with Pillow (no Blender), read-only for the repo.
#
# Run:  python3 Art/Blender/tools/sc_concept_match.py --targets '#6e6a5e,#586b73' <preview.png> [--panel n]   (props too small to crop)
#       python3 Art/Blender/tools/sc_concept_match.py <concept.jpg> <x0,y0,x1,y1 | x0%,y0%,x1%,y1%> <preview.png> [--panel 0..3] [--out <side_by_side.png>]
#   The concept crop is the asset in the painting (pixels, or percentages of the image). The preview is an sc_asset_preview.py sheet; --panel picks
#   its panel (0 three_quarter, 1 side, 2 back_three_quarter, 3 play_distance; default 0). Pixels close to the sheet's sky and ground colours are
#   masked out, so only the model is measured. In the concept crop everything counts (crop tightly).
# Reports, for concept vs model (sRGB, 0-255):
#   value: mean luminance, p10 / p50 / p90 (the light / mid / dark masses), contrast = p90 - p10
#   colour: mean saturation, the mean hue as a warm/cool score (+ warm, - cool), and the top 5 colour clusters with their shares
#   gaps: value shift, contrast ratio, saturation ratio, warm/cool shift, and a 0-100 match score (100 = same distribution)
# The score is a guide for hillclimbing (higher is closer). It is not a gate: a painting crop includes light, shadow and haze the model doesn't own.
import colorsys, json, math, sys

def pixels(im):
    return list(im.get_flattened_data() if hasattr(im, 'get_flattened_data') else im.getdata())
from PIL import Image

SKY = (0.74, 0.80, 0.82)

def parse_box(s, w, h):
    parts = s.split(',')
    if all(p.endswith('%') for p in parts):
        f = [float(p[:-1]) / 100 for p in parts]
        return int(f[0] * w), int(f[1] * h), int(f[2] * w), int(f[3] * h)
    return tuple(int(p) for p in parts)

def lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]

def pct(vals, q):
    v = sorted(vals); return v[min(len(v) - 1, int(q * len(v)))] if v else 0

def stats(px):
    L = [lum(c) for c in px]
    hs = [colorsys.rgb_to_hsv(*(x / 255 for x in c)) for c in px]
    sat = sum(h[1] for h in hs) / max(1, len(hs))
    # warm/cool: + for reds/oranges/yellows (hue 0-0.17 or > 0.92), - for greens-cyans-blues (0.4-0.75), weighted by saturation
    warm = sum((1 if (h[0] < 0.17 or h[0] > 0.92) else -1 if 0.4 < h[0] < 0.75 else 0) * h[1] for h in hs) / max(1, len(hs))
    q = {}
    for c in px:
        k = tuple((x // 32) * 32 + 16 for x in c); q[k] = q.get(k, 0) + 1
    top = sorted(q.items(), key=lambda kv: -kv[1])[:5]
    return dict(n=len(px), mean_value=round(sum(L) / max(1, len(L)), 1), p10=round(pct(L, 0.1), 1), p50=round(pct(L, 0.5), 1), p90=round(pct(L, 0.9), 1),
                contrast=round(pct(L, 0.9) - pct(L, 0.1), 1), saturation=round(sat, 3), warm=round(warm, 3),
                clusters=[('#%02x%02x%02x' % k, round(v / max(1, len(px)), 3)) for k, v in top])

def targets_mode(hexes, preview, rest):
    # For props too small to crop from a painting: score the model's colours against target hex values (the concept README's measured palette,
    # or values picked from the painting). Per target: the nearest model colour cluster and its distance; match = mean closeness (0-100).
    panel = int(rest[rest.index('--panel') + 1]) if '--panel' in rest else 0
    pi = Image.open(preview).convert('RGB'); W, H = pi.size; hw, hh = W // 2, H // 2
    ox, oy = (panel % 2) * hw, (panel // 2) * hh
    pan = pi.crop((ox, oy, ox + hw, oy + hh))
    bgs = [pan.getpixel((2, 2)), pan.getpixel((hw - 3, 2)), pan.getpixel((2, hh - 3)), pan.getpixel((hw - 3, hh - 3))]
    small = pan.resize((min(hw, 256), min(hh, 256)), Image.NEAREST)
    mp = [c for c in pixels(small) if not any(sum((c[i] - b[i]) ** 2 for i in range(3)) < 18 ** 2 for b in bgs)]
    model = stats(mp)
    out = []
    for h in hexes.split(','):
        h = h.strip().lstrip('#'); t = tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
        best = min(mp, key=lambda c: sum((c[i] - t[i]) ** 2 for i in range(3))) if mp else (0, 0, 0)
        d = math.sqrt(sum((best[i] - t[i]) ** 2 for i in range(3)))
        near = sum(1 for c in mp if sum((c[i] - t[i]) ** 2 for i in range(3)) < 30 ** 2) / max(1, len(mp))
        out.append(dict(target='#' + h, nearest='#%02x%02x%02x' % best, distance=round(d, 1), share_within_30=round(near, 3)))
    # a target only counts as met when its colour covers a real part of the model: closeness x min(1, share / 5%) (one stray pixel scores ~0)
    match = sum(max(0.0, 100.0 - t['distance'] * 1.5) * min(1.0, t['share_within_30'] / 0.05) for t in out) / max(1, len(out))
    print(json.dumps(dict(model=model, targets=out, match=round(match, 1)), indent=1))

def usage():
    # the Run lines of the header above
    with open(__file__, encoding='utf-8') as f:
        print(''.join(l[2:] for l in f.readlines()[2:12]), end='')

def main(argv):
    if not argv or argv[0] in ('-h', '--help') or (argv[0] != '--targets' and len(argv) < 3) or (argv[0] == '--targets' and len(argv) < 3):
        usage()
        return 0 if argv and argv[0] in ('-h', '--help') else 2
    if argv and argv[0] == '--targets':
        return targets_mode(argv[1], argv[2], argv[3:])
    concept, box, preview = argv[0], argv[1], argv[2]
    rest = argv[3:]
    panel = int(rest[rest.index('--panel') + 1]) if '--panel' in rest else 0
    out = rest[rest.index('--out') + 1] if '--out' in rest else None
    ci = Image.open(concept).convert('RGB')
    crop = ci.crop(parse_box(box, *ci.size))
    pi = Image.open(preview).convert('RGB')
    W, H = pi.size; hw, hh = W // 2, H // 2
    ox, oy = (panel % 2) * hw, (panel // 2) * hh
    pan = pi.crop((ox, oy, ox + hw, oy + hh))
    # mask the sheet's sky and ground: sample the panel's corners (sky top, ground bottom) and drop pixels near them
    bgs = [pan.getpixel((2, 2)), pan.getpixel((hw - 3, 2)), pan.getpixel((2, hh - 3)), pan.getpixel((hw - 3, hh - 3))]
    def bg(c):
        return any(sum((c[i] - b[i]) ** 2 for i in range(3)) < 18 ** 2 for b in bgs)
    small = pan.resize((min(hw, 256), min(hh, 256)), Image.NEAREST)
    mp = [c for c in pixels(small) if not bg(c)]
    cp = pixels(crop.resize((max(1, min(crop.width, 256)), max(1, min(crop.height, 256)))))
    a, b = stats(cp), stats(mp)
    gaps = dict(value_shift=round(b['mean_value'] - a['mean_value'], 1),
                contrast_ratio=round(b['contrast'] / max(1.0, a['contrast']), 2),
                saturation_ratio=round(b['saturation'] / max(1e-3, a['saturation']), 2),
                warm_shift=round(b['warm'] - a['warm'], 3))
    score = 100.0
    score -= min(35, abs(gaps['value_shift']) * 0.6)
    score -= min(25, abs(1 - gaps['contrast_ratio']) * 40)
    score -= min(25, abs(1 - gaps['saturation_ratio']) * 35)
    score -= min(15, abs(gaps['warm_shift']) * 60)
    gaps['match'] = round(max(0.0, score), 1)
    res = dict(concept=a, model=b, gaps=gaps)
    print(json.dumps(res, indent=1))
    if out:
        t = 256
        c2 = crop.copy(); c2.thumbnail((t, t)); p2 = pan.copy(); p2.thumbnail((t, t))
        sheet = Image.new('RGB', (2 * t, t), (40, 40, 40)); sheet.paste(c2, (0, 0)); sheet.paste(p2, (t, 0)); sheet.save(out)

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]) or 0)
