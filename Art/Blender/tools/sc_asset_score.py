# sc_asset_score.py (2026-10-25, asset skills): measurable rubric lines for a model, graded against the SHIPPED library. Plain Python 3 + Pillow +
# numpy (no Blender). The duo hillclimb found self-scored rubrics plateau at ~32/40 on one-point judgement calls; these numbers don't drift.
#
# Run:  python3 Art/Blender/tools/sc_asset_score.py <sheet.png> [--class prop|building|outbuilding|tree|run|creature|cover] [--audit audit.json] [--tris N] [--length m]
#       python3 Art/Blender/tools/sc_asset_score.py --baseline <dir of sheets> <out baseline.json> [audit.json]   (rebuild a baseline)
# Baselines: props 114, buildings 43 (base+_Tri joined), trees/bushes 13 (_Tri+_Cards joined), all Millpond, 2026-10-25.
# <sheet.png> is an sc_asset_preview.py sheet (--size 512, the review default; other sizes are resampled) of ONE model (halves joined with +), default options (paint 1, grain 1, no --ref).
# Metrics (model pixels only; the sheet's sky and ground are masked out):
#   silhouette  the better grade of (a) openness: outline complexity of the 48 px mask, perimeter^2 / (4 pi area), and (b) breaks: dents in the
#               outline vs its convex hull of at least 2 x 2 px (compact solids read by these: a tap, a pipe, a leaning tool)
#   value       luminance spread p90 - p10 in the three close panels (the light / mid / dark masses)
#   painterly   brush-scale variation INSIDE surfaces: band-pass (blur 1.5 - blur 6 on the 2x-downsampled panel) std, strongest 15% of edges
#               excluded; face-level confetti (1-2 px) is averaged away first, so it no longer scores
#   efficiency  model tris (from --audit, base + _Tri summed, or --tris) / the class's per-model median: <=1x 5, <=1.5x 4, <=2x 3, <=3x 2, else 1;
#               grade_soft interpolates between those steps (one decimal) so a few tris either side of a step don't flip a whole grade
#               class run (walls, fences, edges laid in modules): tris per metre of run (--length, else the audit's longest horizontal size) /
#               the shipped runs' median per metre (sc_asset_score_baseline_run.json); value, painterly and silhouette against the props
# painterly also reports the confetti and the discount it applied (confetti > 0.35 scales painterly by 0.35 / confetti).
# Each is graded 1-5 by its percentile among the shipped Millpond props (sc_asset_score_baseline.json next to this file): 5 = top 15%,
# 4 = 60-85th, 3 = 40-60th (the typical shipped asset), 2 = 20-40th, 1 = bottom 20%. Lines 2, 4, 6, 7 of the rubric stay judged by eye
# (proportion, colour vs concept: use sc_concept_match --targets, form, wear).
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
BASELINE = os.path.join(HERE, 'sc_asset_score_baseline.json')                 # props (114)
BASELINES = {'prop': BASELINE, 'building': os.path.join(HERE, 'sc_asset_score_baseline_building.json'),   # buildings (43)
             'tree': os.path.join(HERE, 'sc_asset_score_baseline_tree.json'),                             # trees and bushes (Tri + Cards)
             'outbuilding': os.path.join(HERE, 'sc_asset_score_baseline_outbuilding.json'),               # walk-in outbuildings under 3 m (its note says how it was made)
             'run': BASELINE,                                                                              # modular runs: the props' looks, per-metre tris below
             'creature': BASELINE,
             'cover': BASELINE}                                                                            # shore / ground plants: the props' looks, cover tris below                                                                         # animals and birds: the props' looks, creature tris below                                                                              # modular runs: the props' looks, per-metre tris below
RUN_BASELINE = os.path.join(HERE, 'sc_asset_score_baseline_run.json')
CREATURE_BASELINE = os.path.join(HERE, 'sc_asset_score_baseline_creature.json')   # the shipped Life animals' per-model tris (round 28)
COVER_BASELINE = os.path.join(HERE, 'sc_asset_score_baseline_cover.json')         # the shipped cover kinds' per-model tris (round 30)

def panels(path):
    im = Image.open(path).convert('RGB')
    if im.size != (1024, 1024):   # the baseline is --size 512 (a 1024 sheet): measure every sheet at that scale
        im = im.resize((1024, 1024), Image.LANCZOS)
    W, H = im.size; hw, hh = W // 2, H // 2
    return [im.crop(((k % 2) * hw, (k // 2) * hh, (k % 2) * hw + hw, (k // 2) * hh + hh)) for k in range(4)]

def mask_of(p):
    a = np.asarray(p, dtype=np.float32)
    h, w, _ = a.shape
    sky = a[2, w // 2]; ground = a[h - 3, w // 2]   # centre samples: the ground disc can end before the panel's bottom corners
    side = a[int(h * 0.8), 3]
    if np.sqrt(((ground - sky) ** 2).sum()) < np.sqrt(((side - sky) ** 2).sum()) - 18:
        # the disc ended above the bottom row (a small model's play-distance panel: duo round 27, the heron), so that sample hit sky or the
        # horizon blend and the whole disc counted as model; take the ground from the side at 80% height (unchanged where the old one was ground)
        ground = side
    # background = any colour on (or near) the segment sky -> ground in RGB: the horizon is anti-aliased into blends of the two
    d = ground - sky; L2 = float((d * d).sum()) + 1e-6
    t = np.clip(((a - sky) * d).sum(-1) / L2, 0.0, 1.0)
    near = a - (sky + t[..., None] * d)
    m = np.sqrt((near ** 2).sum(-1)) > 18
    for b in (a[2, 2], a[2, w - 3], a[h - 3, 2], a[h - 3, w - 3]):   # and whatever the corners show (the world colour beyond the disc)
        m &= np.sqrt(((a - b) ** 2).sum(-1)) > 18
    return a, m

def lum(a):
    return a @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

def hull(points):
    # Andrew's monotone chain on (x, y) integer points
    pts = sorted(set(points))
    if len(pts) < 3: return pts
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], p) <= 0: lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cross(up[-2], up[-1], p) <= 0: up.pop()
        up.append(p)
    return lo[:-1] + up[:-1]

def breaks(m):
    # outline breaks at play distance: connected dents between the mask and its convex hull at least 2 x 2 px (smaller ones a player
    # can't see), counted. A barrel + downpipe + tap + can scores here even though it is compact (duo round 7, M1).
    ys, xs = np.nonzero(m)
    if len(xs) < 6: return 0
    hp = hull(list(zip(xs.tolist(), ys.tolist())))
    if len(hp) < 3: return 0
    im = Image.new('L', (m.shape[1], m.shape[0]), 0); ImageDraw.Draw(im).polygon(hp, fill=1)
    dent = (np.asarray(im, bool) & ~m)
    # 2 x 2 erosion keeps only dents a player could see
    core = dent & np.roll(dent, 1, 0) & np.roll(dent, 1, 1) & np.roll(np.roll(dent, 1, 0), 1, 1)
    seen = np.zeros_like(core); count = 0
    for y, x in zip(*np.nonzero(core)):
        if seen[y, x]: continue
        count += 1; stack = [(y, x)]; seen[y, x] = True
        while stack:
            cy, cx = stack.pop()
            for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                if 0 <= ny < core.shape[0] and 0 <= nx < core.shape[1] and core[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True; stack.append((ny, nx))
    return count

def metrics(path):
    P = panels(path)
    # silhouette: the play-distance panel is upscaled nearest; measure at its native-ish resolution by downsampling 4x
    pd = P[3]; small = pd.resize((pd.width // 4, pd.height // 4), Image.NEAREST)
    _, m = mask_of(small)
    area = m.sum()
    if area < 4:
        sil = 0.0
    else:
        edge = (m & ~np.roll(m, 1, 0)) | (m & ~np.roll(m, -1, 0)) | (m & ~np.roll(m, 1, 1)) | (m & ~np.roll(m, -1, 1))
        sil = float(edge.sum() ** 2 / (4 * math.pi * area))
    brk = breaks(m) if area >= 4 else 0
    vals, paint, conf = [], [], []
    for p in P[:3]:
        a, m = mask_of(p)
        if m.sum() < 50: continue
        L = lum(a)
        vals.append(float(np.percentile(L[m], 90) - np.percentile(L[m], 10)))
        # painterly = variation at BRUSH scale, not pixel noise (duo round 7, M7: per-face confetti scored 5). Band-pass on the 2x-downsampled
        # panel: blur 1.5 px minus blur 6 px keeps patches of about 3-12 px (6-24 px at 512), so face-level speckle (1-2 px) is averaged away
        # before it is measured.
        half = Image.fromarray(L.astype(np.uint8)).resize((L.shape[1] // 2, L.shape[0] // 2), Image.BOX)
        b1 = np.asarray(half.filter(ImageFilter.GaussianBlur(1.5)), dtype=np.float32)
        b6 = np.asarray(half.filter(ImageFilter.GaussianBlur(6)), dtype=np.float32)
        band = b1 - b6
        mh = np.asarray(Image.fromarray(m.astype(np.uint8) * 255).resize(half.size, Image.NEAREST)) > 127
        gy, gx = np.gradient(b6)
        g = np.hypot(gx, gy)
        inner = mh & (g < np.percentile(g[mh], 85)) if mh.sum() > 10 else mh
        # erode the mask so silhouette edges against the background don't count
        er = inner & np.roll(mh, 3, 0) & np.roll(mh, -3, 0) & np.roll(mh, 3, 1) & np.roll(mh, -3, 1)
        if er.sum() > 30:
            paint.append(float(band[er].std()))
            # confetti: fine (1 px) contrast relative to the brush band, at full resolution. Coherent painting ~0.2-0.33; a per-face mosaic
            # ~0.4+ (the duo's adopted thatch: 0.42 at 3 tones per region, 0.23 at 1)
            imL = Image.fromarray(L.astype(np.uint8))
            fine = L - np.asarray(imL.filter(ImageFilter.GaussianBlur(1)), dtype=np.float32)
            bnd = np.asarray(imL.filter(ImageFilter.GaussianBlur(3)), dtype=np.float32) - np.asarray(imL.filter(ImageFilter.GaussianBlur(12)), dtype=np.float32)
            erf = m & np.roll(m, 3, 0) & np.roll(m, -3, 0) & np.roll(m, 3, 1) & np.roll(m, -3, 1)
            if erf.sum() > 30: conf.append(float(fine[erf].std() / max(bnd[erf].std(), 1e-3)))
    pt = float(np.mean(paint)) if paint else 0.0
    cf = float(np.mean(conf)) if conf else 0.0
    disc = 0.35 / cf if cf > 0.35 else 1.0
    pt *= disc                             # confetti is not painting: discount it (M7)
    return dict(silhouette=round(sil, 3), breaks=brk, value=round(float(np.mean(vals)) if vals else 0.0, 2), painterly=round(pt, 3), confetti=round(cf, 3),
                discount=round(disc, 3))

def soft_eff(r):
    # efficiency as a soft band (duo round 26): the same steps as the integer grade (<= 1x 5, 1.5x 4, 2x 3, 3x 2, 4x 1), interpolated between
    # them, so 1.49x and 1.51x read 4.0 and 4.0, not 4 and 3. The rubric keeps the integer grade; this is reported beside it.
    pts = [(1.0, 5.0), (1.5, 4.0), (2.0, 3.0), (3.0, 2.0), (4.0, 1.0)]
    if r <= 1.0: return 5.0
    for (r0, g0), (r1, g1) in zip(pts, pts[1:]):
        if r <= r1: return round(g0 + (g1 - g0) * (r - r0) / (r1 - r0), 1)
    return 1.0

def grade(x, dist, higher_better=True):
    d = sorted(dist)
    if not d: return 3, 50.0
    pct = 100.0 * sum(1 for v in d if v < x) / len(d)
    if not higher_better: pct = 100.0 - pct
    g = 1 if pct < 20 else 2 if pct < 40 else 3 if pct < 60 else 4 if pct < 85 else 5
    return g, round(pct, 1)

def main(argv):
    if argv and argv[0] == '--baseline':
        d, out = argv[1], argv[2]
        rows = {}
        for f in sorted(os.listdir(d)):
            if f.endswith('.png'):
                rows[f[:-4]] = metrics(os.path.join(d, f))
        tris = {}
        if len(argv) > 3:   # optional audit JSON of the same models, for the per-model tris distribution
            for r in json.load(open(argv[3])):
                k = r['file'].replace('_Tri.fbx', '.fbx').replace('_Cards.fbx', '.fbx')[:-4]; tris[k] = tris.get(k, 0) + r['tris']
        json.dump(dict(models=rows, tris=tris), open(out, 'w'), indent=1)
        print('BASELINE', len(rows), 'models ->', out); return
    sheet = argv[0]; rest = argv[1:]
    cls = rest[rest.index('--class') + 1] if '--class' in rest else 'prop'
    cls = {'card': 'tree', 'rock': 'prop', 'hero': 'prop'}.get(cls, cls)
    base = json.load(open(BASELINES[cls]))
    m = metrics(sheet)
    tris = None
    if '--tris' in rest: tris = int(rest[rest.index('--tris') + 1])
    length = float(rest[rest.index('--length') + 1]) if '--length' in rest else None
    if '--audit' in rest:
        rows = json.load(open(rest[rest.index('--audit') + 1]))
        tris = sum(r['tris'] for r in rows)
        if length is None and rows and all('size' in r for r in rows):
            length = max(max(r['size'][0], r['size'][1]) for r in rows)   # Blender axes: x, y horizontal
    dist = {k: [v.get(k, 0) for v in base['models'].values()] for k in ('silhouette', 'breaks', 'value', 'painterly')}
    out = {}
    for k in ('value', 'painterly'):
        g, pct = grade(m[k], dist[k]); out[k] = dict(metric=m[k], percentile=pct, grade=g)
    out['painterly'].update(confetti=m['confetti'], discount=m['discount'])   # the confetti limiter, visible on every run
    # silhouette: the better of openness (outline complexity) and outline breaks (hull dents >= 2 px): a compact solid reads by its breaks,
    # a framework by its openness (duo round 7, M1)
    g1, p1 = grade(m['silhouette'], dist['silhouette']); g2, p2 = grade(m['breaks'], dist['breaks'])
    out['silhouette'] = dict(complexity=m['silhouette'], breaks=m['breaks'], percentile=max(p1, p2), grade=max(g1, g2))
    if tris is not None and cls == 'run':
        if not length:
            raise SystemExit('sc_asset_score: class run needs the run length (--length m, or an --audit with sizes)')
        per = sorted(json.load(open(RUN_BASELINE))['tris_per_m'].values())
        med = per[len(per) // 2]
        r = tris / length / max(med, 1)
        g = 5 if r <= 1.0 else 4 if r <= 1.5 else 3 if r <= 2.0 else 2 if r <= 3.0 else 1
        out['efficiency'] = dict(tris=tris, length=round(length, 2), tris_per_m=round(tris / length, 1), class_median_per_m=med, ratio=round(r, 2), grade=g,
                                 grade_soft=soft_eff(r))
    elif tris is not None and (base.get('tris') or cls in ('creature', 'cover')):
        if cls in ('creature', 'cover'):
            base = dict(base, tris=json.load(open(CREATURE_BASELINE if cls == 'creature' else COVER_BASELINE))['tris'])
        # efficiency by the ratio to the class's per-model median, not by percentile: detail on the silhouette must not cost a grade per break
        # (M3). <= 1x median 5, <= 1.5x 4, <= 2x 3, <= 3x 2, above 1.
        med = sorted(base['tris'].values())[len(base['tris']) // 2]
        r = tris / max(med, 1)
        g = 5 if r <= 1.0 else 4 if r <= 1.5 else 3 if r <= 2.0 else 2 if r <= 3.0 else 1
        out['efficiency'] = dict(tris=tris, class_median=med, ratio=round(r, 2), grade=g, grade_soft=soft_eff(r))
    print(json.dumps(out, indent=1))

if __name__ == '__main__':
    main(sys.argv[1:])
