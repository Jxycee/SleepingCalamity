"""Multi-view evaluation of a fitted head against the TRELLIS multi-view shape and the generated views (read-only on the run; writes only to --out).
usage (trellis2 venv python):
  python mv_eval.py <run_dir> <recon_dir> <cutout_dir> <tag_g2> --meshes label=path.npz,label=path.npz ... --out <dir> [--hf recon/fblr_headframe.npz] [--sheet label]
  <run_dir>   pipeline run (fit/target_aligned_full.npz = TRELLIS shape in the template frame, config.resolved.json); meshes are template-frame wraps (v,f), e.g. fit/wrap_rh6_mesh.npz
  <cutout_dir>/mv1_<tag>_<view>.png = the RGBA cutouts of the six generated views (mv_make_cutouts.py)
Per mesh: (1) 3D signed distance of the head vertices to the TRELLIS surface in mm (+ outside), overall and per region (face, ears, crown, back of skull, jaw/neck-side),
(2) silhouette IoU against EACH generated view with ONE shared camera per view (the camera of the TRELLIS mesh itself) and a shared neck clip row,
(3) the side-prior joint distance of the mesh's left silhouette, (4) head aspect (height/width of the front silhouette, % vs the TRELLIS mesh), (5) per-vertex displacement from the previous mesh in the list (the 'drift').
The template-frame -> head-frame similarity is estimated by trimmed ICP of fit/target_aligned_full.npz onto the head-frame TRELLIS mesh (mean residual is printed)."""
import os, sys, json, argparse, numpy as np, cv2
from scipy.spatial import cKDTree
HERE = os.path.dirname(os.path.abspath(__file__)).replace('\\', '/')
sys.path.insert(0, HERE); sys.path.insert(0, HERE + '/..')
import mv_common as MC
from mv_silhouette_iou import YAW, sil_mask

VIEWS = list(YAW)
RES = 384


def umeyama(a, b):
    """s, R, t with b ~ s R a + t"""
    ma, mb = a.mean(0), b.mean(0); A, B = a - ma, b - mb; U, S, Vt = np.linalg.svd(B.T @ A / len(a)); D = np.eye(3)
    if np.linalg.det(U) * np.linalg.det(Vt) < 0: D[2, 2] = -1
    R = U @ D @ Vt; s = (S * np.diag(D)).sum() / (A ** 2).sum() * len(a); return s, R, mb - s * R @ ma


def tpl_to_hf(run_dir, hf_v):
    """template frame -> head frame. Chain: fit/align.json (aligned = s R raw + t), raw = Blender import of the pinned glb = (head frame x 4/zrange-scale) up to a similarity found by a trimmed ICP raw -> head frame (starts from x/4)."""
    a = json.load(open(run_dir + '/fit/align.json')); s_a = a['scale']; Ra = np.array(a['R']); ta = np.array(a['t']); raw = np.load(run_dir + '/fit/target_raw.npz')['v'].astype(float); kd = cKDTree(hf_v)
    s_, R_, t_ = 0.25, np.eye(3), np.zeros(3)
    for _ in range(40):
        p = s_ * raw @ R_.T + t_; d, ix = kd.query(p); k = d < np.percentile(d, 60); s_, R_, t_ = umeyama(raw[k], hf_v[ix[k]])
    d, ix = kd.query(s_ * raw @ R_.T + t_); res = float(np.sqrt((d ** 2).mean()))
    # hf = s_ R_ raw + t_ ; raw = (x - ta) Ra / s_a   ->  hf = (s_/s_a) R_ Ra^T (x - ta) + t_
    Rt = R_ @ Ra.T; S = s_ / s_a; T = t_ - S * Rt @ ta
    return S, Rt, T, res


def sil(v, f, yaw, cam, res=RES):
    th = np.radians(yaw); c = np.array([np.sin(th), -np.cos(th), 0.0]); fwd = -c; right = np.cross(fwd, [0, 0, 1.0]); right /= np.linalg.norm(right)
    x = v @ right; y = v[:, 2]; x0, x1, y0, y1 = cam['x0'], cam['x1'], cam['y0'], cam['y1']; s = 0.9 * (res - 1) / (y1 - y0)
    px = (x - 0.5 * (x0 + x1)) * s + res / 2; py = (y1 - y) * s + 0.05 * res; tri = np.stack([px[f], py[f]], -1); img = np.zeros((res, res), np.uint8)
    cv2.fillPoly(img, np.round(tri * 16).astype(np.int32).reshape(len(f), 3, 2), 255, shift=4)
    from scipy import ndimage as ndi; img = cv2.morphologyEx(img, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)); return ndi.binary_fill_holes(img > 0), (lambda yy: (y1 - yy) * s + 0.05 * res)


def view_cam(hf_v, yaw):
    th = np.radians(yaw); c = np.array([np.sin(th), -np.cos(th), 0.0]); right = np.cross(-c, [0, 0, 1.0]); right /= np.linalg.norm(right); x = hf_v @ right
    return dict(x0=x.min(), x1=x.max(), y0=hf_v[:, 2].min(), y1=hf_v[:, 2].max())


def iou(a, b): return float((a & b).sum() / max((a | b).sum(), 1))


class Evaluator:
    def __init__(s, run_dir, recon_dir, cut_dir, tag, hf='fblr_headframe.npz'):
        s.run = run_dir.rstrip('/'); hf_ = np.load(recon_dir.rstrip('/') + '/' + hf); s.hv = hf_['v'].astype(float); s.hf = hf_['f'].astype(int)
        s.S, s.R, s.T, s.icp = tpl_to_hf(s.run, s.hv); s.cams = {k: view_cam(s.hv, YAW[k]) for k in VIEWS}
        s.masks = {k: sil_mask('%s/mv1_%s_%s.png' % (cut_dir, tag, k), RES) for k in VIEWS}
        cfg = json.load(open(s.run + '/config.resolved.json')); from_env = __import__('paths').Paths(cfg).env(); os.environ.update({k: v for k, v in from_env.items() if k.startswith('SCP_')})
        sys.path.insert(0, HERE + '/../stages'); os.environ['SC_TPL'] = s.run + '/fit/tobin_head_template6.npz'
        from fit_common import Tpl
        t = Tpl(); s.t = t; b = t.basis; zc = float(np.load(s.run + '/fit/target_aligned_full.npz')['zcut']); chin_z = b[t.lm_idx[t.lm_names.index('chin_tip')]][2]; s.chin_z = chin_z
        ear = np.zeros(len(b), bool); ear[t.S('ear_L')] = True; ear[t.S('ear_R')] = True; ear = np.isin(t.rep, t.rep[ear])
        face = (t.rw == 1.0) & (b[:, 2] > zc + 0.01); head = b[:, 2] > chin_z + 0.01; top = b[:, 2].max(); earc = b[ear].mean(0)
        skull = head & ~face & ~ear
        import r6_metrics as MT; s.MT = MT
        s.reg = dict(skin=MT.M['skin'], head=head, face=face & head, ears=ear, crown=skull & (b[:, 2] > top - 0.055), back=skull & (b[:, 1] > earc[1] + 0.01) & (b[:, 2] < top - 0.04) & (b[:, 2] > chin_z + 0.03),
                     side=skull & (np.abs(b[:, 0]) > 0.055) & (b[:, 2] < top - 0.055), jaw=head & ~ear & (b[:, 2] < chin_z + 0.05))
        from trimesh import Trimesh
        s.kd = cKDTree(s.hv); s.nrm = Trimesh(s.hv, s.hf, process=False).vertex_normals; s.P = MC.prior()
        s.neck_row_ref = None

    def hf_of(s, P): return s.S * np.asarray(P, float) @ s.R.T + s.T

    def sdist(s, Ph):
        d, i = s.kd.query(Ph); sg = np.sign(((Ph - s.hv[i]) * s.nrm[i]).sum(1)); return d * sg / s.S * 1e3          # mm in template-frame units
    def evaluate(s, P, f, sheet=None, hf_frame=False):
        Ph = np.asarray(P, float) if hf_frame else s.hf_of(P); sd = s.sdist(Ph); out = dict(rms3d={}, mean3d={}, p95={})
        for k, m in s.reg.items():
            if m.sum() == 0 or len(m) != len(sd): continue
            q = sd[m]; out['rms3d'][k] = round(float(np.sqrt((q ** 2).mean())), 2); out['mean3d'][k] = round(float(q.mean()), 2); out['p95'][k] = round(float(np.percentile(np.abs(q), 95)), 2)
        zclip = s.hf_of(np.array([[0, 0, s.chin_z - 0.015]]))[0, 2]; out['iou'] = {}; ims = {}
        for k in VIEWS:
            m_, rowf = sil(Ph, f, YAW[k], s.cams[k]); r = int(round(rowf(zclip) + 0.5)); a = s.masks[k].copy(); a[r:] = False; m_[r:] = False; out['iou'][k] = round(iou(m_, a), 3); ims[k] = (m_, a)
        out['iou_mean'] = round(float(np.mean(list(out['iou'].values()))), 3)
        # side joint distance (left silhouette, 900 px, shared camera not needed: the prior is scale free)
        try:
            cam = s.cams['left']; zs = s.hf_of(np.array([[0, 0, s.chin_z - 0.04]]))[0, 2]; sl, _ = sil(Ph, f[(Ph[f, 2] > zs).all(1)], 90, cam, 900); m_, lines, js = MC.score_profile('fit', sl, s.P)
            out['side_joint'] = round(js['joint_distance'], 2) if js else None; out['side_flags'] = [x['feature'] + ' %+.1f' % x['z'] for x in js['flags']] if js else []
        except Exception as e: out['side_joint'] = None; out['side_err'] = repr(e)
        # aspect: crown-to-chin height over skull width (median row width between 20 % and 50 % of that height) of the front silhouette, as % difference from the TRELLIS mesh
        def asp(q, rc):
            ys, xs = np.where(q); top = ys.min(); H = rc - top; w = [np.ptp(np.where(q[r])[0]) for r in range(int(top + .2 * H), int(top + .5 * H)) if q[r].any()]; return H / float(np.median(w))
        rc = int(round(sil(Ph, f, 0, s.cams['front'])[1](s.hf_of(np.array([[0, 0, s.chin_z]]))[0, 2]))); tm, _ = sil(s.hv, s.hf, 0, s.cams['front']); out['aspect_err_pct'] = round(100 * (asp(ims['front'][0], rc) / asp(tm, rc) - 1), 2)
        out['_ims'] = ims; out['_sd'] = sd; return out


def panel(ims, title):
    cells = []
    for k in VIEWS:
        m_, a = ims[k]; im = np.zeros((RES, RES, 3), np.uint8); im[a] = (0, 0, 200); im[m_ & ~a] = (0, 190, 0); im[m_ & a] = (200, 200, 200)       # red = view only, green = fitted only, grey = both
        cv2.putText(im, k, (6, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA); cells.append(im)
    g = np.vstack([np.hstack(cells[:3]), np.hstack(cells[3:])]); cv2.putText(g, title, (6, g.shape[0] - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 0), 1, cv2.LINE_AA); return g


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('run'); ap.add_argument('recon'); ap.add_argument('cuts'); ap.add_argument('tag'); ap.add_argument('--meshes', required=True); ap.add_argument('--out', required=True); ap.add_argument('--hf', default='fblr_headframe.npz'); ap.add_argument('--sheet', default='')
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True); E = Evaluator(a.run, a.recon, a.cuts, a.tag, a.hf); print('template->head frame: scale %.4f, trimmed-ICP residual %.4f (head-frame units)' % (E.S, E.icp))
    res = {}; prev = None; lines = []
    meshes = ('trellis=@hf,' + a.meshes).split(',')
    for item in meshes:
        lab, path = item.split('=', 1)
        if path == '@hf': P, f = E.hv, E.hf; r = E.evaluate(P, f, hf_frame=True); drift = None; prev = None
        else:
            z = np.load(path); P = z['v'].astype(float); f = z['f'].astype(int); r = E.evaluate(P, f); drift = None
        if path != '@hf' and prev is not None and prev.shape == P.shape:
            d = np.linalg.norm(P - prev, axis=1) * 1e3; drift = {k: [round(float(d[m].mean()), 2), round(float(d[m].max()), 2)] for k, m in E.reg.items() if m.sum()}
        prev = None if path == '@hf' else P
        if lab in a.sheet.split(','): cv2.imwrite('%s/iou_%s.png' % (a.out, lab), panel(r['_ims'], '%s  mean IoU %.3f  side %.1f' % (lab, r['iou_mean'], r['side_joint'] or -1)))
        r.pop('_ims'); r.pop('_sd'); r['drift_from_prev_mm_mean_max'] = drift; res[lab] = r
        lines.append('%-14s IoU mean %.3f  [%s]  side joint %s  aspect %+.1f%%  3D rms mm %s  drift mm(mean,max) %s' % (lab, r['iou_mean'], ' '.join('%s %.3f' % (k, v) for k, v in r['iou'].items()), r['side_joint'], r['aspect_err_pct'],
                     r['rms3d'], drift if drift else '-'))
        print(lines[-1], flush=True)
    json.dump(res, open(a.out + '/eval.json', 'w'), indent=1); open(a.out + '/eval.txt', 'w').write('\n'.join(lines) + '\n')


if __name__ == '__main__': main()
