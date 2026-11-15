# Filters a HairDeck cards json down to nape cards: TIP behind the skull centre and below ear-top height (world z, via the same ellipsoid map hd_fit.py applies).
#   <trellis python> sweep_nape_cards.py <src cards.json> <out.json> <variant_dir with fit.log + opt/best.json> [ear_top=1.80] [min_z=1.55]
import sys, os, re, json, numpy as np
src, out, vd = sys.argv[1:4]; kw = dict(a.split('=', 1) for a in sys.argv[4:]); ear = float(kw.get('ear_top', 1.80)); zmin = float(kw.get('min_z', 1.55))
log = open(os.path.join(vd, 'fit.log'), errors='ignore').read(); P = json.load(open(os.path.join(vd, 'opt', 'best.json')))['best']['params']
cen = np.array([float(x) for x in re.search(r'centre \[([^\]]*)\]', log).group(1).split()]); sc = np.array([float(x) for x in re.search(r'SCALE per axis \[([^\]]*)\]', log).group(1).split(',')])
S = sc   # the log SCALE already includes sx sy sz
D = cen + np.array([0, P.get('dy', 0), P.get('dz', 0)]); PC = np.array([0.0, -0.0003, 0.422])
cards = json.load(open(src)); keep = []
for c in cards:
    t = np.array(c['seq'])[-1, :3]; b = np.array([t[0], -t[2], t[1]]); w = D + (b - PC) * S      # glTF (x, y up, z front) -> Blender (x, -z, y)
    if w[1] > D[1] and zmin < w[2] < ear: keep.append(c)
json.dump(keep, open(out, 'w')); print('NAPE', len(keep), 'of', len(cards), 'skull centre y %.3f' % D[1])
