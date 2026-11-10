"""6-view comparison sheet: generated view (top row, cut out, registered to the shared camera) over the fitted head as a normal-shaded clay (bottom row) with the generated silhouette drawn on it in cyan.
usage (trellis2 venv python): python mv_view_sheet.py <run_dir> <recon_dir> <cutout_dir> <tag> <out.png> [--mesh wrap_mesh.npz] [--label text]
Same camera as mv_eval.py (TRELLIS bounding box normalisation, neck clip at the chin), painter's algorithm with Lambert shading from the camera direction (no ambient occlusion: concavities look flat)."""
import sys, os, argparse, numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/'); sys.path.insert(0, HERE); sys.path.insert(0, HERE + '/..')
import mv_eval as ME
RES = 384


def clay(Ph, f, yaw, cam, zclip):
    th = np.radians(yaw); c = np.array([np.sin(th), -np.cos(th), 0.0]); right = np.cross(-c, [0, 0, 1.0]); right /= np.linalg.norm(right); x = Ph @ right; y = Ph[:, 2]; s = 0.9 * (RES - 1) / (cam['y1'] - cam['y0'])
    px = (x - 0.5 * (cam['x0'] + cam['x1'])) * s + RES / 2; py = (cam['y1'] - y) * s + 0.05 * RES; dep = Ph @ c; tri = f[(Ph[f, 2] > zclip - 0.03).all(1)]
    n = np.cross(Ph[tri[:, 1]] - Ph[tri[:, 0]], Ph[tri[:, 2]] - Ph[tri[:, 0]]); n /= np.linalg.norm(n, axis=1)[:, None] + 1e-12; facing = n @ c; tri = tri[facing > -0.05]; n = n[facing > -0.05]
    key = np.array([0.35, -0.55, 0.75]); key /= np.linalg.norm(key); lit = np.clip(0.25 + 0.55 * np.clip(n @ c, 0, 1) + 0.35 * np.clip(n @ (np.cross(np.cross(c, [0, 0, 1.0]), c) * 0 + c * 0.6 + np.array([0, 0, 0.8])), 0, 1), 0, 1)
    order = np.argsort(dep[tri].mean(1)); img = np.full((RES, RES, 3), 60, np.uint8)
    for i in order:
        p = np.round(np.stack([px[tri[i]], py[tri[i]]], 1) * 16).astype(np.int32); g = int(60 + 170 * lit[i]); cv2.fillConvexPoly(img, p, (g, g, g), cv2.LINE_8, 4)
    return img


def main():
    ap = argparse.ArgumentParser(); [ap.add_argument(x) for x in ('run', 'recon', 'cuts', 'tag', 'out')]; ap.add_argument('--mesh', default=None); ap.add_argument('--label', default=''); a = ap.parse_args()
    E = ME.Evaluator(a.run, a.recon, a.cuts, a.tag); z = np.load(a.mesh or a.run + '/fit/wrap_rh6_mesh.npz'); Ph = E.hf_of(z['v'].astype(float)); f = z['f'].astype(int); zclip = E.hf_of(np.array([[0, 0, E.chin_z - 0.015]]))[0, 2]
    top, bot = [], []
    for v in ME.VIEWS:
        im = cv2.imread('%s/mv1_%s_%s.png' % (a.cuts, a.tag, v), cv2.IMREAD_UNCHANGED); al = im[..., 3] > 127; ys, xs = np.where(al); s = 0.9 * (RES - 1) / (ys.max() - ys.min())
        M = np.array([[s, 0, RES / 2 - 0.5 * (xs.min() + xs.max()) * s], [0, s, 0.05 * RES - ys.min() * s]], np.float32)
        rgb = cv2.warpAffine(im[..., :3], M, (RES, RES), borderValue=(60, 60, 60)); m = cv2.warpAffine(al.astype(np.uint8) * 255, M, (RES, RES)) > 127; rgb[~m] = (60, 60, 60)
        r = int(round((E.cams[v]['y1'] - zclip) * s * 0 + 0)); rowz = lambda zz: (E.cams[v]['y1'] - zz) * 0.9 * (RES - 1) / (E.cams[v]['y1'] - E.cams[v]['y0']) + 0.05 * RES; rc = int(rowz(zclip)); rgb[rc:] = (60, 60, 60); m[rc:] = False
        cl = clay(Ph, f, ME.YAW[v], E.cams[v], zclip); cl[rc:] = (60, 60, 60); cnt, _ = cv2.findContours(m.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); cv2.drawContours(cl, cnt, -1, (255, 255, 0), 1)
        for im_, t in ((rgb, v + ' (generated)'), (cl, v + ' (fit)')): cv2.putText(im_, t, (6, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)
        top.append(rgb); bot.append(cl)
    g = np.vstack([np.hstack(top), np.hstack(bot)]); cv2.putText(g, a.label, (6, g.shape[0] - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 255), 1, cv2.LINE_AA); cv2.imwrite(a.out, g); print('wrote', a.out)


if __name__ == '__main__': main()
