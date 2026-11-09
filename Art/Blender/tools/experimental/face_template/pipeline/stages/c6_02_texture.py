"""Round 3: project the photo onto Tobin's head UV (numpy, CPU, deterministic). venv python (numpy scipy cv2 skimage PIL).
 wrapped head (full basis) -> per-texel 3D position/normal (UV raster) -> front-ortho render px (analytic, depth test) -> photo px (landmark map, c6_01_mapfit)
 Round-3 changes vs round 2:
  (a) the photo is projected only onto skin UV texels: never into the lip patch, mouth interior, eyelid/socket texels; lips = Tobin lip texture tinted to the photo lip colour
  (b) validity is a binary mask; the blend weight is a wide smoothstep of the UV-space distance from invalid texels (no sigma-3 blur of a hard depth-test edge);
      two-band blend: low band (wide, mask-normalised, no bleed across invalid texels) + high band (narrow)
  (c) de-light in Lab: robust (trimmed) degree-3 2D polynomial of L, a, b over the front-facing skin of the photo = shading + colour cast (side light, teal rim); L is divided by
      it, a/b have it subtracted, the photo mean skin tone is re-applied; teal-hue shadow pixels get the fitted chroma
  (d) brow luminance unsharp-masked in the photo before projection
 writes bake6/Head_BaseColor_char.png, bake6/Body_BaseColor_char.png, bake6/_proj_debug.png, bake6/texture.json"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
from c6_common import *
from c6_01_mapfit import fit, make_map
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
eye_uv = touch(S('eye_socket')) | touch(S('eyelid_ring_L')) | touch(S('eyelid_ring_R'))
mouth = touch(S('mouth_interior'))
ear = touch(S('ear_L')) | touch(S('ear_R')); nostril = touch(S('nostril'))
lipv = np.concatenate([S('lip_patch_upper_verts'), S('lip_patch_lower_verts')])
lip = touch(lipv) & ~mouth
lip_any = touch(np.concatenate([lipv, S('lip_seam_all'), S('lip_line')])) | mouth     # everything lip-like or inside the mouth
smoothstep_ = lambda a, b, x: smoothstep(a, b, x)

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
teal = (hsv[..., 0] >= 80) & (hsv[..., 0] <= 140) & (hsv[..., 1] > 70)
skin_hue = ~teal & (hsv[..., 2] > 18)
head_m = np.zeros((H, W), np.uint8); q = pp[sel & inb]; head_m[np.clip(q[:, 1].astype(int), 0, H - 1), np.clip(q[:, 0].astype(int), 0, W - 1)] = 1
head_m = cv2.morphologyEx(head_m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21))); head_m = ndi.binary_fill_holes(head_m).astype(bool)
RING = dict(eyeR=[33,7,163,144,145,153,154,155,133,173,157,158,159,160,161,246], eyeL=[362,382,381,380,374,373,390,249,263,466,388,387,386,385,384,398],
            browR=[70,63,105,66,107,55,65,52,53,46], browL=[300,293,334,296,336,285,295,282,283,276],
            lips=[61,146,91,181,84,17,314,405,321,375,291,409,270,269,267,0,37,39,40,185])
def poly(ids_, dil):
    m = np.zeros((H, W), np.uint8); cv2.fillPoly(m, [np.round(lm[ids_]).astype(np.int32)], 1)
    return cv2.dilate(m, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * dil + 1, 2 * dil + 1))).astype(bool) if dil > 0 else m.astype(bool)
eye_ph = poly(RING['eyeR'], 7) | poly(RING['eyeL'], 7)
brow_ph = poly(RING['browR'], 6) | poly(RING['browL'], 6)
lip_ph = poly(RING['lips'], 4)
nose_ph = np.zeros((H, W), np.uint8)
for k in (1, 2, 4, 5, 98, 327):
    cv2.circle(nose_ph, tuple(np.round(lm[k]).astype(int)), 9, 1, -1)
nose_ph = nose_ph.astype(bool)
feat = eye_ph | brow_ph | lip_ph | nose_ph
# fit mask: front-facing skin of the photo above the chin
yy_, xx_ = np.mgrid[:H, :W]
above_chin = yy_ < lm[152, 1] - 10
fitm = head_m & above_chin & skin_hue & ~ndi.binary_dilation(feat, iterations=4) & ndi.binary_erosion(head_m, iterations=8)

# ---------------- de-light in Lab (robust polynomial of L, a, b)
lab_ph = rgb2lab(photo).astype(np.float64)
# face-region ellipse to keep ears / hairline-free: use the fitm mask itself
xs_n = (xx_ - W / 2) / (W / 2); ys_n = (yy_ - H / 2) / (H / 2)
def basis_poly(deg=3):
    return np.stack([xs_n ** i * ys_n ** j for i in range(deg + 1) for j in range(deg + 1 - i)], -1)
Bp = basis_poly(3)
def robust_fit(ch, m, it=5):
    mm = m.copy()
    for _ in range(it):
        A = Bp[mm]; b = ch[mm]; c, *_ = np.linalg.lstsq(A, b, rcond=None); r = ch - Bp @ c
        s = 1.4826 * np.median(np.abs(r[mm] - np.median(r[mm]))) + 1e-6
        mm = m & (np.abs(r) < 2.0 * s)
    return Bp @ c, mm
fit_L, m_L = robust_fit(lab_ph[..., 0], fitm); fit_a, _ = robust_fit(lab_ph[..., 1], fitm); fit_b, _ = robust_fit(lab_ph[..., 2], fitm)
tone = np.array([np.mean(fit_L[m_L]), np.mean(fit_a[m_L]), np.mean(fit_b[m_L])])      # the photo mean skin tone (Lab) over the fitted region
print('photo fit mean Lab', tone.round(2), 'fit L range in mask %.1f..%.1f' % (np.percentile(fit_L[m_L], 2), np.percentile(fit_L[m_L], 98)))
delit_lab = np.empty_like(lab_ph)
delit_lab[..., 0] = lab_ph[..., 0] * (tone[0] / np.maximum(fit_L, 20.0))
delit_lab[..., 1] = lab_ph[..., 1] - fit_a + tone[1]
delit_lab[..., 2] = lab_ph[..., 2] - fit_b + tone[2]
# teal shadow-side pixels: keep the delit luminance but take the fitted chroma
teal_s = cv2.GaussianBlur(ndi.binary_dilation(teal & head_m, iterations=2).astype(np.float32), (0, 0), 3)
delit_lab[..., 1] = delit_lab[..., 1] * (1 - teal_s) + (tone[1] + (lab_ph[..., 1] - fit_a).clip(-4, 4) * 0.3) * teal_s
delit_lab[..., 2] = delit_lab[..., 2] * (1 - teal_s) + (tone[2] + (lab_ph[..., 2] - fit_b).clip(-4, 4) * 0.3) * teal_s
# side-light residual: symmetrise the low band of L about the face midline (mirror in photo space) so the one-sided light is not baked into the albedo
mid_ids = [10, 168, 6, 1, 2, 0, 17, 152]; cm = np.polyfit(lm[mid_ids, 1], lm[mid_ids, 0], 1)
xm = np.polyval(cm, yy_)
mxs = (2 * xm - xx_).astype(np.float32)
def mirror(a): return cv2.remap(a.astype(np.float32), mxs, yy_.astype(np.float32), cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)
symm = fitm & ndi.binary_erosion(fitm, iterations=2)
symf = symm.astype(np.float32); symf_m = mirror(symf)
SIGS = float(__import__('os').environ.get('SIGS', '18.0'))   # round 6: 26 -> 18 (finer low band so the one-sided forehead light is removed too)
Ld = delit_lab[..., 0].astype(np.float32)
low_n = cv2.GaussianBlur(Ld * symf, (0, 0), SIGS); den_n = cv2.GaussianBlur(symf, (0, 0), SIGS)
low = low_n / np.maximum(den_n, 1e-4); lowm = mirror(low); denm = mirror(den_n)
okb = (den_n > 0.05) & (denm > 0.05)
low_sym = np.where(okb, 0.5 * (low + lowm), low)
SYM = float(__import__('os').environ.get('SYM', '1.0'))      # round 6: 0.85 -> 1.0 (full midline symmetrisation of the low luminance band)
delit_lab[..., 0] = np.where(okb, Ld - SYM * (low - low_sym), Ld)
# keep features dark relative to the skin: the divide above already keeps their relative darkness; clamp extremes
delit_lab[..., 0] = np.clip(delit_lab[..., 0], 8, 92)
_hi = tone[0] + 7.0; _L = delit_lab[..., 0]; delit_lab[..., 0] = np.where(_L > _hi, _hi + 0.3 * (_L - _hi), _L)    # soft-compress specular highlights (chin, nose tip) out of the albedo
# (d) brow unsharp on luminance in the brow mask (feathered)
Lsrc = delit_lab[..., 0]; bm = cv2.GaussianBlur(ndi.binary_dilation(brow_ph, iterations=2).astype(np.float32), (0, 0), 2.0)
BROW_SHARP = float(__import__('os').environ.get('BROW_SHARP', '0.5'))   # round 5: 1.4 -> 0.5 (brows read thicker and darker than the photo's thin straight brows)
Lsharp = Lsrc + BROW_SHARP * (Lsrc - cv2.GaussianBlur(Lsrc.astype(np.float32), (0, 0), 2.2))
delit_lab[..., 0] = Lsrc * (1 - bm) + np.clip(Lsharp, 6, 92) * bm
Lc = tone.copy()
# photo lip colour (mean Lab inside the lip polygon, eroded; undelit: the lips keep their own colour)
lipm = ndi.binary_erosion(poly(RING['lips'], 0), iterations=3)
_pl = lab_ph[lipm]; _lo, _hi = np.percentile(_pl[:, 0], [45, 97]); lip_lab_ph = _pl[(_pl[:, 0] >= _lo) & (_pl[:, 0] <= _hi)].mean(0)   # vermilion only: drop the dark mouth line / shadow
print('photo lip Lab', lip_lab_ph.round(1))

# ---------------- sample photo at texels (Lab)
mx, my = pp[..., 0].astype(np.float32), pp[..., 1].astype(np.float32)
samp = lambda im, interp_=cv2.INTER_LINEAR: cv2.remap(im.astype(np.float32), mx, my, interp_, borderMode=cv2.BORDER_REFLECT)
proj_lab = samp(delit_lab)
skin_ok_ph = cv2.GaussianBlur((head_m & (skin_hue | teal)).astype(np.float32), (0, 0), 1.0)
s_eye = samp(cv2.GaussianBlur(eye_ph.astype(np.float32), (0, 0), 2)); s_head = samp(ndi.binary_erosion(head_m, iterations=3).astype(np.float32))
chin = basis[t.lm_idx[t.lm_names.index('chin_tip')], 2]
chin_f = zmap > (chin - 0.004)

# ---------------- validity (binary) -> wide smoothstep of the UV-space distance from invalid texels
valid = cov & vis & inb & (wface > 0.50) & (nor[..., 2] > -0.30) & (pp[..., 1] < lm[152, 1] + 3) & (s_head > 0.5) & chin_f & (s_eye < 0.2)
valid &= ~ndi.binary_dilation(eye_uv, iterations=6) & ~ndi.binary_dilation(lip_any, iterations=3) & ~ndi.binary_dilation(nostril, iterations=2)
# round 5 (general rule, replaces 'ear texels valid where front-facing'): the photo is only trusted where the de-light fit has support (the fitted skin mask grown by 30 px, which
# also bridges the feature holes); ears and anything outside that support are always filled (skin tone + Tobin detail). The ear texels used to be projected onto the shadowed photo ear / background.
support = ndi.binary_dilation(fitm, iterations=30) & head_m
s_sup = samp(support.astype(np.float32)); valid &= (s_sup > 0.5) & ~ndi.binary_dilation(ear, iterations=3)
valid = ndi.binary_opening(valid, iterations=2)
inval = ~valid & cov                                   # texels outside the coverage are neither valid nor invalid (UV island borders are not penalised)
dist = ndi.distance_transform_edt(~inval)             # distance (texels) from the nearest invalid covered texel
w_low = smoothstep(6, 80, dist) * valid      # round 4: wider low-frequency blend (round 3: 2..34)               # wide transition for the colour
w_hi = smoothstep(1, 12, dist) * valid * wface        # narrower for the detail
print('valid texels', int(valid.sum()), 'of', int(cov.sum()))

# ---------------- Tobin texture, tinted to the photo skin tone
tob = np.array(Image.open(TEX + 'Head_BaseColor.png').convert('RGB')).astype(np.float32) / 255; assert tob.shape[:2] == (R, R)
body_tex = np.array(Image.open(TEX + 'Body_BaseColor.png').convert('RGB')).astype(np.float32) / 255
tl = Lab(tob); face_area = cov & (zmap > 1.58) & ~eye_uv & ~mouth & ~lip & ~ear & ~nostril; Lt = np.median(tl[face_area], 0)
def darken(l):
    o = l.copy(); o[..., 0] = l[..., 0] * (Lc[0] / Lt[0]); o[..., 1:] = l[..., 1:] + (Lc[1:] - Lt[1:]); return o
tob_dark = darken(tl); body_dark = darken(Lab(body_tex)); print('tobin Lab', Lt.round(1), 'photo skin Lab', Lc.round(1))

# ---------------- masked (mask-normalised) blurs: no bleed across invalid texels
def mblur(x, ww, s):
    den = cv2.GaussianBlur(ww.astype(np.float32), (0, 0), s)
    return np.stack([cv2.GaussianBlur(x[..., k].astype(np.float32) * ww, (0, 0), s) for k in range(3)], -1) / np.maximum(den, 1e-6)[..., None], den
vf = valid.astype(np.float32)
low6, d6 = mblur(proj_lab, vf, 8)              # projected colour within valid texels only
low60, d60 = mblur(proj_lab, ndi.binary_erosion(valid, iterations=2).astype(np.float32), 60); low250, _ = mblur(proj_lab, ndi.binary_erosion(valid, iterations=2).astype(np.float32), 250)
fill_low = np.where((d60 > 0.02)[..., None], low60, low250)
detail = tob_dark - np.stack([cv2.GaussianBlur(tob_dark[..., k], (0, 0), 25) for k in range(3)], -1)
detail[..., 0] = np.clip(detail[..., 0], -10, 6); detail[..., 1:] = np.clip(detail[..., 1:], -6, 6)
patch = fill_low + detail
far_w = np.clip((d60 - 0.02) / 0.1, 0, 1)[..., None]
patch = patch * far_w + (np.broadcast_to(Lc, patch.shape) + detail) * (1 - far_w)
# bands: low band = wide blend of the (6 px blurred) projection vs the patch; high band = projection minus its 6 px blur, narrow weight
lowband = low6 * w_low[..., None] + patch * (1 - w_low[..., None])
highband = (proj_lab - low6) * w_hi[..., None]
highband[..., 1:] *= 0.6
comb = lowband + highband
# ---------------- round 6 brows: the photo's brows are thin, pale and low-contrast. In the (projected) brow region: thin the dark mask (erode BROW_ERODE of the half thickness)
# and pull the contrast toward the local skin tone (BROW_K), chroma too. Geometry untouched.
import os as _os
BROW_K = float(_os.environ.get('BROW_K', '0.55')); BROW_ERODE = float(_os.environ.get('BROW_ERODE', '0.30'))
s_brow = samp(cv2.GaussianBlur(brow_ph.astype(np.float32), (0, 0), 1.5)); brow_uv = (s_brow > 0.3) & valid
ref_lab, _d = mblur(comb, (valid & ~ndi.binary_dilation(brow_uv, iterations=5)).astype(np.float32), 14)    # skin reference from the composed texture around the brows (no visible patch edge)
dark = ref_lab[..., 0] - comb[..., 0]; dm_ = brow_uv & (dark > 4.0)
if dm_.sum() > 50:
    dt = ndi.distance_transform_edt(dm_); hth = np.percentile(dt[dm_], 90); rr_ = max(1, int(round(BROW_ERODE * hth)))
    soft = cv2.GaussianBlur(ndi.binary_erosion(dm_, iterations=rr_).astype(np.float32), (0, 0), 1.3)
    bw = np.clip(cv2.GaussianBlur(brow_uv.astype(np.float32), (0, 0), 4.0) * 1.3, 0, 1)
    thin = ref_lab[..., 0] - dark * soft * BROW_K
    comb[..., 0] = comb[..., 0] * (1 - bw) + np.where(brow_uv, thin, comb[..., 0]) * bw
    for k_ in (1, 2):
        dk_ = comb[..., k_] - ref_lab[..., k_]; comb[..., k_] = comb[..., k_] * (1 - bw) + (ref_lab[..., k_] + dk_ * (0.5 + 0.5 * BROW_K) * np.maximum(soft, 0.3)) * bw
    print('brows: half-thickness p90 %.1f texels, eroded %d, dark px %d' % (hth, rr_, int(dm_.sum())))
# ---------------- round 6: flatten the remaining low-frequency luminance (side light) toward the photo skin tone over the valid skin texels (albedo has no large-scale shading)
FLAT = float(_os.environ.get('FLAT', '0.85'))
_okf = (valid & ~ndi.binary_dilation(brow_uv, iterations=4) & ~ndi.binary_dilation(lip_any, iterations=6) & ~ndi.binary_dilation(eye_uv, iterations=6)).astype(np.float32)
_lp, _dn = mblur(comb, _okf, 40.0)
_wf = np.clip(_dn / 0.15, 0, 1) * w_low
comb[..., 0] = comb[..., 0] - FLAT * (_lp[..., 0] - Lc[0]) * _wf
# ---------------- lips: tinted Tobin lip texture matched to the photo lip colour (never the photo projection)
tob_lip_mean = tl[lip].mean(0) if lip.any() else Lt
shift = lip_lab_ph - tob_lip_mean
lip_tex = tl + shift[None, None]
lip_w = np.clip(cv2.GaussianBlur(ndi.binary_dilation(lip, iterations=1).astype(np.float32), (0, 0), 2.0), 0, 1)
comb = comb * (1 - lip_w[..., None]) + lip_tex * lip_w[..., None]
mouth_w = np.clip(cv2.GaussianBlur(mouth.astype(np.float32), (0, 0), 3.0), 0, 1)[..., None]; comb = comb * (1 - mouth_w) + tob_dark * mouth_w
print('lip Lab tobin->photo', tob_lip_mean.round(1), lip_lab_ph.round(1))

# ---------------- neck match
bz = z['body/basis'][:, 2]; bf = z['body/faces']; bnk = np.where(bz > 1.485)[0]
bu = z['body/loop_uv'].reshape(-1, 3, 2)[np.isin(bf, bnk).any(1)].reshape(-1, 2)
bpx = np.clip((bu * [R, R]).astype(int), 0, R - 1); bpx[:, 1] = R - 1 - bpx[:, 1]
body_neck = np.median(body_dark[bpx[:, 1], bpx[:, 0]], 0); zr = zmap[cov].min()
head_neck = np.median(comb[cov & (zmap < zr + 0.012)], 0); shift_n = body_neck - head_neck
wn = np.clip(1 - (zmap - zr) / 0.05, 0, 1) ** 1.5 * cov
comb = comb + shift_n[None, None] * wn[..., None]; print('neck shift Lab', shift_n.round(2))
res = Rgb8(comb)
idx = ndi.distance_transform_edt(~ndi.binary_dilation(cov, iterations=1), return_distances=False, return_indices=True)
res = res[idx[0], idx[1]]
Image.fromarray(res).save(B2 + 'Head_BaseColor_char.png'); Image.fromarray(Rgb8(body_dark)).save(B2 + 'Body_BaseColor_char.png')
raw8 = Rgb8(proj_lab) * (valid[..., None]).astype(np.uint8)
dbg = np.concatenate([raw8, res, np.repeat((w_low[..., None] * 255).astype(np.uint8), 3, 2), np.repeat((w_hi[..., None] * 255).astype(np.uint8), 3, 2)], 1)
Image.fromarray(dbg).resize((2048, 512)).save(B2 + '_proj_debug.png')
Image.fromarray(Rgb8(delit_lab)).save(B2 + '_delit_photo.png')

# ---------------- skin Lab report: photo vs texture at cheeks and forehead (same 3D points, 21x21 px patches)
hp = json.load(open(FIT + 'tpl_hits.json'))
def tex_patch_lab(vert_xyz, r=14):
    v = cKDTree(basis).query(vert_xyz)[1]
    # a face of that vertex -> its loop UV
    f = np.where((faces == v).any(1))[0][0]; k = int(np.where(faces[f] == v)[0][0]); u = uvl[f, k]
    px, py = int(u[0] * R), int((1 - u[1]) * R)
    return Lab(res[py - r:py + r + 1, px - r:px + r + 1].astype(np.float32) / 255).reshape(-1, 3).mean(0), (px, py)
def photo_patch_lab(p, r=12):
    x, y = int(p[0]), int(p[1]); return lab_ph[y - r:y + r + 1, x - r:x + r + 1].reshape(-1, 3).mean(0)
al = json.load(open(FIT + 'align.json'))
rep = {}
tpl_b = t.basis
for name, k, off in (('cheek_R(img-left)', 50, (0, 0)), ('cheek_L(img-right)', 280, (0, 0)), ('forehead', 9, (0, 52))):
    if hp.get(str(k)) is None: continue
    vx = np.array(hp[str(k)]); nn = cKDTree(tpl_b).query(vx)[1]
    if name == 'forehead':   # forehead: raise 25 mm above landmark 9 (mediapipe 9 is between the brows)
        vx2 = basis[nn] + np.array([0, 0, 0.040]); tl_, uv = tex_patch_lab(vx2)
        pl = photo_patch_lab(lm[9] + np.array([0, -45]))
    else:
        tl_, uv = tex_patch_lab(basis[nn]); pl = photo_patch_lab(lm[k])
    rep[name] = dict(photo_Lab=pl.round(1).tolist(), texture_Lab=tl_.round(1).tolist())
print('SKIN LAB photo vs texture', json.dumps(rep))
json.dump(dict(photo_fit_mean_Lab=tone.tolist(), photo_lip_Lab=lip_lab_ph.tolist(), tobin_Lab=Lt.tolist(), neck_shift=shift_n.tolist(), valid_texels=int(valid.sum()), cov_texels=int(cov.sum()),
               tps_pairs_kept=int(keep.sum()), tps_pairs=len(keep), chin_z=float(chin), skin_Lab_report=rep), open(B2 + 'texture.json', 'w'), indent=1)
print('TEXTURE DONE')
