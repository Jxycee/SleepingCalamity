"""Is this head a plausible human head?  z-scores of the character's proportions against the SDXL face prior (human_prior.json).
usage (any python with numpy+cv2+scipy):  python plausibility.py <run_dir> [--tag h6] [--force-clay]   -> prints a plain-language report, writes <run_dir>/out/look/plausibility.json
Measured with the same code (proportions.measure) on (a) the fitted mesh's front render fit/rn_<tag>.png + its detected landmarks, (b) the target photo, so the report shows
mesh z, photo z and the mesh-minus-photo gap in sigma: a mesh that is odd because the PHOTO is odd (stylised render, wide-angle selfie) is not a fit error.
Side: from the fixed-camera clay side + front renders (metric, 1 px = 0.49 mm): skull width/depth and depth/height compared with LITERATURE values (flagged UNCALIBRATED, the prior has no side data).
Flags: |z| >= 2 watch, >= 3 FLAG (z = (value - median) / robust sigma of the prior; the sigma is a bald-SDXL-face spread, narrower than a real population: read +-3 as
'clearly outside what the generator draws', not as a medical statement)."""
import os, sys, json, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import proportions as PR, vcommon as VC
PRIOR = VC.HERE + '/human_prior.json'


def side_metrics(run):
    """metric head shape from the fixed clay front + side silhouettes. Rows are relative to the crown (top of the clay front mask) and the chin (landmark 152 mapped into the clay frame)."""
    run.ensure_clay(); mm = VC.S2 / VC.R2 * 1000.0
    _, mf = VC.read_clay(run.clay('front')); _, ms = VC.read_clay(run.clay('side'))
    rows = np.where(mf.sum(1) >= 3)[0]; top = int(rows.min())
    x152, z152 = VC.render_px_to_xz(run, *run.lm_render[152]); _, ychin = VC.xz_to_clay_px(x152, z152); H = ychin - top
    r0, r1 = int(top + 0.10 * H), int(top + 0.35 * H)         # crown-to-above-the-ears band: no nose, ears, brow ridge or neck
    ext = lambda m: max(int(np.ptp(np.where(m[y])[0])) for y in range(r0, r1) if m[y].any())
    wid, dep = ext(mf), ext(ms)
    return dict(head_height_mm=H * mm, dome_width_mm=wid * mm, dome_depth_mm=dep * mm, head_width_over_depth=wid / dep, head_depth_over_height=dep / H, crown_row=top, chin_row=float(ychin), band=[r0, r1])


def zscores(m, prior):
    return {k: (m[k] - f['median']) / f['sigma'] for k, f in prior['features'].items() if k in m}


def skull_summary(zm, zp, limit=3.0, waive=0.5):
    """Scored skull plausibility (round 15). Features = the silhouette ones (PR.SIL_KEYS). Per feature: raw |z| of the mesh; the feature is WAIVED when |z_mesh| <= |z_photo| + waive (the mesh is built like its photo:
    the photo itself is the outlier, not the fit). value = max over features of (min(|z|, limit) if waived else |z|), so value <= limit passes; raw_max_abs_z is the unwaived maximum. Missing silhouette -> value None."""
    ks = [k for k in PR.SIL_KEYS if k in zm]
    if not ks: return dict(value=None, raw_max_abs_z=None, features={})
    f = {}
    for k in ks:
        a = abs(zm[k]); b = abs(zp[k]) if k in zp else None; w = a > limit and b is not None and a <= b + waive
        f[k] = dict(z_mesh=zm[k], z_photo=zp.get(k), waived=bool(w), effective=min(a, limit) if w else a)
    return dict(value=max(v['effective'] for v in f.values()), raw_max_abs_z=max(abs(zm[k]) for k in ks), worst=max(ks, key=lambda k: f[k]['effective']), limit=limit, waive=waive, features=f)


def skull_check(run_dir, tag=None, prior=None):
    """cheap (no clay render): mesh render + photo mask -> skull_summary. Used by stages/score_final.py"""
    run = VC.Run(run_dir, tag); prior = prior or json.load(open(PRIOR))
    mm = PR.measure(cv2.imread(run.rn), run.lm_render); mm.pop('geom'); mm.pop('ok_sil')
    mp_ = PR.measure(run.photo, run.lm_photo, mask=VC.photo_head_mask(run)); mp_.pop('geom'); mp_.pop('ok_sil')
    return skull_summary(zscores(mm, prior), zscores(mp_, prior))


def mahal(m, prior):
    mh = prior['mahalanobis']; ks = mh['keys']
    if not all(k in m for k in ks):
        return None
    x = np.array([m[k] for k in ks]) - np.array(mh['mean']); C = np.array(mh['cov'])
    return float(np.sqrt(x @ np.linalg.solve(C, x)))


def analyse(run, prior=None, force_clay=False):
    prior = prior or json.load(open(PRIOR)); run.ensure_clay(force_clay)
    mm = PR.measure(cv2.imread(run.rn), run.lm_render); mm.pop('geom'); okm = mm.pop('ok_sil')
    mp_ = PR.measure(run.photo, run.lm_photo, mask=VC.photo_head_mask(run)); mp_.pop('geom'); okp = mp_.pop('ok_sil')
    zm, zp = zscores(mm, prior), zscores(mp_, prior)
    sd = side_metrics(run); side = {}
    for k, ref in prior['side_literature'].items():
        if k == 'note' or k not in sd:
            continue
        side[k] = dict(value=sd[k], z=(sd[k] - ref['median']) / ref['sigma'], label=ref['label'], low=ref['low'], high=ref['high'], calibrated=False)
    skull = skull_summary(zm, zp)
    res = dict(run=run.name, tag=run.tag, skull=skull, silhouette_ok=dict(mesh=okm, photo=okp), mesh=mm, photo=mp_, z_mesh=zm, z_photo=zp, gap_sigma={k: zm[k] - zp[k] for k in zm if k in zp},
               mahalanobis=dict(mesh=mahal(mm, prior) if okm else None, photo=mahal(mp_, prior) if okp else None, keys=len(prior['mahalanobis']['keys'])), side=side, side_raw=sd)
    json.dump(res, open(run.look + 'plausibility.json', 'w'), indent=1)
    return res


def phrase(k, z, prior):
    f = prior['features'][k]
    return f['high'] if z > 0 else f['low']


def report(res, prior, thr=2.0):
    L = []; flags = []
    L.append('PLAUSIBILITY %s (final tag %s): z = sigmas from the median of %d SDXL faces; photo z in brackets; gap = mesh minus photo' % (res['run'], res['tag'], prior['n_faces']))
    for k, z in sorted(res['z_mesh'].items(), key=lambda kv: -abs(kv[1])):
        zp = res['z_photo'].get(k, float('nan')); gap = res['gap_sigma'].get(k, float('nan'))
        lvl = 'FLAG ' if abs(z) >= 3 else 'watch' if abs(z) >= thr else 'ok   '
        name = phrase(k, z, prior) if abs(z) >= thr else prior['features'][k]['label']
        L.append('  %s %s: %+.1f sigma (photo %+.1f, gap %+.1f)  [%s %.3f vs prior %.3f]' % (lvl, name, z, zp, gap, k, res['mesh'][k], prior['features'][k]['median']))
        if abs(z) >= thr:
            flags.append('%s %+.1f' % (name, z))
    sk = res.get('skull') or {}
    if sk.get('value') is not None:
        L.append('  skull plausibility (scored, <= %.0f): value %.2f (raw max |z| %.2f, worst %s; waived = mesh no worse than its photo + %.1f sigma: %s)' % (sk['limit'], sk['value'], sk['raw_max_abs_z'], sk['worst'], sk['waive'], ', '.join(k for k, v in sk['features'].items() if v['waived']) or 'none'))
    m = res['mahalanobis']
    L.append('  joint (Mahalanobis over %d features): mesh %s, photo %s (a typical face is about %.1f)' % (m['keys'], '%.1f' % m['mesh'] if m['mesh'] else 'n/a', '%.1f' % m['photo'] if m['photo'] else 'n/a', np.sqrt(m['keys'])))
    if not res['silhouette_ok']['mesh'] or not res['silhouette_ok']['photo']:
        L.append('  note: head silhouette features missing for %s (crown clipped or mask unusable)' % ('mesh' if not res['silhouette_ok']['mesh'] else 'photo'))
    for k, s in res['side'].items():
        lvl = 'FLAG ' if abs(s['z']) >= 3 else 'watch' if abs(s['z']) >= thr else 'ok   '
        name = (s['high'] if s['z'] > 0 else s['low']) if abs(s['z']) >= thr else s['label']
        L.append('  %s [side, UNCALIBRATED literature] %s: %+.1f sigma (value %.3f)' % (lvl, name, s['z'], s['value']))
        if abs(s['z']) >= thr:
            flags.append('side: %s %+.1f (uncalibrated)' % (name, s['z']))
    L.append('  summary: ' + ('; '.join(flags) if flags else 'no proportion outside +-%.0f sigma' % thr))
    return '\n'.join(L), flags


if __name__ == '__main__':
    a = sys.argv[1:]; tag = a[a.index('--tag') + 1] if '--tag' in a else None
    run = VC.Run(a[0], tag); prior = json.load(open(PRIOR)); res = analyse(run, prior, '--force-clay' in a)
    print(report(res, prior)[0])
