"""Silhouette IoU with a neck-clip search: a reconstruction that includes shoulders (Hunyuan/klein) cannot be compared to cutouts with a fixed neck cut unless the mesh is clipped.
Takes the <label>_headframe.npz written by mv_silhouette_iou.py (frame already solved), clips at fraction f of the mesh height (crown down) for f in a grid,
reports per-view IoU at the f that maximises the mean TRAIN IoU, then the side-prior joint distance of the clipped left silhouette.
usage: python iou_clip.py <headframe.npz> <cutout_dir> <tag> <out_dir> <label> [--train front,back,left,right]"""
import sys, os, json, numpy as np, cv2
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import mv_silhouette_iou as M
npz, cut, tag, out, label = sys.argv[1:6]; os.makedirs(out, exist_ok=True)
train = (sys.argv[sys.argv.index('--train') + 1] if '--train' in sys.argv else 'front,back,left,right').split(',')
d = np.load(npz); v, f = d['v'].astype(np.float64), d['f'].astype(np.int64)
masks = {k: M.sil_mask(f'{cut}/mv1_{tag}_{k}.png') for k in M.YAW}
zt, zb = v[:, 2].max(), v[:, 2].min(); best = None; rows = []
for fr in np.arange(0.40, 0.96, 0.025):
    zc = zt - fr * (zt - zb); keep = (v[f, 2] > zc).all(1); ff = f[keep]
    if len(ff) < 1000: continue
    t = {k: M.iou(M.sil_mesh(v, ff, M.YAW[k], res=256), masks[k]) for k in M.YAW}; m = float(np.mean([t[k] for k in train])); rows.append((fr, m))
    if best is None or m > best[0]: best = (m, fr, t, ff)
m, fr, t, ff = best
L = ['%s: neck clip fraction %.3f of mesh height, mean TRAIN IoU %.3f' % (label, fr, m)] + ['  %-6s %.3f %s' % (k, t[k], 'train' if k in train else 'HELD-OUT') for k in M.YAW]
L.append('  mean all six %.3f' % np.mean(list(t.values())))
try:
    import mv_common as MC
    P = MC.prior(); sl = M.sil_mesh(v, ff, 90, res=900); m_, lines, js = MC.score_profile('MESH left silhouette (clipped)', sl, P); L += [''] + (lines or ['side prior failed'])
except Exception as e: L.append('side prior skipped %r' % e)
txt = '\n'.join(L); print(txt); open(f'{out}/{label}_clip_iou.txt', 'w').write(txt + '\n')
cells = []
for k in M.YAW:
    ms = M.sil_mesh(v, ff, M.YAW[k]); im = np.zeros((256, 256, 3), np.uint8); im[masks[k]] = (0, 0, 220); im[ms & ~masks[k]] = (0, 200, 0); im[ms & masks[k]] = (60, 200, 220)
    cv2.putText(im, '%s %.2f' % (k, t[k]), (6, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1); cells.append(im)
cv2.imwrite(f'{out}/{label}_clip_sheet.png', np.vstack([np.hstack(cells[:3]), np.hstack(cells[3:])]))
