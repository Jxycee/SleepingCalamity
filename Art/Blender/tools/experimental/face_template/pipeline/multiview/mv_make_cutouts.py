"""Cut the generated turnaround tiles into head+neck RGBA images for TRELLIS.2 multi-view input.
usage (facekeys python): python mv_make_cutouts.py <views_dir> <tag> <out_dir> [--scale 2]
Alpha = mv_common.seg_view mask (Lab background fit), shirt cut ~1.15 H under the crown, 1px erode, 2x Lanczos. Writes <out_dir>/mv1_<tag>_<view>.png (all 6 views) + <tag>_cutouts.png sheet."""
import sys, os, cv2, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mv_common as MC
vd, tag, od = sys.argv[1:4]; sc = 2; os.makedirs(od, exist_ok=True); cells = []
for v in MC.VIEWS:
    im = cv2.imread(f'{vd}/{tag}_{v}.png'); m = MC.seg_view(vd, tag, v); rows = np.where(m.any(1))[0]; top = int(rows[0]); Hh = 250                           # nominal H px of the tile (guide H 240)
    hsv = cv2.cvtColor(cv2.GaussianBlur(im, (0, 0), 2), cv2.COLOR_BGR2HSV); skin = hsv[..., 1] > 40; keep = np.ones_like(m); keep[top + int(0.62 * Hh):] = skin[top + int(0.62 * Hh):]   # below the ears only skin-coloured pixels (drops grey shirt / backdrop wedges)
    m = m & keep; n, lab, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8)); m = lab == (1 + int(np.argmax(st[1:, 4]))); m = cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8)).astype(bool); m = __import__("scipy.ndimage", fromlist=["x"]).binary_fill_holes(m)
    m = cv2.erode(m.astype(np.uint8), np.ones((3, 3), np.uint8)).astype(bool)
    big = cv2.resize(im, None, fx=sc, fy=sc, interpolation=cv2.INTER_LANCZOS4); mb = cv2.resize(m.astype(np.uint8) * 255, None, fx=sc, fy=sc, interpolation=cv2.INTER_LINEAR)
    mb = cv2.GaussianBlur(mb, (0, 0), 1.2); rgba = np.dstack([big, mb]); cv2.imwrite(f'{od}/mv1_{tag}_{v}.png', rgba)
    cells.append(np.where(mb[..., None] > 127, big, 40).astype(np.uint8)[::2, ::2])
cv2.imwrite(f'{vd}/{tag}_cutouts.png', np.vstack([np.hstack(cells[:3]), np.hstack(cells[3:])])); print('ok')
