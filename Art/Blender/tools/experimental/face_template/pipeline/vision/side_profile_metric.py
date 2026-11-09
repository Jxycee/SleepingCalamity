"""Score a fitted head's SIDE profile against the SDXL side prior (side_prior.json). Read-only on the run; writes ONLY <run_dir>/out/side_check/.
usage (facekeys python):  python side_profile_metric.py <run_dir> [--tag h6] [--keep-clay]
1. renders the run's mesh (fit/_rr_<tag>.npz) with clay_views.py (side + q34, fixed ortho camera) into out/side_check/ (not out/look/);
2. measures the side silhouette with side_prior.measure (same code as the portraits): anchors nose tip / crown / menton, H = crown-to-menton, scalars + back/front/radial curves;
3. prints z-scores in plain language and writes side_check.json / side_check.txt, side_overlay.png (mesh silhouette over the prior median outline + the +-2 sigma band, in the mesh's own H frame)
   The 3/4 clay (clay_q34.png) is rendered for the eye only: the SDXL 3/4 prompts came out frontal, so there is NO 3/4 prior (SIDE_README.md).
Sign convention: z = (mesh - prior median) / prior sigma; radial r(theta) is the distance from a fixed pivot to the outline, so z > 0 means the mesh outline is OUTSIDE the prior there (too full), z < 0 INSIDE (too flat / cut off).
Honest limit: the prior is what SDXL draws (UNCALIBRATED vs measured 3D heads; see SIDE_README.md), sigma is narrow, |z| >= 2 watch, >= 3 FLAG."""
import os, sys, json, subprocess, glob, argparse, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vcommon as VC, side_prior as SP

SEG = [('back of skull (occiput)', -20, 40), ('upper back / crown shoulder', 45, 85), ('crown top', 90, 115), ('forehead / front of skull', 120, 165)]


def render(run, outdir, force=False):
    pref = outdir + '/clay'
    if not force and all(os.path.exists(pref + '_%s.png' % v) and os.path.getmtime(pref + '_%s.png' % v) > os.path.getmtime(run.mesh) for v in ('side', 'q34')): return
    r = subprocess.run([VC.BLENDER, '-b', '--factory-startup', '--python', VC.HERE + '/clay_views.py', '--', run.mesh, pref, 'side,q34', str(VC.R2)], capture_output=True, text=True)
    if 'CLAY_DONE' not in r.stdout: raise SystemExit('clay render failed:\n' + r.stdout[-1200:] + r.stderr[-600:])


def zrad(m, P):
    r = np.array(m['radial']); med = np.array(P['radial']['median']); sg = np.array(P['radial']['sigma']); return (r - med) / sg


def report(name, m, P):
    lines = []; mmpx = VC.S2 / VC.R2 * 1000.0; z = {}
    for k in SP.FEATS:
        f = P['features'][k]; v = m['scalars'][k]
        if v is not None: z[k] = (v - f['median']) / f['sigma']
    zr = zrad(m, P); th = np.array(P['theta_grid'])
    lines.append('%s: side silhouette H (crown-menton) %.0f mm, depth nose-to-occiput %.2f H (prior %.2f +- %.2f)' % (name, m['H_px'] * mmpx, m['scalars']['depth'], P['features']['depth']['median'], P['features']['depth']['sigma']))
    seg = {}
    for nm, a, b in SEG:
        s = (th >= a) & (th <= b); seg[nm] = dict(mean_z=float(zr[s].mean()), worst_z=float(zr[s][np.argmax(np.abs(zr[s]))]), worst_theta=int(th[s][np.argmax(np.abs(zr[s]))]), mean_dr_mm=float(np.mean((np.array(m['radial']) - np.array(P['radial']['median']))[s]) * m['H_px'] * mmpx))
    lines.append('outline vs prior median (radial, from pivot behind the nose tip at mid head):')
    for nm, d in seg.items():
        w = 'too full' if d['mean_z'] > 0 else 'too flat / inside'
        flag = 'FLAG' if abs(d['mean_z']) >= 3 else ('watch' if abs(d['mean_z']) >= 2 else 'ok')
        lines.append('  %-30s mean %+5.1f sigma (%+5.1f mm)  worst %+5.1f at %d deg  [%s%s]' % (nm, d['mean_z'], d['mean_dr_mm'], d['worst_z'], d['worst_theta'], flag, (': ' + w) if flag != 'ok' else ''))
    pc = P['pca_radial']; x = np.array(m['radial']) - np.array(pc['mean']); pr = np.array(pc['comps']) @ x / np.array(pc['std']); dist = float(np.sqrt((pr ** 2).sum()))
    kk = int(np.argmax(np.abs(pr))); pk = int(np.argmax(np.abs(np.array(pc['comps'][kk])))); sgn = '+' if pr[kk] * np.array(pc['comps'][kk])[pk] > 0 else '-'
    lines.append('whole outline: joint distance %.1f (prior p50 %.1f, p95 %.1f, p99 %.1f); largest component PC%d %+.1f sigma, mostly at %d deg (%s of the median there)' % (dist, pc['dist_p50'], pc['dist_p95'], pc['dist_p99'], kk + 1, pr[kk], th[pk], 'outside' if sgn == '+' else 'inside'))
    lines.append('scalars:')
    flags = []
    for k, zk in sorted(z.items(), key=lambda t: -abs(t[1])):
        if abs(zk) >= 2:
            txt = SP.WORDS[k][0 if zk > 0 else 1]; lines.append('  %-15s %+5.1f sigma (%.3f vs median %.3f): %s' % (k, zk, m['scalars'][k], P['features'][k]['median'], txt)); flags.append(dict(feature=k, z=float(zk), text=txt))
    if not flags: lines.append('  none beyond +-2 sigma')
    return lines, dict(joint_distance=dist, z_scalars=z, z_segments=seg, flags=flags, H_mm=m['H_px'] * mmpx, scalars=m['scalars'], z_radial=zr.tolist())


def overlay(png, m, P, path, name):
    bgr, mask = VC.read_clay(png); a = m['anchors']; H = m['H_px']; xn, top = a['nose_x'], a['top']
    pt = lambda u, v: (xn + u * H, top + v * H)
    th = np.array(P['theta_grid'], float); med, sg = np.array(P['radial']['median']), np.array(P['radial']['sigma'])
    def poly(r):
        return np.array([[xn + (SP.PIV[0] + ri * np.cos(np.radians(t))) * H, top + (SP.PIV[1] - ri * np.sin(np.radians(t))) * H] for ri, t in zip(r, th)], np.float32)
    lo, hi, mid = poly(np.maximum(med - 2 * sg, 0)), poly(med + 2 * sg), poly(med)
    ov = bgr.copy(); band = np.vstack([hi, lo[::-1]]).astype(np.int32); lay = ov.copy(); cv2.fillPoly(lay, [band], (80, 200, 80)); ov = cv2.addWeighted(lay, 0.45, ov, 0.55, 0)
    cv2.polylines(ov, [mid.astype(np.int32)], False, (0, 255, 255), 2, cv2.LINE_AA)
    cs, _ = cv2.findContours(mask.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); cv2.drawContours(ov, cs, -1, (0, 140, 255), 2, cv2.LINE_AA)
    for (u, v), c in (((0, m['nose_v']), (255, 0, 0)),):
        cv2.circle(ov, tuple(int(x) for x in (xn, top + v * H)), 6, c, -1)
    cv2.line(ov, (0, int(top)), (ov.shape[1], int(top)), (255, 255, 0), 1); cv2.line(ov, (0, int(a['menton_row'])), (ov.shape[1], int(a['menton_row'])), (255, 255, 0), 1)
    cv2.circle(ov, tuple(int(x) for x in pt(*SP.PIV)), 5, (255, 255, 255), -1)
    cv2.putText(ov, '%s  orange = mesh, yellow = prior median, green band = +-2 sigma, yellow rules = crown / menton' % name, (8, 20), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
    cv2.imwrite(path, ov)


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('run_dir'); ap.add_argument('--tag', default=None); ap.add_argument('--force-clay', action='store_true'); a = ap.parse_args()
    run = VC.Run(a.run_dir, a.tag); out = run.out + 'side_check'; os.makedirs(out, exist_ok=True); P = json.load(open(SP.PRIOR))
    render(run, out, a.force_clay); _, mask = VC.read_clay(out + '/clay_side.png'); m = SP.measure(mask)
    if m is None: raise SystemExit('side silhouette anchors failed for ' + run.name)
    lines, js = report(run.name, m, P); txt = '\n'.join(lines); print(txt)
    open(out + '/side_check.txt', 'w').write(txt + '\n'); json.dump(js, open(out + '/side_check.json', 'w'), indent=1)
    overlay(out + '/clay_side.png', m, P, out + '/side_overlay.png', run.name); print('wrote', out)


if __name__ == '__main__':
    main()
