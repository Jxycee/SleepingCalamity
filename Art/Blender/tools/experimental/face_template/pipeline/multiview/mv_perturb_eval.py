"""Circularity test: does the pipeline recover a skull that differs from the template? (mv2 = perturbed guide, see mv_perturb_template.py)
usage (trellis2 venv python):
  python mv_perturb_eval.py <run_dir> <recon_dir> <cutout_dir> <tag> --guide_orig <dir with sil_*.png of the unperturbed guide> --guide_pert <dir with sil_*.png of the perturbed guide> --views <views_dir> --fitted <wrap_mesh.npz> [--out report.json]
Neck-independent skull numbers from the LEFT (nose to image left) and FRONT silhouettes, in units of H = crown-to-menton of the left silhouette (side_prior.measure anchors, the same code as the side metric):
  depth/H (nose tip to occiput), skull width/H (median row width between 15 and 40 % of H under the crown, ears excluded by the row range), cephalic index = width / depth, plus the side joint distance.
Rows: original guide, perturbed guide (the ground truth the generator was told to follow), the generated views (SDXL), the TRELLIS multi-view mesh, the fitted head. 'follows' = (x - original) / (perturbed - original): 1 = on the perturbed guide, 0 = on the template."""
import sys, os, json, argparse, numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/'); sys.path.insert(0, HERE); sys.path.insert(0, HERE + '/..')
import mv_eval as ME, mv_common as MC, side_prior as SP


def dims(left, front, P):
    m = SP.measure(left)
    if m is None: return None
    H = m['H_px']; top = int(np.where(front.any(1))[0][0]); rows = [r for r in range(int(top + .15 * H), int(top + .40 * H)) if r < front.shape[0] and front[r].any()]
    w = float(np.median([np.ptp(np.where(front[r])[0]) for r in rows])) / H; dep = m['scalars']['depth']
    VC_S2, VC_R2 = 0.225, H; MC.VC.S2, MC.VC.R2 = VC_S2, VC_R2; lines, js = MC.SPM.report('x', m, P)
    return dict(depth_H=round(float(dep), 3), width_H=round(w, 3), cephalic_index=round(w / dep, 3), side_joint=round(js['joint_distance'], 2))


def main():
    ap = argparse.ArgumentParser(); [ap.add_argument(x) for x in ('run', 'recon', 'cuts', 'tag')]; ap.add_argument('--guide_orig', required=True); ap.add_argument('--guide_pert', required=True); ap.add_argument('--views', required=True); ap.add_argument('--fitted', required=True)
    ap.add_argument('--out', default=None); a = ap.parse_args(); E = ME.Evaluator(a.run, a.recon, a.cuts, a.tag); P = MC.prior(); rows = {}
    gl = lambda d, v: cv2.imread('%s/sil_%s.png' % (d, v), 0) > 127
    rows['original_guide'] = dims(gl(a.guide_orig, 'left'), gl(a.guide_orig, 'front'), P); rows['perturbed_guide'] = dims(gl(a.guide_pert, 'left'), gl(a.guide_pert, 'front'), P)
    rows['generated_views'] = dims(MC.seg_view(a.views, a.tag, 'left'), MC.seg_view(a.views, a.tag, 'front'), P)
    def mesh_dims(Ph, f):
        zs = E.hf_of(np.array([[0, 0, E.chin_z - 0.04]]))[0, 2]; ff = f[(Ph[f, 2] > zs).all(1)]; return dims(ME.sil(Ph, ff, 90, E.cams['left'], 900)[0], ME.sil(Ph, ff, 0, E.cams['front'], 900)[0], P)
    rows['trellis_mesh'] = mesh_dims(E.hv, E.hf); z = np.load(a.fitted); rows['fitted_head'] = mesh_dims(E.hf_of(z['v'].astype(float)), z['f'].astype(int))
    o, p = rows['original_guide'], rows['perturbed_guide']
    for k, r in rows.items():
        if r: r['follows_depth'] = round((r['depth_H'] - o['depth_H']) / (p['depth_H'] - o['depth_H']), 2) if abs(p['depth_H'] - o['depth_H']) > 1e-3 else None; r['follows_width'] = round((r['width_H'] - o['width_H']) / (p['width_H'] - o['width_H']), 2) if abs(p['width_H'] - o['width_H']) > 1e-3 else None
        print('%-16s %s' % (k, r))
    if a.out: json.dump(rows, open(a.out, 'w'), indent=1)


if __name__ == '__main__': main()
