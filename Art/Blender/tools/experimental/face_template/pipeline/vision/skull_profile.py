"""Skull / crown silhouette profile, mesh render vs photo mask (round 15). Read-only.
usage: python skull_profile.py <run_dir> [<run_dir> ...] [--overlay out.png]   (first run only for --overlay)
Half-width of the head silhouette in inter-ocular units at fixed fractions of the crown-to-chin height (0 = crown), both in the photo frame (render warped by the 4-eye-corner similarity, the scorer's registration):
the table says WHERE the mesh differs from the photo mask (crown tip 1-3 %, dome 5-15 %, temple 20-40 %). --overlay writes the top 45 % of the head with the photo mask contour (green) and the mesh contour (orange), 2x.
Reading (round 15): on all four characters the mesh stays within about 1-2 % IO of the mask over 3-40 % (largest: char4 crown tip 1-3 %, the mesh top is 5 px higher in the photo frame); so a pointed / tall plausibility flag that the photo shares is the photo, not the fit."""
import os, sys, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vcommon as VC
FR = (0.01, 0.02, 0.03, 0.05, 0.07, 0.10, 0.15, 0.20, 0.30, 0.40)

def masks(run):
    M = VC.sim_render_to_photo(run); pm = VC.photo_head_mask(run); H, W = pm.shape
    rm = VC.bg_mask(cv2.imread(run.rn)); rmw = cv2.warpAffine(rm.astype(np.uint8), M, (W, H), flags=cv2.INTER_NEAREST).astype(bool)
    return pm, rmw

def profile(run):
    pm, rmw = masks(run); io = np.linalg.norm(run.lm_photo[33] - run.lm_photo[263]); y152 = run.lm_photo[152][1]; out = {}
    for nm, m in (('photo', pm), ('mesh', rmw)):
        top = np.where(m.sum(1) >= 3)[0].min(); Hh = y152 - top; r = []
        for f in FR:
            xs = np.where(m[int(round(top + f * Hh))])[0]; r.append((xs.max() - xs.min()) / 2 / io)
        out[nm] = dict(top=int(top), height=float(Hh), half_width_io=r)
    return out

def overlay(run, path):
    pm, rmw = masks(run); img = run.photo.copy()
    for m, c in ((pm.astype(np.uint8), (0, 255, 0)), (rmw.astype(np.uint8), (0, 140, 255))):
        cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); cv2.drawContours(img, cs, -1, c, 1)
    top = np.where(pm.any(1))[0].min(); y1 = int(top + 0.45 * (run.lm_photo[152][1] - top))
    cv2.imwrite(path, cv2.resize(img[max(top - 15, 0):y1], None, fx=2, fy=2, interpolation=cv2.INTER_CUBIC))

if __name__ == '__main__':
    a = sys.argv[1:]; ov = a[a.index('--overlay') + 1] if '--overlay' in a else None; dirs = [x for i, x in enumerate(a) if not x.startswith('--') and (i == 0 or a[i - 1] != '--overlay')]
    for d in dirs:
        run = VC.Run(d); o = profile(run)
        print('%s: crown row photo %d mesh %d, crown-to-chin px photo %.0f mesh %.0f' % (run.name, o['photo']['top'], o['mesh']['top'], o['photo']['height'], o['mesh']['height']))
        print('  frac   ' + '  '.join('%5.2f' % f for f in FR))
        for nm in ('photo', 'mesh'): print('  %-6s ' % nm + '  '.join('%5.3f' % v for v in o[nm]['half_width_io']))
        print('  diff %  ' + '  '.join('%+5.1f' % (100 * (m / p - 1)) for m, p in zip(o['mesh']['half_width_io'], o['photo']['half_width_io'])))
        if ov and d == dirs[0]: overlay(run, ov)
