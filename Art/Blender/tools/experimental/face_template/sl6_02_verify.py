"""Verify new sliders (sl_make_sliders.py output). Writes OUT/verify6.json + verify6.txt"""
import json, sys, numpy as np
from sl_common import *
t = Tpl(); B = t.basis; K = t.keys; N = len(B); rep = t.rep; F = t.faces
Z = np.load(OUT + '' + (sys.argv[1] if len(sys.argv)>1 else 'new_sliders.npz') + ''); names = [str(n) for n in Z['names']]; ND = Z['delta'].astype(np.float64)
ex_idx = list(t.slider_idx); ED = np.stack([K[i] - B for i in ex_idx])
groups = split_groups(t)
def fnorm(v):
    tri = v[F]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n
n0 = fnorm(B); a0 = np.linalg.norm(n0, axis=1); ok = a0 > 1e-12
def flips(v):
    n = fnorm(v); c = (n * n0).sum(1) / (np.linalg.norm(n, axis=1) * a0 + 1e-30)
    return int(((c <= 0) & ok).sum())
E = edges_of(rep[F]); 
def ringE(s):
    r = set(map(int, s)); return np.array([(a, b) for a, b in E if a in r and b in r])
rings = {n: t.S(n) for n in ['eyelid_ring_L', 'eyelid_ring_R', 'lip_line']}
rE = {n: ringE(np.unique(rep[v])) for n, v in rings.items()}
def ratio(v):
    out = {}
    for n, e in rE.items():
        q = np.linalg.norm(v[e[:, 0]] - v[e[:, 1]], axis=1) / np.linalg.norm(B[e[:, 0]] - B[e[:, 1]], axis=1); out[n] = [float(q.min()), float(q.max())]
    return out
def seam(v): return max(np.abs((v - B)[g] - (v - B)[g[0]]).max() for g in groups if len(g) > 1) * 1000   # delta split across a weld group
_up = t.S('lip_line_upper'); _lo = t.S('lip_line_lower')
_pair = _lo[np.abs(B[_up, 0][:, None] - B[_lo, 0][None]).argmin(1)]; _mid = np.abs(B[_up, 0]) < 0.02
def kd(name): return K[t.kidx(name)] - B
def gap(v, up, lo):
    up = t.S(up); lo = t.S(lo)
    lo = lo[np.argsort(v[lo, 0])]; zl = np.interp(v[up, 0], v[lo, 0], v[lo, 2]); g = (v[up, 2] - zl) * 1000
    return float(g.mean()), float(g.max()), float(g.min())
def lipgap(v):
    return float(((v[_up, 2] - v[_pair, 2]) * 1000)[_mid].min())   # index-paired (rest x), centre +-20 mm
R = {'items': {}}
L = []
Mv = ND.reshape(len(names), -1); Mn = Mv / (np.linalg.norm(Mv, axis=1)[:, None] + 1e-12)
Ev = ED.reshape(len(ex_idx), -1); En = Ev / (np.linalg.norm(Ev, axis=1)[:, None] + 1e-12)
Cx = Mn @ En.T; Cn = Mn @ Mn.T; np.fill_diagonal(Cn, 0)
bl = {'L': t.kidx('eyeBlinkLeft'), 'R': t.kidx('eyeBlinkRight')}
tb = {s: gap(B + kd('eyeBlinkLeft' if s == 'L' else 'eyeBlinkRight'), 'eyelid_ring_%s_upper' % s, 'eyelid_ring_%s_lower' % s) for s in 'LR'}
t0 = {s: gap(B, 'eyelid_ring_%s_upper' % s, 'eyelid_ring_%s_lower' % s) for s in 'LR'}
L.append('template eye gap open(mean/max/min mm) %s ; blink %s' % (t0, tb))
jo = kd('jawOpen') * 0.5; aa = kd('aa'); oh = kd('oh')
tl = {'jaw': lipgap(B + jo), 'aa': lipgap(B + aa), 'oh': lipgap(B + oh), 'rest': lipgap(B)}
L.append('template lip min gap center rest/jaw.5/aa/oh: %s' % tl)
for i, nm in enumerate(names):
    d = ND[i]; r = {}
    v = B + d
    r['nan'] = bool(np.isnan(d).any()); r['max_mm'] = float(np.linalg.norm(d, axis=1).max() * 1000)
    r['flips'] = flips(v); r['seam_mm'] = seam(v); r['neck_mm'] = float(np.linalg.norm(d[t.S('neck_ring_raw')], axis=1).max() * 1000)
    rr = ratio(v); r['ring_ratio'] = rr
    j = int(np.abs(Cx[i]).argmax()); r['max_cos_existing'] = [t.slider_names[j], float(Cx[i, j])]
    # ignore the exact negative twin for new-new
    jj = int(np.abs(Cn[i]).argmax()); r['max_cos_new'] = [names[jj], float(Cn[i, jj])]
    # blink
    bad = []
    for s in 'LR':
        g = gap(B + kd('eyeBlinkLeft' if s == 'L' else 'eyeBlinkRight') + d, 'eyelid_ring_%s_upper' % s, 'eyelid_ring_%s_lower' % s)
        r['blink_gap_%s' % s] = g[0] - tb[s][0]; r['blink_max_%s' % s] = g[1] - tb[s][1]
    r['blink_ok'] = bool(max(r['blink_gap_L'], r['blink_gap_R'], r['blink_max_L'], r['blink_max_R']) <= 0.5)
    r['lip'] = {k: lipgap(B + kk + d) - tl[k] for k, kk in (('jaw', jo), ('aa', aa), ('oh', oh))}
    r['lip_flips'] = {k: flips(B + kk + d) for k, kk in (('jaw', jo), ('aa', aa), ('oh', oh))}
    R['items'][nm] = r
    L.append('%-22s max%5.2f flips%d seam%.4f neck%.3f ring[L %.2f-%.2f R %.2f-%.2f lip %.2f-%.2f] cosEx %s %.2f cosNew %s %.2f blinkdGap %.2f/%.2f ok=%s lipdGap %s lipflips %s' % (
        nm, r['max_mm'], r['flips'], r['seam_mm'], r['neck_mm'], *rr['eyelid_ring_L'], *rr['eyelid_ring_R'], *rr['lip_line'], r['max_cos_existing'][0], r['max_cos_existing'][1],
        r['max_cos_new'][0], r['max_cos_new'][1], r['blink_gap_L'], r['blink_gap_R'], r['blink_ok'], {k: round(x, 2) for k, x in r['lip'].items()}, r['lip_flips']))
# random combos
rng = np.random.default_rng(7); base_fl = []; new_fl = []; worst = 0
pairs = [(2 * i, 2 * i + 1) for i in range(len(names) // 2)]
for s in range(100):
    w = rng.random(83) * (rng.random(83) < 0.25) * rng.choice([0.5, 1.0], 83)
    v0 = B + np.tensordot(w, ED, 1)
    wn = np.zeros(len(names))
    for a, b in pairs:
        if rng.random() < 0.6: wn[a if rng.random() < .5 else b] = rng.random()
    v1 = v0 + np.tensordot(wn, ND, 1)
    f0, f1 = flips(v0), flips(v1); base_fl.append(f0); new_fl.append(f1)
att = {}
samples = []
for s in range(30):
    w = rng.random(83) * (rng.random(83) < 0.25) * rng.choice([0.5, 1.0], 83); samples.append(B + np.tensordot(w, ED, 1))
for i, nm in enumerate(names):
    ex = [flips(v + ND[i]) - flips(v) for v in samples]; att[nm] = [float(np.mean(ex)), int(max(ex))]
R['attrib_extra_flips_mean_max'] = att
L.append('extra flips vs 30 random old combos (mean,max): ' + ', '.join('%s %.1f/%d' % (k, a, b) for k, (a, b) in att.items() if b > 0))
R['random'] =dict(n=100, flips_old_max=max(base_fl), flips_all_max=max(new_fl), extra_flips_max=int(max(np.array(new_fl) - np.array(base_fl))), samples_with_extra=int((np.array(new_fl) > np.array(base_fl)).sum()))
L.append('random 100: %s' % R['random'])
L.append('cos new vs existing: max over all new = %.2f ; new-new (non-twin) max = %.2f' % (np.abs(Cx).max(), max(abs(Cn[i, j]) for i in range(len(names)) for j in range(len(names)) if i != j and i // 2 != j // 2)))
json.dump(R, open(OUT + 'verify6.json', 'w'), indent=1); open(OUT + 'verify6.txt', 'w').write('\n'.join(L)); print('\n'.join(L))
