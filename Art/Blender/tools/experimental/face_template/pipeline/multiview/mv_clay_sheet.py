"""Clay sheet (front, 3/4, side, back, top) of a head-frame mesh written by mv_silhouette_iou.py (<label>_headframe.npz).
usage (trellis2 venv python): python mv_clay_sheet.py <label_headframe.npz> <out_prefix>   -> <out_prefix>_sheet.png (+ clay_*.png)
Scales the mesh to the clay_views.py frame (crown-to-neck-cut 0.26 m, crown at z 1.835), renders with ../vision/clay_views.py (Blender Workbench), back view = mesh turned 180 deg."""
import sys, os, subprocess, numpy as np, cv2
src, pref = sys.argv[1:3]; here = os.path.dirname(os.path.abspath(__file__)); B = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'; CV = os.path.join(here, '..', 'vision', 'clay_views.py')
d = np.load(src); v = d['v'].astype(float); f = d['f']; s = 0.26 / (v[:, 2].max() - v[:, 2].min()); v = v * s; v[:, 2] += 1.835 - v[:, 2].max(); v[:, 0] -= 0.5 * (v[:, 0].min() + v[:, 0].max()); v[:, 1] -= v[:, 1].mean()
np.savez(pref + '_pipe.npz', v=v, f=f); c = v.copy(); c[:, 0] *= -1; c[:, 1] *= -1; np.savez(pref + '_pipe_back.npz', v=c, f=f[:, ::-1])
for npz, p, views in ((pref + '_pipe.npz', pref + '_clay', 'front,q34,side,top'), (pref + '_pipe_back.npz', pref + '_clayb', 'front')):
    r = subprocess.run([B, '-b', '--factory-startup', '--python', CV, '--', npz, p, views, '700'], capture_output=True, text=True); assert 'CLAY_DONE' in r.stdout, r.stdout[-500:]
ims = [cv2.imread(pref + n) for n in ('_clay_front.png', '_clay_q34.png', '_clay_side.png', '_clayb_front.png', '_clay_top.png')]
cv2.imwrite(pref + '_sheet.png', np.hstack([cv2.resize(i, (500, 500)) for i in ims])); print('wrote', pref + '_sheet.png')
