"""Round 2: project the photo onto Tobin's head UV (numpy, CPU, deterministic). venv python (numpy scipy cv2 skimage PIL).
 wrapped head (full basis) -> per-texel 3D position/normal (UV raster) -> front-ortho render px (analytic, depth test) -> photo px (landmark map, c2_01_mapfit)
 -> de-lit photo colour (divide per channel by a masked sigma-40 blur of the skin, multiply by the mean skin colour) -> facing/visibility weight
 -> unseen texels = blurred projected tone + Tobin high-frequency detail -> neck matched to a tinted Body texture.
 writes bake/Head_BaseColor_cloud.png, bake/Body_BaseColor_cloud.png, bake/_proj_debug.png, bake/texture.json"""
from c2_common import *
from c2_01_mapfit import fit, make_map
import json, cv2
from PIL import Image
from skimage.color import rgb2lab, lab2rgb
from scipy import ndimage as ndi
R = 2048
t = Tpl(); z = t.z; S = t.S
faces = t.faces; uvl = z['head/loop_uv'].reshape(-1, 3, 2); assert (z['head/loop_vertex'] == faces.ravel()).all()
basis = np.load(B2 + 'head_variants.npz')['full_basis'].astype(np.float64)
Lab = lambda a: rgb2lab(np.clip(a, 0, 1).astype(np.float32))
Rgb8 = lambda l: (np.clip(lab2rgb(l), 0, 1) * 255 + 0.5).astype(np.uint8)
srgb2lin = lambda c: np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
lin2srgb = lambda c: np.where(c <= 0.0031308, c * 12.92, 1.055 * np.clip(c, 0, None) ** (1 / 2.4) - 0.055)

print('uv raster...'); fid, bary = raster_uv(faces, uvl, R); cov = fid >= 0
pos = interp(fid, bary, faces, basis).astype(np.float32)
nor = interp(fid, bary, faces, vertex_normals(basis, faces)).astype(np.float32); nor /= np.linalg.norm(nor, axis=2, keepdims=True) + 1e-9
zmap = pos[..., 2]
def vmask(idx):
    v = np.zeros(len(basis)); v[np.asarray(idx).astype(int)] = 1; return interp(fid, bary, faces, v)
touch = lambda idx: vmask(idx) > 0.01
feather = lambda m, grow, sig: np.clip(ndi.gaussian_filter((ndi.binary_dilation(m, iterations=grow) if grow else m).astype(np.float32), sig), 0, 1)
eye_uv = touch(S('eye_socket')) | touch(S('eyelid_ring_L')) | touch(S('eyelid_ring_R'))
mouth = touch(S('mouth_interior'))
ear = touch(S('ear_L')) | touch(S('ear_R')); nostril = touch(S('nostril'))
lip = touch(np.concatenate([S('lip_patch_upper_verts'), S('lip_patch_lower_verts')])) & ~mouth

# ---------------- camera / visibility
cam = json.load(open(B2 + 'wrap_cam.json')); Rr, Sc, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
sx = lambda x: ((x - cx) / Sc + 0.5) * Rr
sy = lambda zz: (0.5 - (zz - cz) / Sc) * Rr
print('depth map...'); dm = depth_map(basis, faces, sx(basis[:, 0]), sy(basis[:, 2]), Rr)
rx = sx(pos[..., 0]); ry = sy(pos[..., 2])
ix = np.clip(np.round(rx).astype(int), 0, Rr - 1); iy = np.clip(np.round(ry).astype(int), 0, Rr - 1)
dmin = np.minimum.reduce([dm[np.clip(iy + a, 0, Rr - 1), np.clip(ix + b, 0, Rr - 1)] for a in (-1, 0, 1) for b in (-1, 0, 1)])
vis = cov & (pos[..., 1] <= dmin + 0.0025)
wface = smoothstep(0.25, 0.75, -nor[..., 1])

# ---------------- photo map
ids, src, dst, keep, M, tps = fit(10000.0); fmap = make_map(M, tps, src[keep])
sel = cov & vis & (wface > 0.02)
pp = np.zeros((R, R, 2), np.float32); pp[sel] = fmap(np.c_[rx[sel], ry[sel]])
photo = np.array(Image.open(TGT + 'crop_src.png').convert('RGB')).astype(np.float32) / 255; H, W = photo.shape[:2]
lm = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]
inb = (pp[..., 0] > 2) & (pp[..., 0] < W - 3) & (pp[..., 1] > 2) & (pp[..., 1] < H - 3)

# ---------------- photo masks
hsv = cv2.cvtColor((photo * 255).astype(np.uint8), cv2.COLOR_RGB2HSV)
skin_hue = ~((hsv[..., 0] >= 80) & (hsv[..., 0] <= 140) & (hsv[..., 1] > 70)) & (hsv[..., 2] > 18)   # anything but teal/navy: the shadow side is teal-tinted
head_m = np.zeros((H, W), np.uint8); q = pp[sel & inb]; head_m[np.clip(q[:, 1].astype(int), 0, H - 1), np.clip(q[:, 0].astype(int), 0, W - 1)] = 1
head_m = cv2.morphologyEx(head_m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21))); head_m = ndi.binary_fill_holes(head_m).astype(bool)
RING = dict(eyeR=[33,7,163,144,145,153,154,155,133,173,157,158,159,160,161,246], eyeL=[362,382,381,380,374,373,390,249,263,466,388,387,386,385,384,398],
            browR=[70,63,105,66,107,55,65,52,53,46], browL=[300,293,334,296,336,285,295,282,283,276],
            lips=[61,146,91,181,84,17,314,405,321,375,291,409,270,269,267,0,37,39,40,185])
def poly(ids_, dil):
    m = np.zeros((H, W), np.uint8); cv2.fillPoly(m, [np.round(lm[ids_]).astype(np.int32)], 1)
    return cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * dil + 1, 2 * dil + 1))).astype(bool)
eye_ph = poly(RING['eyeR'], 7) | poly(RING['eyeL'], 7)
feat = eye_ph | poly(RING['browR'], 6) | poly(RING['browL'], 6) | poly(RING['lips'], 4)
for k in (1, 2, 4, 5, 98, 327, 129, 358):   # nose tip / nostril area
    cv2.circle(feat.view(np.uint8), tuple(np.round(lm[k]).astype(int)), 9, 1, -1)
mask0 = skin_hue & head_m & ~feat
mask0 = ndi.binary_opening(mask0, iterations=2)

# ---------------- de-light: divide by a heavy blur of the skin, multiply by the mean skin colour
lin = srgb2lin(photo).astype(np.float32)
def mblur(x, m, s):
    mm = m.astype(np.float32); den = cv2.GaussianBlur(mm, (0, 0), s)
    num = np.stack([cv2.GaussianBlur(x[..., k] * mm, (0, 0), s) for k in range(x.shape[2])], -1)
    return num / np.maximum(den, 1e-6)[..., None], den
SIG = 40.0   # brief: ~40
m = mask0.copy()
bl, den = mblur(lin, m, SIG)
far, _ = mblur(lin, m, 120.0); bl = np.where((den > 0.03)[..., None], bl, far)
ratio = lin.mean(2) / np.maximum(bl.mean(2), 1e-4)
lum = bl.mean(2)[mask0]; lo, hi = np.percentile(lum, [62, 72])   # lit-skin tone (not the shadow side)
meanskin = np.median(bl[mask0][(lum >= lo) & (lum <= hi)], 0)
feat_d = ndi.binary_dilation(feat, iterations=4)
rel = lin / np.maximum(bl, 1e-4)
rel = np.where(feat_d[..., None], np.clip(rel, 0.3, 1.6), np.clip(rel, 0.6, 1.5))   # keep brows/lips/nostrils dark, flatten socket and cheek shadow
GAIN = 1.05; meanskin = meanskin * GAIN       # albedo a bit above the dim photo exposure
delit = np.clip(rel * meanskin, 0, 0.95).astype(np.float32)
mean_rgb = lin2srgb(meanskin); print('mean skin srgb', (mean_rgb * 255).round(1))
Lc = Lab(mean_rgb[None, None].astype(np.float32))[0, 0]
skin_b = ndi.binary_closing(skin_hue & head_m, iterations=10) & head_m   # close small teal-shadow holes inside the face
skin_ok = cv2.GaussianBlur(ndi.binary_erosion(skin_b, iterations=1).astype(np.float32), (0, 0), 1.5)

# ---------------- sample photo at texels
mx, my = pp[..., 0].astype(np.float32), pp[..., 1].astype(np.float32)
samp = lambda im, interp_=cv2.INTER_LINEAR: cv2.remap(im, mx, my, interp_, borderMode=cv2.BORDER_REFLECT)
proj = samp(delit); proj_raw = samp(srgb2lin(photo).astype(np.float32))
s_skin = samp(skin_ok); s_eye = samp(cv2.GaussianBlur(eye_ph.astype(np.float32), (0, 0), 2))
chin = basis[t.lm_idx[t.lm_names.index('chin_tip')], 2]
chin_f = np.clip((zmap - (chin - 0.004)) / 0.012, 0, 1)
w = wface * vis * inb * s_skin * chin_f * (1 - s_eye)
w = w * (1 - feather(eye_uv, 10, 8)) * (1 - feather(mouth, 4, 4))
w = np.where(ear, np.minimum(w, 0.35), w); w[~cov] = 0
w = np.clip(cv2.GaussianBlur(w, (0, 0), 3), 0, 1) * cov
print('projected texels w>0.5:', int((w > 0.5).sum()), 'of', int(cov.sum()))

# ---------------- Tobin texture, tinted to the photo skin tone
tob = np.array(Image.open(TEX + 'Head_BaseColor.png').convert('RGB')).astype(np.float32) / 255; assert tob.shape[:2] == (R, R)
body_tex = np.array(Image.open(TEX + 'Body_BaseColor.png').convert('RGB')).astype(np.float32) / 255
tl = Lab(tob); face_area = cov & (zmap > 1.58) & ~eye_uv & ~mouth & ~lip & ~ear & ~nostril; Lt = np.median(tl[face_area], 0)
def darken(l):
    o = l.copy(); o[..., 0] = l[..., 0] * (Lc[0] / Lt[0]); o[..., 1:] = l[..., 1:] + (Lc[1:] - Lt[1:]); return o
tob_dark = darken(tl); body_dark = darken(Lab(body_tex)); print('tobin Lab', Lt, 'photo skin Lab', Lc)
proj_lab = Lab(np.clip(lin2srgb(proj), 0, 1))

# ---------------- unseen fill: weighted blur of the projected tone (+ Tobin detail)
def lblur(x, ww, s):
    den = cv2.GaussianBlur(ww.astype(np.float32), (0, 0), s)
    return np.stack([cv2.GaussianBlur(x[..., k] * ww, (0, 0), s) for k in range(3)], -1) / np.maximum(den, 1e-6)[..., None], den
wv = ndi.binary_erosion(w > 0.6, iterations=2) & ~ndi.binary_dilation(eye_uv | mouth | ear, iterations=6)
low60, d60 = lblur(proj_lab, wv, 60); low250, _ = lblur(proj_lab, wv, 250)
fill_low = np.where((d60 > 0.02)[..., None], low60, low250)
detail = tob_dark - np.stack([cv2.GaussianBlur(tob_dark[..., k], (0, 0), 25) for k in range(3)], -1)
detail[..., 0] = np.clip(detail[..., 0], -10, 6); detail[..., 1:] = np.clip(detail[..., 1:], -6, 6)
patch = fill_low + detail
# the sides/back keep the tone blur only partly: far from the face fall back on the mean skin tone so the back of the head cannot drift
far_w = np.clip((d60 - 0.02) / 0.1, 0, 1)[..., None]
patch = patch * far_w + (np.broadcast_to(Lc, patch.shape) + detail) * (1 - far_w)
comb = proj_lab * w[..., None] + patch * (1 - w[..., None])
mouth_w = feather(mouth, 4, 4)[..., None]; comb = comb * (1 - mouth_w) + (tob_dark) * mouth_w   # inside of the mouth: tinted Tobin

# ---------------- neck match
bz = z['body/basis'][:, 2]; bf = z['body/faces']; bnk = np.where(bz > 1.485)[0]
bu = z['body/loop_uv'].reshape(-1, 3, 2)[np.isin(bf, bnk).any(1)].reshape(-1, 2)
bpx = np.clip((bu * [R, R]).astype(int), 0, R - 1); bpx[:, 1] = R - 1 - bpx[:, 1]
body_neck = np.median(body_dark[bpx[:, 1], bpx[:, 0]], 0); zr = zmap[cov].min()
head_neck = np.median(comb[cov & (zmap < zr + 0.012)], 0); shift = body_neck - head_neck
wn = np.clip(1 - (zmap - zr) / 0.05, 0, 1) ** 1.5 * cov
comb = comb + shift[None, None] * wn[..., None]; print('neck shift Lab', shift)
res = Rgb8(comb)
idx = ndi.distance_transform_edt(~ndi.binary_dilation(cov, iterations=1), return_distances=False, return_indices=True)
res = res[idx[0], idx[1]]
Image.fromarray(res).save(B2 + 'Head_BaseColor_cloud.png'); Image.fromarray(Rgb8(body_dark)).save(B2 + 'Body_BaseColor_cloud.png')
raw8 = (np.clip(lin2srgb(proj_raw), 0, 1) * 255).astype(np.uint8); raw8[~(cov & vis)] = 0
dbg = np.concatenate([raw8, (np.clip(lin2srgb(proj), 0, 1) * 255).astype(np.uint8) * (w[..., None] > 0.05).astype(np.uint8), res, np.repeat((w[..., None] * 255).astype(np.uint8), 3, 2)], 1)
Image.fromarray(dbg).resize((2048, 512)).save(B2 + '_proj_debug.png')
# photo-space debug: delit photo + masks
Image.fromarray((np.clip(lin2srgb(delit), 0, 1) * 255).astype(np.uint8)).save(B2 + '_delit_photo.png')
json.dump(dict(mean_skin_srgb=(mean_rgb * 255).tolist(), skin_Lab=Lc.tolist(), tobin_Lab=Lt.tolist(), neck_shift=shift.tolist(), projected_texels_w_gt_0p5=int((w > 0.5).sum()), cov_texels=int(cov.sum()),
               tps_pairs_kept=int(keep.sum()), tps_pairs=len(keep), chin_z=float(chin)), open(B2 + 'texture.json', 'w'), indent=1)
print('TEXTURE DONE')
