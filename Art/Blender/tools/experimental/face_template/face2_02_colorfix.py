"""Colour-fix the Cycles bake (venv python: numpy scipy cv2 skimage PIL).
 raw bake -> remove black speckles -> keep only high-frequency detail, replace low-frequency colour by the concept skin tone
 -> blend with a skin-tone-matched copy of Tobin Head_BaseColor in eye/lid/mouth/lip/ear/no-hit/neck regions -> neck Lab match to Body.
usage: python face2_02_colorfix.py   (writes bake/Head_BaseColor_face2.png, bake/Body_BaseColor_face2.png, bake/_masks.png, bake/colorfix.json)"""
import numpy as np, cv2, json
from PIL import Image
from skimage.color import rgb2lab, lab2rgb
from scipy import ndimage as ndi
D = 'C:/AI/test_out/heroes/face_spike/'; B = D + 'bake/'
TEX = 'C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/Assets/_Project/Content/Characters/TobinBase/Textures/'
R = 2048
z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
S = lambda k: np.asarray(z['tpl/' + k])
faces = z['head/faces']; uv = z['head/loop_uv'].reshape(-1, 3, 2); basis = np.load(D + 'fit/wrap_r3_basis.npy')
pix = np.stack([uv[..., 0] * R - 0.5, (1 - uv[..., 1]) * R - 0.5], -1)  # (F,3,2) x,y

def raster_attr(vals):
    """barycentric raster of a per-vertex scalar into UV space; returns (img, covered)"""
    out = np.zeros((R, R), np.float32); cov = np.zeros((R, R), bool)
    for f in range(len(faces)):
        p = pix[f]; x0, y0 = np.floor(p.min(0)).astype(int) - 1; x1, y1 = np.ceil(p.max(0)).astype(int) + 1
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, R - 1), min(y1, R - 1)
        if x1 < x0 or y1 < y0: continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        d = (p[1, 1] - p[2, 1]) * (p[0, 0] - p[2, 0]) + (p[2, 0] - p[1, 0]) * (p[0, 1] - p[2, 1])
        if abs(d) < 1e-9: continue
        l0 = ((p[1, 1] - p[2, 1]) * (gx - p[2, 0]) + (p[2, 0] - p[1, 0]) * (gy - p[2, 1])) / d
        l1 = ((p[2, 1] - p[0, 1]) * (gx - p[2, 0]) + (p[0, 0] - p[2, 0]) * (gy - p[2, 1])) / d
        l2 = 1 - l0 - l1; m = (l0 >= -1e-4) & (l1 >= -1e-4) & (l2 >= -1e-4)
        v = vals[faces[f]]; out[gy[m], gx[m]] = (l0 * v[0] + l1 * v[1] + l2 * v[2])[m]; cov[gy[m], gx[m]] = True
    return out, cov
def vmask(idx):
    v = np.zeros(len(basis), np.float32); v[np.asarray(idx)] = 1
    a, _ = raster_attr(v); return a
def touch(idx):  # texels of triangles touching the vertex set (soft edge -> >0.01)
    return (vmask(idx) > 0.01)
zmap, cov = raster_attr(basis[:, 2].astype(np.float32))
cov_d = ndi.binary_dilation(cov, iterations=3)
def feather(m, grow, sig):
    m = ndi.binary_dilation(m, iterations=grow) if grow else m
    return np.clip(ndi.gaussian_filter(m.astype(np.float32), sig), 0, 1)
eye = touch(S('eye_socket')) | touch(S('eyelid_ring_L')) | touch(S('eyelid_ring_R'))
mouth = touch(S('mouth_interior'))
lip = touch(np.concatenate([S('lip_patch_upper_verts'), S('lip_patch_lower_verts')])) & ~mouth
ear = touch(S('ear_L')) | touch(S('ear_R'))
nostril = touch(S('nostril'))

raw = np.array(Image.open(B + 'raw_color.png').convert('RGB')); hit = np.array(Image.open(B + 'raw_hit.png'))[..., 0] > 128
tob = np.array(Image.open(TEX + 'Head_BaseColor.png').convert('RGB')); assert tob.shape[:2] == (R, R)
body_tex = np.array(Image.open(TEX + 'Body_BaseColor.png').convert('RGB'))
Lab = lambda a: rgb2lab(a.astype(np.float32) / 255)
Rgb = lambda l: (np.clip(lab2rgb(l), 0, 1) * 255 + 0.5).astype(np.uint8)

# ---- concept skin tone (c2_3.png, 1024 px): forehead + both cheeks
c = np.array(Image.open(D + 'target/c2_3.png').convert('RGB'))
rects = [(400, 190, 620, 290), (290, 470, 380, 560), (640, 470, 740, 560), (300, 560, 400, 660), (620, 560, 720, 660), (290, 330, 340, 420), (680, 330, 730, 420)]
cl = np.concatenate([Lab(c[y0:y1, x0:x1]).reshape(-1, 3) for x0, y0, x1, y1 in rects])
lo, hi = np.percentile(cl[:, 0], [8, 32]); Lc = np.median(cl[(cl[:, 0] >= lo) & (cl[:, 0] <= hi)], 0)  # skip specular highlights; print('concept skin Lab median', Lc, 'rgb', Rgb(Lc[None, None])[0, 0])

# ---- Tobin skin tone -> dark (same transform used for body so the neck matches)
tl = Lab(tob); face_area = cov & (zmap > 1.58) & ~eye & ~mouth & ~lip & ~ear & ~nostril
Lt = np.median(tl[face_area], 0); print('tobin skin Lab median', Lt)
def darken(l):
    o = l.copy(); o[..., 0] = l[..., 0] * (Lc[0] / Lt[0]); o[..., 1:] = l[..., 1:] + (Lc[1:] - Lt[1:]); return o
tob_dark = darken(tl); bl = Lab(body_tex); body_dark = darken(bl)

# ---- valid bake texels
cloth = (raw.astype(int).sum(2) < 120) & (raw[..., 2] >= raw[..., 0] - 2)   # navy shirt / black
z_hit_min = zmap[hit & cov].min(); zr = zmap[cov].min()
valid = hit & cov & ~cloth
print('z of coverage min', zr, 'bake hit min', z_hit_min)

# ---- speckles: small dark blobs vs local median
rawl = Lab(raw); Lg = np.clip(rawl[..., 0] * 2.55, 0, 255).astype(np.uint8)
med = cv2.medianBlur(Lg, 21)
dark = ((Lg.astype(int) < med.astype(int) - 22) & valid).astype(np.uint8)
n, lab_, st, _ = cv2.connectedComponentsWithStats(dark, connectivity=8)
sp = np.zeros_like(dark); cnt = 0
for i in range(1, n):
    w_, h_, ar = st[i, 2], st[i, 3], st[i, 4]
    if 2 <= ar <= 900 and max(w_, h_) <= 34:
        sp[lab_ == i] = 1; cnt += 1
sp = cv2.dilate(sp, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (7, 7)))
fixed = cv2.inpaint(raw, (sp * 255).astype(np.uint8), 5, cv2.INPAINT_TELEA); print('speckles', cnt)
fl = Lab(fixed)

# ---- split low / high frequency (masked blur) and re-tone
def mblur(x, m, s):
    mm = m.astype(np.float32); den = cv2.GaussianBlur(mm, (0, 0), s) + 1e-6
    return np.stack([cv2.GaussianBlur(x[..., k] * mm, (0, 0), s) for k in range(3)], -1) / den[..., None]
low = mblur(fl, valid, 45); hf = fl - low
mu = np.median(low[valid], 0)
out = np.zeros_like(fl)
dl = np.clip((low[..., 0] - mu[0]) * 0.12, -5, 5)           # keep a little large-scale shading
out[..., 0] = Lc[0] + dl + np.clip(hf[..., 0], -22, 8)
dab = np.clip((low[..., 1:] - mu[1:]) * 0.2, -4, 4)
out[..., 1:] = Lc[1:] + dab + hf[..., 1:] * 0.3
bake_rgb = Rgb(out)

# ---- blend with Tobin-dark.  Eye/nostril/ear/no-hit/neck patches = diffused bake tone + Tobin's local detail (no tone jump);
# mouth interior and lips use Tobin-dark directly.
bake_lab = Lab(bake_rgb)
core_bad = eye | mouth | nostril | lip | ear | ~valid
wv = ndi.binary_erosion(valid & ~ndi.binary_dilation(core_bad, iterations=6), iterations=2)
fill_low = mblur(bake_lab, wv, 60); den = cv2.GaussianBlur(wv.astype(np.float32), (0, 0), 60)
far = mblur(bake_lab, wv, 250); fill_low = np.where((den > 0.02)[..., None], fill_low, far)
tob_detail = tob_dark - np.stack([cv2.GaussianBlur(tob_dark[..., k], (0, 0), 25) for k in range(3)], -1)
patch = fill_low + tob_detail
w_patch = np.zeros((R, R), np.float32); w_direct = np.zeros((R, R), np.float32)
w_patch = np.maximum(w_patch, feather(eye, 10, 8)); w_patch = np.maximum(w_patch, feather(nostril, 4, 4))
w_patch = np.maximum(w_patch, 0.5 * feather(ear, 0, 6))
nohit = ~valid
w_patch = np.maximum(w_patch, feather(nohit & cov_d, 0, 6)); w_patch[nohit] = 1.0
zl = z_hit_min + 0.004   # bake fades out over 14 mm above the first hit height
w_patch = np.maximum(w_patch, np.clip(1 - (zmap - zl) / 0.014, 0, 1) * cov)
w_direct = np.maximum(feather(mouth, 6, 6), 0.7 * feather(lip, 2, 5))
w_direct = np.minimum(w_direct, 1 - 0 * w_patch)
w = np.clip(np.maximum(w_patch, w_direct), 0, 1)
inner = w_direct / np.maximum(w, 1e-6)           # share of the direct (Tobin-dark) term inside the blend
tgt = patch * (1 - inner[..., None]) + tob_dark * inner[..., None]
comb = bake_lab * (1 - w[..., None]) + tgt * w[..., None]

# ---- neck match: Lab shift of head near neck ring toward the (tinted) body neck colour
bz = z['body/basis'][:, 2]; bf = z['body/faces']; bnk = np.where(bz > 1.485)[0]
bu = z['body/loop_uv'].reshape(-1, 3, 2)[np.isin(bf, bnk).any(1)].reshape(-1, 2)
bpx = np.clip((bu * [R, R]).astype(int), 0, R - 1); bpx[:, 1] = R - 1 - bpx[:, 1]
body_neck = np.median(body_dark[bpx[:, 1], bpx[:, 0]], 0)
ring_band = cov & (zmap < zr + 0.012)
head_neck = np.median(comb[ring_band], 0); shift = body_neck - head_neck
wn = np.clip(1 - (zmap - zr) / 0.05, 0, 1) ** 1.5 * cov
comb = comb + shift[None, None] * wn[..., None]
print('neck body Lab', body_neck, 'head Lab', head_neck, 'shift', shift)

res = Rgb(comb)
# gutter: nearest covered texel everywhere outside the UV islands
idx = ndi.distance_transform_edt(~ndi.binary_dilation(cov, iterations=1), return_distances=False, return_indices=True)
res = res[idx[0], idx[1]]
Image.fromarray(res).save(B + 'Head_BaseColor_face2.png')
Image.fromarray(Rgb(body_dark)).save(B + 'Body_BaseColor_face2.png')
sheet = np.concatenate([raw, fixed, res, (np.stack([w] * 3, -1) * 255).astype(np.uint8)], 1)
Image.fromarray(sheet).resize((2048, 512)).save(B + '_masks.png')
json.dump(dict(concept_Lab=Lc.tolist(), tobin_Lab=Lt.tolist(), speckles=cnt, neck_shift=shift.tolist(), z_hit_min=float(z_hit_min), z_cov_min=float(zr)), open(B + 'colorfix.json', 'w'), indent=1)
