"""Head+neck RGBA cutout from ANY studio-grey-background portrait (klein / SDXL / photo), same output convention as mv_make_cutouts.py
(<out_dir>/mv1_<tag>_<view>.png, 2x not applied). usage: python cutout_generic.py <image.png> <out.png> [--headfrac 0.48]
Alpha = Lab distance from the border-median background, skin-saturation gate below the ears (drops grey shirt), largest blob, cut 1.15 H under the crown,
H = headfrac * image height (crown to chin in the studio framing used by klein/SDXL here; measured 0.48)."""
import sys, cv2, numpy as np
from scipy import ndimage as ndi
src, dst = sys.argv[1:3]; hf = float(sys.argv[sys.argv.index('--headfrac') + 1]) if '--headfrac' in sys.argv else 0.48
im = cv2.imread(src); Hh_img, W = im.shape[:2]
lab = cv2.cvtColor(im, cv2.COLOR_BGR2LAB).astype(np.float32)
b = np.concatenate([lab[:12].reshape(-1, 3), lab[:, :12].reshape(-1, 3), lab[:, -12:].reshape(-1, 3)]); bg = np.median(b, 0)
# smooth illumination gradient: fit a plane to border pixels per channel
yy, xx = np.mgrid[:Hh_img, :W]; ys = np.concatenate([np.repeat(np.arange(Hh_img), 12), np.repeat(np.arange(Hh_img), 12), np.tile(np.arange(12), W)]) 
d = np.linalg.norm(lab - bg, axis=2); m = d > 14
hsv = cv2.cvtColor(cv2.GaussianBlur(im, (0, 0), 2), cv2.COLOR_BGR2HSV); skin = hsv[..., 1] > 40
m = ndi.binary_opening(m, iterations=2); n, l, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8)); m = l == 1 + int(np.argmax(st[1:, 4]))
top = int(np.where(m.any(1))[0][0]); H = hf * Hh_img
keep = np.ones_like(m); keep[top + int(0.62 * H):] = skin[top + int(0.62 * H):]; keep[top + int(1.15 * H):] = False
m = m & keep; n, l, st, _ = cv2.connectedComponentsWithStats(m.astype(np.uint8)); m = l == 1 + int(np.argmax(st[1:, 4]))
m = ndi.binary_fill_holes(cv2.morphologyEx(m.astype(np.uint8), cv2.MORPH_OPEN, np.ones((5, 5), np.uint8)).astype(bool))
m = cv2.erode(m.astype(np.uint8), np.ones((3, 3), np.uint8)); a = cv2.GaussianBlur(m * 255, (0, 0), 1.2)
cv2.imwrite(dst, np.dstack([im, a])); print('ok', dst, 'top', top, 'area', int(m.sum()))
