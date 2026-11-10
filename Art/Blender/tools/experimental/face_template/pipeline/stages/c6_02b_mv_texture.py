"""Multi-view mode: colour the side, back, ear and scalp texels from the other generated views (the single front photo only sees the face; c6_02 smears or extrapolates everything else).
Runs after c6_02_texture (bake/Head_BaseColor_char.png = front-photo projection, kept as bake/Head_BaseColor_front.png).
usage: c6_02b_mv_texture.py <headframe.npz> <cutout_dir> <tag>     (cutouts = <dir>/mv1_<tag>_<view>.png RGBA; views q34l, q34r, left, right, back are used; the front view stays the photo projection)
Method: texel 3D position + normal on the final head (as c6_02) -> head frame (similarity from fit/align.json + ICP, multiview/mv_eval.tpl_to_hf) -> each view's pixel by the orthographic mapping that registers
the TRELLIS mesh bounding box to the cutout alpha bounding box (uniform scale, same camera as the silhouette IoU table) -> visibility by a point z-buffer over all texels + facing weight smoothstep(0.15, 0.75, n.view).
Colour consistency: views are composited in the order 3/4 -> side -> back; each is shifted in Lab by the mean difference to the composite on their overlap (no shift under 1500 overlap texels).
The front projection keeps the face: its weight is 40 * smoothstep(0.35, 0.75, frontness) + 0.05. Eye, lid, mouth, lip and nostril texels are never touched and the views fade in over 70 texels around them. Limits: the views carry their own studio light (no de-lighting
beyond the Lab shift), ears and other concavities get the nearest visible view, registration is bounding-box based (about 1-2 mm). Writes bake/Head_BaseColor_char.png, bake/mv_tex_debug.png, bake/mv_tex.json."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
from c6_common import *
import sys, json, cv2, shutil
from PIL import Image
from skimage.color import rgb2lab, lab2rgb
from scipy import ndimage as ndi
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'multiview')); sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import mv_eval as ME
hf_npz, cut_dir, tag = sys.argv[1:4]; R = 2048
Lab = lambda a: rgb2lab(np.clip(a, 0, 1).astype(np.float32)); Rgb8 = lambda l: (np.clip(lab2rgb(l), 0, 1) * 255 + 0.5).astype(np.uint8)
t = Tpl(); z = t.z; S = t.S; faces = t.faces; uvl = z['head/loop_uv'].reshape(-1, 3, 2); basis = np.load(B2 + 'head_variants.npz')['full_basis'].astype(np.float64)
fid, bary = raster_uv(faces, uvl, R); cov = fid >= 0
pos = interp(fid, bary, faces, basis).astype(np.float64); nor = interp(fid, bary, faces, vertex_normals(basis, faces)).astype(np.float64); nor /= np.linalg.norm(nor, axis=2, keepdims=True) + 1e-9
def vmask(idx):
    v = np.zeros(len(basis)); v[np.asarray(idx).astype(int)] = 1; return interp(fid, bary, faces, v)
touch = lambda idx: vmask(idx) > 0.01
lipv = np.concatenate([S('lip_patch_upper_verts'), S('lip_patch_lower_verts')])
excl = touch(S('eye_socket')) | touch(S('eyelid_ring_L')) | touch(S('eyelid_ring_R')) | touch(S('mouth_interior')) | touch(S('nostril')) | touch(np.concatenate([lipv, S('lip_seam_all'), S('lip_line')]))
excl = ndi.binary_dilation(excl, iterations=3); feat_ramp = np.clip(ndi.distance_transform_edt(~excl) / 70.0, 0, 1); feat_ramp = feat_rampp = feat_ramp * feat_ramp * (3 - 2 * feat_ramp)   # views fade in over 70 texels around eyes / nose holes / mouth (their registration error is ~1-2 mm: it would paint nostril and lip colour on the skin next to them)
hfz = np.load(hf_npz); hv = hfz['v'].astype(np.float64); Sx, Rm, Tt, res_icp = ME.tpl_to_hf(D2.rstrip('/'), hv); print('template->head frame scale %.4f, icp residual %.4f' % (Sx, res_icp))
ph = Sx * pos @ Rm.T + Tt; nh = nor @ Rm.T
shutil.copy(B2 + 'Head_BaseColor_char.png', B2 + 'Head_BaseColor_front.png')
base = np.array(Image.open(B2 + 'Head_BaseColor_front.png').convert('RGB')).astype(np.float32) / 255; baseL = Lab(base)
front = np.clip(-nor[..., 1], 0, 1); wB = (40.0 * smoothstep(0.35, 0.75, front) + 0.05) * cov
C = baseL.copy(); Wc = wB.copy(); order = ['q34l', 'q34r', 'left', 'right', 'back']; info = {}
for v in order:
    yaw = ME.YAW[v]; th = np.radians(yaw); c = np.array([np.sin(th), -np.cos(th), 0.0]); right = np.cross(-c, [0, 0, 1.0]); right /= np.linalg.norm(right); cam = ME.view_cam(hv, yaw)
    im = cv2.imread('%s/mv1_%s_%s.png' % (cut_dir, tag, v), cv2.IMREAD_UNCHANGED); alpha = im[..., 3] > 127; ys, xs = np.where(alpha); ay0, ay1, ax0, ax1 = ys.min(), ys.max(), xs.min(), xs.max()
    sY = (ay1 - ay0) / (cam['y1'] - cam['y0']); xc = ph @ right if False else ph[..., 0] * right[0] + ph[..., 1] * right[1] + ph[..., 2] * right[2]
    u = 0.5 * (ax0 + ax1) + (xc - 0.5 * (cam['x0'] + cam['x1'])) * sY; vv = ay0 + (cam['y1'] - ph[..., 2]) * sY
    depth = ph[..., 0] * c[0] + ph[..., 1] * c[1] + ph[..., 2] * c[2]
    cell = 3; gw = int(im.shape[1] / cell) + 2; gh = int(im.shape[0] / cell) + 2; ci = np.clip((vv / cell).astype(int), 0, gh - 1) * gw + np.clip((u / cell).astype(int), 0, gw - 1)
    zb = np.full(gw * gh, -1e9); np.maximum.at(zb, ci[cov], depth[cov]); vis = cov & (depth >= zb[ci] - 0.006)
    mapx = u.astype(np.float32); mapy = vv.astype(np.float32); rgb = cv2.remap(im[..., :3], mapx, mapy, cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)[..., ::-1].astype(np.float32) / 255
    inside = ndi.binary_erosion(alpha, iterations=4)[np.clip(vv.round().astype(int), 0, im.shape[0] - 1), np.clip(u.round().astype(int), 0, im.shape[1] - 1)]
    nd = nh[..., 0] * c[0] + nh[..., 1] * c[1] + nh[..., 2] * c[2]; wv = smoothstep(0.15, 0.75, nd) * vis * inside * cov * (~excl) * feat_ramp
    col = Lab(rgb); ov = (wv > 0.25) & (Wc > 0.25); n_ov = int(ov.sum())
    shift = np.median((C - col)[ov], axis=0) if n_ov >= 1500 else np.zeros(3); col = col + shift
    C = (C * Wc[..., None] + col * wv[..., None]) / np.maximum(Wc + wv, 1e-6)[..., None]; Wc = Wc + wv
    info[v] = dict(overlap_texels=n_ov, lab_shift=[round(float(x), 2) for x in shift], texels=int((wv > 0.25).sum()))
    print(v, info[v], flush=True)
out = np.where(excl[..., None] | ~cov[..., None], baseL, C)
rgb8 = Rgb8(out); idx = ndi.distance_transform_edt(~ndi.binary_dilation(cov, iterations=1), return_distances=False, return_indices=True); rgb8 = rgb8[idx[0], idx[1]]
Image.fromarray(rgb8).save(B2 + 'Head_BaseColor_char.png')
dbg = np.concatenate([Rgb8(baseL), rgb8, np.repeat((np.clip(Wc - wB, 0, 1)[..., None] * 255).astype(np.uint8), 3, 2)], 1); Image.fromarray(dbg).resize((dbg.shape[1] // 4, dbg.shape[0] // 4), Image.LANCZOS).save(B2 + 'mv_tex_debug.png')
json.dump(dict(views=info, front_texels=int((front > 0.45).sum()), cov=int(cov.sum()), icp=res_icp), open(B2 + 'mv_tex.json', 'w'), indent=1)
print('MVTEX DONE')
