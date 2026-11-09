"""Diagnostic: list triangles that newly flip for a new slider (optionally on top of an expression key, e.g. SC_X+:oh)."""
import sys, numpy as np
from sl_common import *
t = Tpl(); B = t.basis; F = t.faces
Z = np.load(OUT + 'new_sliders.npz'); names = [str(n) for n in Z['names']]; ND = Z['delta'].astype(np.float64)
def fnorm(v):
    tri = v[F]; return np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
n0 = fnorm(B); a0 = np.linalg.norm(n0, axis=1)
def bad(v, extra=0):
    n = fnorm(v + extra); c = (n * n0).sum(1) / (np.linalg.norm(n, axis=1) * a0 + 1e-30)
    return np.where((c <= 0) & (a0 > 1e-12))[0]
for nm in sys.argv[1:]:
    ex = 0
    if ':' in nm:
        nm, kn = nm.split(':'); ex = (t.keys[t.kidx(kn)] - B) * (0.5 if kn == 'jawOpen' else 1)
    d = ND[names.index(nm)]; b0 = set(bad(B, ex)); b = bad(B + d, ex)
    nb = [i for i in b if i not in b0]
    print(nm, 'new flips', len(nb), [(F[i].tolist(), (B[F[i]].mean(0) * 1000).round(0).tolist()) for i in nb[:4]])
