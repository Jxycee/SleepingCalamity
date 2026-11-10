# Mix two HairDeck hairstyles (cards_seed*.json, both in HairDeck's canonical head frame, so their cards combine directly).
#   python hd_mix.py <A.json> <B.json> <out.json> --mode union|region [--root 0.012] [--tip 0.02] [--crown 0.455] [--drop i,j] [--trim i:frac,...]
# union : all of A, plus every B card with no A card whose root lies within --root and tip within --tip (HairDeck units; head ~0.10 wide).
# region: top / crown / back cards from A, fringe and side cards from B (classified by where each card ends).
import sys, json, argparse, numpy as np
ap = argparse.ArgumentParser(); ap.add_argument('a'); ap.add_argument('b'); ap.add_argument('out'); ap.add_argument('--mode', default='union')
ap.add_argument('--root', type=float, default=0.012); ap.add_argument('--tip', type=float, default=0.02); ap.add_argument('--crown', type=float, default=0.455)
ap.add_argument('--drop', default='', help='comma-separated indices of the mixed list to remove (stray cards)')
ap.add_argument('--trim', default='', help='i:frac,... shorten card i (index after --drop, = hd_strands cid) to frac of its arc length')
a = ap.parse_args()
A, B = json.load(open(a.a)), json.load(open(a.b))
ends = lambda C: (np.array([np.array(c['seq'])[0, :3] for c in C]), np.array([np.array(c['seq'])[-1, :3] for c in C]))
(ra, ta), (rb, tb) = ends(A), ends(B)
if a.mode == 'union':
    keep = [j for j in range(len(B)) if not np.any((np.linalg.norm(ra - rb[j], axis=1) < a.root) & (np.linalg.norm(ta - tb[j], axis=1) < a.tip))]
    out = A + [B[j] for j in keep]
    print('MIX union: A %d + B %d of %d -> %d cards' % (len(A), len(keep), len(B), len(out)))
else:   # by where each card ENDS (roots all sit near the crown): fringe = tip in front (z > 0.03) below the top, sides = |x| > 0.05 and not back
    def face(t): return ((t[:, 2] > 0.03) & (t[:, 1] < a.crown)) | ((np.abs(t[:, 0]) > 0.05) & (t[:, 2] > -0.02))
    fa, fb = face(ta), face(tb)
    out = [c for c, f in zip(A, fa) if not f] + [c for c, f in zip(B, fb) if f]
    print('MIX region: A top/crown/back %d + B fringe/sides %d -> %d cards' % ((~fa).sum(), fb.sum(), len(out)))
if a.drop:
    dr = {int(i) for i in a.drop.split(',')}; out = [c for i, c in enumerate(out) if i not in dr]; print('DROP', sorted(dr), '->', len(out))
if a.trim:
    for it in a.trim.split(','):
        i, fr = it.split(':'); i, fr = int(i), float(fr); S = np.array(out[i]['seq'], float)
        d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(S[:, :3], axis=0), axis=1))]; d /= d[-1]
        n = max(4, int(round(len(S) * fr))); q = np.linspace(0, fr, n)
        out[i] = dict(out[i], seq=np.stack([np.interp(q, d, S[:, k]) for k in range(S.shape[1])], 1).tolist())
    print('TRIM', a.trim)
json.dump(out, open(a.out, 'w'))
