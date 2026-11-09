"""Slider atlas: what does each fit slider do to the head, and to the photo ratios the fit measures?
usage (facekeys or trellis2 python: numpy, cv2, scipy; Blender 5.2 for the renders):
    python slider_atlas.py [--run C:/AI/test_out/heroes/pipeline_runs/cloud_v1] [--out DIR] [--tile 192] [--no-render]
Inputs from --run (read only): fit/tobin_head_template6.npz (the 184-key template; identical for every character) and fit/tpl_hits.json (template landmark ray hits).
The slider list is READ FROM stages/r6_06_sliderloop.py (PAIRS, SINGLES, SCN literals) so it cannot drift from the fit loop: 18 signed pairs + 6 one-sided + 14 SC_ pairs = 38 columns
(the 16 global warp parameters are not sliders and are not in the atlas). A coefficient a > 0 applies the '+' key delta, a < 0 the '-' key delta (exactly as the fit's slider_disp).
Outputs (DIR, default C:/AI/test_out/heroes/vision_prior/atlas/; the .json/.md are also copied next to this file):
  atlas_page<N>.png   contact sheet: per slider, FRONT -1 | 0 | +1 then SIDE -1 | 0 | +1 (clay, fixed camera of look_sheet; the neutral silhouette is drawn in cyan on the +-1 tiles)
  sensitivity.json / sensitivity.md   per slider and sign: % change of the 13 photo ratios + head_aspect + skull_over_face + head height/width (mm) for a = -1 / +1
Sensitivity is GEOMETRIC: landmarks = inverse-distance-weighted template vertices at the template ray hits (same model as r6_02_warp.geo / lm_model), ratios in the front plane (x, -z).
It is NOT the detector's response (MediaPipe follows a moved landmark with gain 0.05-0.7, README round 12-13). Four landmarks have no ray hit (159, 386, 145, 374: lid mid points);
they are taken as the highest / lowest eyelid-ring vertex at the x of the eye centre (approximation; only brow_eye_gap and eye_h use them)."""
import os, sys, json, ast, subprocess, argparse, numpy as np, cv2
from scipy.spatial import cKDTree
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
STAGES = os.path.abspath(os.path.join(HERE, '..', 'stages')).replace(chr(92), '/')
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
sys.path.insert(0, HERE)


def slider_columns():
    """parse PAIRS / SINGLES / SCN out of r6_06_sliderloop.py -> [(name, plus_key, minus_key|None, lo, hi)]"""
    tree = ast.parse(open(STAGES + '/r6_06_sliderloop.py', encoding='utf-8').read()); lit = {}
    for n in tree.body:
        if isinstance(n, ast.Assign) and len(n.targets) == 1 and isinstance(n.targets[0], ast.Name) and n.targets[0].id in ('PAIRS', 'SINGLES', 'SCN'):
            lit[n.targets[0].id] = ast.literal_eval(n.value)
    cols = [(a + '~' + b, a, b, -1.0, 1.0) for a, b in lit['PAIRS']] + [(a, a, None, 0.0, 1.0) for a in lit['SINGLES']] + [('SC_' + n, 'SC_' + n + '+', 'SC_' + n + '-', -1.0, 1.0) for n in lit['SCN']]
    return cols


class Geo:
    """geometric landmark model on the template (r6_02_warp.geo / ratios)"""
    def __init__(self, z, hits):
        self.basis = z['head/basis'].astype(float); self.names = [str(x) for x in z['head/key_names']]; self.keys = z['head/keys'].astype(float); self.faces = z['head/faces']
        self.z = z
        lid = np.array([i for i in range(468) if str(i) in hits and hits[str(i)] is not None and np.asarray(hits[str(i)]).shape == (3,)])
        kd = cKDTree(self.basis); H = np.array([hits[str(i)] for i in lid]); dd, ii = kd.query(H, k=6); ww = 1 / (dd + 1e-3) ** 2; ww /= ww.sum(1, keepdims=True)
        self.ii, self.ww, self.lid = ii, ww, lid; self.pos = {int(i): k for k, i in enumerate(lid)}
        rings = {s: np.asarray(z['tpl/eyelid_ring_' + s]).astype(int) for s in ('L', 'R')}; P0 = self.basis
        self.lidpt = {}   # landmark -> (ring vertex index) for the 4 lid mid points
        L0 = self.landmarks(self.basis, lids=False)
        for (c1, c2, up, lo) in ((33, 133, 159, 145), (263, 362, 386, 374)):
            xc = (L0[c1][0] + L0[c2][0]) / 2; ring = rings['L'] if abs(P0[rings['L']][:, 0].mean() - xc) < abs(P0[rings['R']][:, 0].mean() - xc) else rings['R']
            near = ring[np.abs(P0[ring][:, 0] - xc) < 0.0015]
            if len(near) == 0: near = ring[np.argsort(np.abs(P0[ring][:, 0] - xc))[:4]]
            self.lidpt[up] = int(near[np.argmax(P0[near][:, 2])]); self.lidpt[lo] = int(near[np.argmin(P0[near][:, 2])])
    def landmarks(self, P, lids=True):
        G = (P[self.ii] * self.ww[:, :, None]).sum(1); L = {int(i): G[k] for i, k in self.pos.items()}
        if lids:
            for i, v in self.lidpt.items(): L[i] = P[v]
        return L
    def ratios(self, P):
        L = self.landmarks(P); xz = lambda i: np.array([L[i][0], -L[i][2]]) * 1e3
        d = lambda a, b: float(np.linalg.norm(xz(a) - xz(b))); io = d(33, 263); eyec = (xz(33) + xz(263)) / 2
        r = {'nose_width': d(129, 358) / io, 'mouth_width': d(61, 291) / io, 'face_width': d(234, 454) / io, 'jaw_width': d(172, 397) / io,
             'brow_eye_gap': ((abs(xz(105)[1] - xz(159)[1]) + abs(xz(334)[1] - xz(386)[1])) / 2) / io, 'eye_to_nose_base': (xz(2)[1] - eyec[1]) / io, 'nose_to_mouth': (xz(13)[1] - xz(2)[1]) / io,
             'mouth_to_chin': (xz(152)[1] - xz(13)[1]) / io, 'eye_to_chin': (xz(152)[1] - eyec[1]) / io, 'forehead_eye': (eyec[1] - xz(10)[1]) / io, 'lip_thickness': d(0, 17) / io,
             'eye_w': (d(33, 133) + d(263, 362)) / 2 / io, 'eye_h': (d(159, 145) + d(386, 374)) / 2 / io}
        # head shape from the head mesh (the head mesh includes the neck: the crown is the top vertex, the chin is landmark 152)
        topz = float(P[:, 2].max()) * 1e3; chin = -xz(152)[1]; brow = -(xz(105)[1] + xz(334)[1]) / 2; zrow = topz - 0.55 * (topz - brow)
        band = P[np.abs(P[:, 2] * 1e3 - zrow) < 1.5]; wd = float((band[:, 0].max() - band[:, 0].min()) * 1e3); H = topz - chin
        r['head_aspect'] = H / wd; r['skull_over_face'] = (topz - brow) / (brow - chin) * -1 if False else (topz - brow) / (brow - chin)
        r['head_aspect_cheek'] = H / d(234, 454)
        mm = dict(head_height_mm=H, dome_width_mm=wd)
        return r, mm


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--run', default='C:/AI/test_out/heroes/pipeline_runs/cloud_v1'); ap.add_argument('--out', default='C:/AI/test_out/heroes/vision_prior/atlas')
    ap.add_argument('--tile', type=int, default=192); ap.add_argument('--res', type=int, default=384); ap.add_argument('--no-render', action='store_true'); a = ap.parse_args()
    out = a.out.rstrip('/'); os.makedirs(out + '/meshes', exist_ok=True)
    z = np.load(a.run + '/fit/tobin_head_template6.npz', allow_pickle=True); hits = json.load(open(a.run + '/fit/tpl_hits.json')); G = Geo(z, hits)
    cols = slider_columns(); kidx = {n: i for i, n in enumerate(G.names)}
    bad = [c[1] for c in cols if c[1] not in kidx] + [c[2] for c in cols if c[2] and c[2] not in kidx]
    if bad: sys.exit('keys missing from the template: %s' % bad)
    ev = np.concatenate([z['eyeL/basis'], z['eyeR/basis']]).astype(float); ef = np.concatenate([z['eyeL/faces'], z['eyeR/faces'] + 512])
    jobs = {}; coef = {}
    def mesh(name, P):
        f = out + '/meshes/%s.npz' % name; np.savez(f, v=P, f=G.faces, ev=ev, ef=ef); jobs[name] = f
    mesh('neutral', G.basis)
    sens = {}; r0, mm0 = G.ratios(G.basis)
    for ci, (name, kp, km, lo, hi) in enumerate(cols):
        sens[name] = dict(plus_key=kp, minus_key=km, effect={})
        for a_ in (-1.0, 1.0):
            if a_ < lo: continue
            D = (G.keys[kidx[kp]] - G.basis) if a_ > 0 else (G.keys[kidx[km]] - G.basis)
            P = G.basis + abs(a_) * D; tag = 'p' if a_ > 0 else 'm'; mesh('s%02d%s' % (ci, tag), P)
            r, mm = G.ratios(P); e = {k: 100 * (r[k] / r0[k] - 1) for k in r0}; e['head_height_mm'] = mm['head_height_mm'] - mm0['head_height_mm']; e['dome_width_mm'] = mm['dome_width_mm'] - mm0['dome_width_mm']
            e['max_disp_mm'] = float(np.linalg.norm(P - G.basis, axis=1).max() * 1e3); sens[name]['effect']['%+d' % int(a_)] = e
    if not a.no_render:
        todo = {k: v for k, v in jobs.items() if not (os.path.exists(v.replace('.npz', '_front.png')) and os.path.exists(v.replace('.npz', '_side.png')))}
        if todo:
            jf = out + '/jobs.json'; json.dump(todo, open(jf, 'w'))
            r = subprocess.run([BLENDER, '-b', '--factory-startup', '--python', HERE + '/atlas_render.py', '--', jf, str(a.res)], capture_output=True, text=True)
            if 'ATLAS_DONE' not in r.stdout: sys.exit('render failed\n' + r.stdout[-1200:] + r.stderr[-600:])
    r_names = list(r0)
    json.dump(dict(neutral_ratios=r0, neutral_mm=mm0, note='geometric ratios of the template under slider +-1 (not detector response); % change of each ratio, mm change for head size', sliders=sens), open(out + '/sensitivity.json', 'w'), indent=1)
    # ---- markdown table
    show = ['face_width', 'jaw_width', 'nose_width', 'mouth_width', 'eye_to_nose_base', 'nose_to_mouth', 'mouth_to_chin', 'eye_to_chin', 'forehead_eye', 'brow_eye_gap', 'lip_thickness', 'eye_w', 'eye_h', 'head_aspect', 'skull_over_face']
    L = ['# Slider sensitivity (geometric, template neutral)', '', 'Percent change of each photo ratio (and head_aspect, skull_over_face) when the slider coefficient is -1 / +1 (one-sided sliders only +1). `reach` = max |%| over the table columns; `mm` = max vertex displacement. Front-plane ratios: depth-only sliders read 0.', '',
         '| slider | sign | ' + ' | '.join(show) + ' | dHeight mm | dDome mm | reach | mm |', '|---|---|' + '---|' * (len(show) + 4)]
    for name, s in sens.items():
        for sg, e in s['effect'].items():
            reach = max(abs(e[k]) for k in show); L.append('| %s | %s | ' % (name, sg) + ' | '.join('%+.1f' % e[k] for k in show) + ' | %+.1f | %+.1f | %.1f | %.1f |' % (e['head_height_mm'], e['dome_width_mm'], reach, e['max_disp_mm']))
    L += ['', '## What moves what (|effect| >= 1.5 % at +-1, strongest 3 sliders per quantity)', '']
    for k in show:
        best = sorted(((max(abs(e[k]) for e in s['effect'].values()), n) for n, s in sens.items()), reverse=True)[:3]
        L.append('- **%s**: ' % k + ', '.join('%s (%.1f %%)' % (n, v) for v, n in best if v >= 1.5) if best[0][0] >= 1.5 else '- **%s**: no slider moves it by 1.5 %% or more' % k)
    open(out + '/sensitivity.md', 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    for f in ('sensitivity.json', 'sensitivity.md'): open(HERE + '/slider_' + f, 'w', encoding='utf-8').write(open(out + '/' + f, encoding='utf-8').read())
    # ---- contact sheets
    T = a.tile; import vcommon as VC
    neutral = {v: VC.read_clay(jobs['neutral'].replace('.npz', '_%s.png' % v))[0] for v in ('front', 'side')}
    from scipy import ndimage as ndi
    nsil = {v: VC.read_clay(jobs['neutral'].replace('.npz', '_%s.png' % v))[1] for v in ('front', 'side')}
    def tile(name, view, flip=False):
        img = VC.read_clay(jobs[name].replace('.npz', '_%s.png' % view))[0] if name in jobs else None
        if img is None: img = np.full_like(neutral[view], 40)
        else:
            if name != 'neutral':
                cnt, _ = cv2.findContours(nsil[view].astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); cv2.polylines(img, cnt, True, (255, 255, 0), 1, cv2.LINE_AA)
        # crop to the head (top 78 % of the frame keeps crown to neck) and shrink
        h = img.shape[0]; img = img[int(0.04 * h):int(0.80 * h), int(0.12 * h):int(0.88 * h)]
        return cv2.resize(img, (T, T), interpolation=cv2.INTER_AREA)
    per_page, pcols = 16, 2; rows_per = per_page // pcols; label_h = 14; cellw = 6 * T; pages = []
    for p0 in range(0, len(cols), per_page):
        chunk = list(range(p0, min(p0 + per_page, len(cols)))); H_ = rows_per * (T + label_h); canvas = np.full((H_, pcols * cellw + (pcols - 1) * 10, 3), 24, np.uint8)
        for j, ci in enumerate(chunk):
            r_, c_ = j % rows_per, j // rows_per; x0 = c_ * (cellw + 10); y0 = r_ * (T + label_h); name = cols[ci][0]
            has_m = cols[ci][2] is not None
            tl = [tile('s%02dm' % ci, 'front') if has_m else np.full((T, T, 3), 40, np.uint8), tile('neutral', 'front'), tile('s%02dp' % ci, 'front'),
                  tile('s%02dm' % ci, 'side') if has_m else np.full((T, T, 3), 40, np.uint8), tile('neutral', 'side'), tile('s%02dp' % ci, 'side')]
            for k, t_ in enumerate(tl): canvas[y0 + label_h:y0 + label_h + T, x0 + k * T:x0 + (k + 1) * T] = t_
            reach = max((abs(e[kk]) for e in sens[name]['effect'].values() for kk in show), default=0)
            cv2.putText(canvas, '%02d %s  (-1 | 0 | +1 front, then side)  reach %.1f%%' % (ci, name, reach), (x0 + 2, y0 + 11), cv2.FONT_HERSHEY_SIMPLEX, 0.38, (255, 255, 255), 1, cv2.LINE_AA)
        fn = out + '/atlas_page%d.png' % (p0 // per_page + 1); cv2.imwrite(fn, canvas); pages.append(fn)
    print('sliders', len(cols), 'pages', pages); print('sensitivity ->', out + '/sensitivity.md', '(copied to vision/slider_sensitivity.*)')


if __name__ == '__main__':
    main()
