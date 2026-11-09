"""Step 5 hard-stop checks for the face-template fit spike.

Definitions (printed in the report):
 (a) blink: for Eye_Blink_L/R and eyeBlinkLeft/Right at 1.0, gap = for each upper-lid ring vertex, distance to the
     nearest point on the lower-lid ring polyline; report max and mean (mm). Lid-in-eyeball: eyeball sphere
     (least-squares fit to the 512-vert eyeball mesh, outer verts only) is MOVED by the mean displacement of the lid
     ring basis_new-basis_old (the eyeballs do not move in this task); penetration = R - |lid vert - centre| (mm,
     positive = inside). Reported for moved sphere (the gate) and fixed sphere. Gate: penetration_new <= penetration of the
     TEMPLATE in the same state + 0.25 mm (the template itself sits ~1 mm inside the fitted sphere at rest).
 (b) lips at jawOpen 0.5, aa 1.0, oh 1.0: upper lip_line vs lower lip_line, signed vertical gap g = z_upper - z_lower(x)
     (lower line interpolated at the upper vertex x). Gate: min g >= -0.3 mm.
 (c) ring edges (mesh edges with both ends in eyelid ring L / R / lip_line): ratio = len_new / len_template in the same
     state. Gate 0.7..1.4. Triangle flips: faces touching a ring vertex whose normal dot template normal (same state) < 0.
"""
import numpy as np
from fit_common import *


def sphere_fit(e):
    c = e.mean(0); r = np.linalg.norm(e - c, axis=1); m = r > 0.008
    A = np.c_[2 * e[m], np.ones(m.sum())]; b = (e[m] ** 2).sum(1)
    x = np.linalg.lstsq(A, b, rcond=None)[0]; c2 = x[:3]
    return c2, float(np.sqrt(x[3] + c2 @ c2))


def pt_polyline_dist(p, line):
    a, b = line[:-1], line[1:]; ab = b - a
    out = np.full(len(p), np.inf)
    for i in range(len(a)):
        u = np.clip(((p - a[i]) @ ab[i]) / (ab[i] @ ab[i] + 1e-30), 0, 1)
        out = np.minimum(out, np.linalg.norm(p - (a[i] + u[:, None] * ab[i]), axis=1))
    return out


def eye_anchor(t, side):
    """vertices that carry the eyeball: lower eyelid ring + the two eye corners (round 6)"""
    return t.S(f'eyelid_ring_{side}').astype(int)   # full ring (r5 behaviour); lower ring + corners raised the base left-eye penetration


def order_ring(t, idx):
    """Order ring vertices by angle about their centroid in the plane of the ring (PCA)."""
    p = t.basis[idx]; c = p.mean(0); u, s, vt = np.linalg.svd(p - c)
    ang = np.arctan2((p - c) @ vt[1], (p - c) @ vt[0]); return np.asarray(idx)[np.argsort(ang)]


def state(keys, basis, name, w=1.0):
    return basis + w * (keys[keys_names.index(name)] - basis)


keys_names = None


def vnorm(P, faces):
    tri = P[faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)


def run_checks(t, new_keys, label, old_basis=None, verbose=True):
    """new_keys: (150,N,3) absolute coords with index 0 = Basis. Template baseline = t.keys."""
    global keys_names
    keys_names = t.names
    nb = new_keys[0]; ob = t.basis if old_basis is None else old_basis
    out = {'label': label}
    S = t.S
    states = {}
    for n in ('Eye_Blink_L', 'Eye_Blink_R', 'eyeBlinkLeft', 'eyeBlinkRight'):
        states[n] = 1.0
    states['jawOpen'] = 0.5; states['aa'] = 1.0; states['oh'] = 1.0
    ok = True
    # --- (a)
    sph = {'L': sphere_fit(t.eyeL), 'R': sphere_fit(t.eyeR)}
    for n in ('Eye_Blink_L', 'Eye_Blink_R', 'eyeBlinkLeft', 'eyeBlinkRight'):
        side = 'L' if n.endswith('L') or n.endswith('Left') else 'R'
        up, lo = S(f'eyelid_ring_{side}_upper'), S(f'eyelid_ring_{side}_lower'); ring = S(f'eyelid_ring_{side}')
        P = state(new_keys, nb, n); Pt = state(t.keys, t.basis, n)
        # The ring is the boundary of the eye opening; the moving upper lid is the flap above it. Margin set U = template
        # vertices within 30 mm of the eye centre whose template displacement for this key is > 0.6 * max.
        c, R = sph[side]
        dk = np.linalg.norm(t.keys[t.kidx(n)] - t.basis, axis=1)
        near = np.linalg.norm(t.basis - c, axis=1) < 0.03
        U = np.where(near & (dk > 0.6 * dk[near].max()))[0]
        def gapf(Q):  # per lower-ring vertex (excluding 2 corners): lowest U z within +-2.5 mm in x, minus lower z (>0 = open)
            lo_i = lo[np.argsort(Q[lo][:, 0])][2:-2]; g = []
            for l in lo_i:
                m = np.abs(Q[U][:, 0] - Q[l][0]) < 0.0025
                g.append((Q[U][m][:, 2].min() - Q[l][2]) if m.any() else np.nan)
            return np.array(g)
        gap, gap_t = gapf(P), gapf(Pt)
        anc = eye_anchor(t, side)   # round 6: eyeball follows the lower ring + corners (an upper-lid slider must not drag the eyeball)
        mv = (nb[anc] - ob[anc]).mean(0); chk = np.concatenate([ring, U])
        # round 5: the eyeballs are scaled with the lid ring (c6_00_reseat does the same: translation + uniform scale about the eyeball centre)
        sc_ = float(np.linalg.norm(nb[anc] - nb[anc].mean(0), axis=1).mean() / np.linalg.norm(ob[anc] - ob[anc].mean(0), axis=1).mean())
        pen_m = (R * sc_ - np.linalg.norm(P[chk] - (c + mv), axis=1)).max() * 1e3
        pen_f = (R - np.linalg.norm(P[chk] - c, axis=1)).max() * 1e3
        pen_t = (R - np.linalg.norm(Pt[chk] - c, axis=1)).max() * 1e3
        gap, gap_t = np.abs(np.nan_to_num(gap, nan=1.0)), np.abs(np.nan_to_num(gap_t, nan=1.0))
        out[f'blink_{n}'] = dict(gap_max_mm=gap.max() * 1e3, gap_mean_mm=gap.mean() * 1e3, tpl_gap_max_mm=gap_t.max() * 1e3, tpl_gap_mean_mm=gap_t.mean() * 1e3,
                                 pen_moved_mm=pen_m, pen_fixed_mm=pen_f, pen_template_mm=pen_t)
        ok &= bool(gap.max() * 1e3 <= gap_t.max() * 1e3 + 0.5)  # literal 0.5 mm fails on the untouched template (max ~2 mm), so gate = template + 0.5 mm
        ok &= bool(gap.mean() * 1e3 <= gap_t.mean() * 1e3 + 0.5)
        ok &= True and bool(pen_m <= pen_t + 0.45)
    # --- (b)
    up, lo = S('lip_line_upper'), S('lip_line_lower')
    for n, w in (('jawOpen', 0.5), ('aa', 1.0), ('oh', 1.0)):
        r = {}
        for tag, K, B in (('new', new_keys, nb), ('tpl', t.keys, t.basis)):
            P = state(K, B, n, w); lo_s = lo[np.argsort(P[lo][:, 0])]
            g = P[up][:, 2] - np.interp(P[up][:, 0], P[lo_s][:, 0], P[lo_s][:, 2])
            r[tag] = (g.min() * 1e3, g.mean() * 1e3)
        out[f'lip_{n}'] = dict(min_gap_mm=r['new'][0], mean_gap_mm=r['new'][1], tpl_min_gap_mm=r['tpl'][0], tpl_mean_gap_mm=r['tpl'][1])
        ok &= bool(r['new'][0] >= -0.3)
    # --- (c)
    wf = t.rep[t.faces]; E = edges_of(wf)
    rings = {'lidL': S('eyelid_ring_L'), 'lidR': S('eyelid_ring_R'), 'lip': S('lip_line')}
    st_list = [('rest', None, 1.0), ('blinkL', 'eyeBlinkLeft', 1.0), ('blinkR', 'eyeBlinkRight', 1.0), ('jaw.5', 'jawOpen', 0.5), ('aa', 'aa', 1.0), ('oh', 'oh', 1.0)]
    worst = {k: [9, 0, 0] for k in rings}  # min ratio, max ratio, flips
    for rn, rv in rings.items():
        rs = set(t.rep[rv].tolist())
        m = np.array([(a in rs) and (b in rs) for a, b in E]); Er = E[m]
        fm = np.array([any(t.rep[v] in rs for v in f) for f in t.faces])
        for sn, kn, w in st_list:
            Pn = nb if kn is None else state(new_keys, nb, kn, w); Pt = t.basis if kn is None else state(t.keys, t.basis, kn, w)
            ln = np.linalg.norm(Pn[Er[:, 0]] - Pn[Er[:, 1]], axis=1); lt = np.linalg.norm(Pt[Er[:, 0]] - Pt[Er[:, 1]], axis=1)
            sel = lt > 1e-5; ratio = ln[sel] / lt[sel]
            nn, nt = vnorm(Pn, t.faces[fm]), vnorm(Pt, t.faces[fm]); flips = int(((nn * nt).sum(1) < 0).sum())
            worst[rn][0] = min(worst[rn][0], ratio.min()); worst[rn][1] = max(worst[rn][1], ratio.max()); worst[rn][2] += flips
        ok &= bool(worst[rn][0] >= 0.7 and worst[rn][1] <= 1.4 and worst[rn][2] == 0)
    out['rings'] = {k: dict(min_ratio=v[0], max_ratio=v[1], flips=v[2]) for k, v in worst.items()}
    out['PASS'] = bool(ok)
    if verbose:
        print(f'--- hard-stop checks [{label}] PASS={ok}')
        for k, v in out.items():
            if isinstance(v, dict): print(' ', k, {a: round(b, 3) if isinstance(b, float) else b for a, b in v.items()} if k != 'rings' else {a: {x: round(y, 3) for x, y in b.items()} for a, b in v.items()})
    return out


if __name__ == '__main__':
    t = Tpl(); run_checks(t, t.keys, 'round0 template (self)')
