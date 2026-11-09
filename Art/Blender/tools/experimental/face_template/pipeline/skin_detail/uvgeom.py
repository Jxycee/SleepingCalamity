"""UV-space geometry + skin mask for a pipeline run (read-only on the run dir). venv python (numpy scipy cv2 skimage PIL).
Builds, at 2048^2 (cached to <run>/out/skin_detail/_geom.npz): per-texel 3D position (m), coverage, mm-per-texel along u and v, skin weight mask (0..1), patch centres of the pipeline's skin dE report.
Does NOT import c6_02_texture (it runs a whole stage at import); the mask block below is copied from it (touch() sets) on top of c6_common."""
import os, sys, json
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
PIPE = os.path.dirname(HERE)

def norm(p): return p.replace(chr(92), '/').rstrip('/')

def setup_env(run_dir):
    run_dir = norm(run_dir)
    sys.path.insert(0, PIPE)
    from paths import Paths
    cfg = json.load(open(run_dir + '/config.resolved.json'))
    cfg['out_dir'] = run_dir
    P = Paths.__new__(Paths); P.cfg = cfg; P.run = cfg['out_dir']
    t = cfg['template']
    P.target, P.fit, P.bake, P.out = (P.run + '/' + d for d in ('target', 'fit', 'bake', 'out'))
    P.tpl_root = t['root'].rstrip('/'); P.tpl_npz = t['npz']; P.sliders_dir = t['sliders_dir'].rstrip('/'); P.data_dir = t['data_dir'].rstrip('/')
    from paths import TOOLS; P.tools = dict(TOOLS)
    os.environ.update({k: v for k, v in P.env().items() if k.startswith('SCP_')})
    sys.path.insert(0, PIPE + '/stages')
    return cfg

def build(run_dir, R=2048, force=False):
    run_dir = norm(run_dir)
    import numpy as np, cv2
    from scipy import ndimage as ndi
    from skimage.color import rgb2lab
    from PIL import Image
    od = run_dir + '/out/skin_detail/'; os.makedirs(od, exist_ok=True)
    cache = od + '_geom.npz'
    if os.path.exists(cache) and not force:
        return dict(np.load(cache, allow_pickle=True))
    setup_env(run_dir)
    from c6_common import Tpl, raster_uv, interp, smoothstep, B2
    from scipy.spatial import cKDTree
    t = Tpl(); z = t.z; S = t.S; faces = t.faces
    uvl = z['head/loop_uv'].reshape(-1, 3, 2)
    basis = np.load(B2 + 'head_variants.npz')['full_basis'].astype(np.float64)
    fid, bary = raster_uv(faces, uvl, R); cov = fid >= 0
    pos = interp(fid, bary, faces, basis).astype(np.float32)
    def vmask(idx):
        v = np.zeros(len(basis)); v[np.asarray(idx).astype(int)] = 1; return interp(fid, bary, faces, v)
    touch = lambda idx: vmask(idx) > 0.01
    eye_uv = touch(S('eye_socket')) | touch(S('eyelid_ring_L')) | touch(S('eyelid_ring_R'))
    mouth = touch(S('mouth_interior')); ear = touch(S('ear_L')) | touch(S('ear_R')); nostril = touch(S('nostril'))
    lipv = np.concatenate([S('lip_patch_upper_verts'), S('lip_patch_lower_verts')])
    lip_any = touch(np.concatenate([lipv, S('lip_seam_all'), S('lip_line')])) | mouth
    # mm per texel along u (+x) and v (up = -row), from neighbouring texel positions inside the same island
    def dpos(shift_axis):
        a = np.roll(pos, -1, shift_axis); ok = np.roll(cov, -1, shift_axis) & cov
        d = np.linalg.norm(a - pos, axis=2) * 1000.0; return d, ok
    du, oku = dpos(1); dv, okv = dpos(0)
    # fill invalid with local median (dilate) so the normal-map scale is defined everywhere in coverage
    def fill(d, ok):
        d = np.where(ok & (d < 5.0), d, np.nan); m = np.nanmedian(d)
        dd = ndi.generic_filter(np.nan_to_num(d, nan=-1), lambda w: np.median(w[w > 0]) if (w > 0).any() else m, size=5) if False else d
        return np.where(np.isnan(dd), m, dd)
    du = fill(du, oku); dv = fill(dv, okv)
    # brow: dark texels of the albedo relative to the local median skin L, upper face only (the albedo carries the photo brows)
    alb = np.array(Image.open(run_dir + '/bake/Head_BaseColor_char.png').convert('RGB')).astype(np.float32) / 255
    L = rgb2lab(alb)[..., 0]
    Lm = cv2.medianBlur(np.clip(L * 2.55, 0, 255).astype(np.uint8), 31).astype(np.float32) / 2.55
    dark = (Lm - L) > 5.0
    zmax_eye = pos[..., 2][eye_uv].max() if eye_uv.any() else 0; zmin_eye = pos[..., 2][eye_uv].min()
    band = cov & (pos[..., 2] > zmin_eye - 0.01) & (pos[..., 2] < zmax_eye + 0.045) & (np.abs(pos[..., 0]) < 0.075) & (pos[..., 1] < 0.0)
    brow = ndi.binary_dilation(dark & band & ~eye_uv, iterations=2)
    brow = ndi.binary_opening(brow, iterations=1) | (brow & ndi.binary_dilation(brow, iterations=3))
    # skin weight: feathered distance from every exclusion; fade at coverage edge (UV island borders)
    def feather(excl, d0, d1):
        d = ndi.distance_transform_edt(~excl); return smoothstep(d0, d1, d)
    w = cov.astype(np.float32)
    w *= feather(eye_uv, 3, 12) * feather(lip_any, 4, 14) * feather(nostril, 4, 12) * feather(brow, 4, 14) * feather(ear, 3, 10)
    w *= smoothstep(1.5, 8, ndi.distance_transform_edt(cov))
    # the pipeline's dE patch centres (copied from c6_02: tpl_hits lookup, forehead +0.040 m)
    hp = json.load(open(os.environ['SCP_RUN'] + '/fit/tpl_hits.json')); al_nn = {}
    tree = cKDTree(basis); tpl_b = t.basis; ttree = cKDTree(tpl_b); patches = {}
    def uv_of(xyz):
        v = tree.query(xyz)[1]; f = np.where((faces == v).any(1))[0][0]; k = int(np.where(faces[f] == v)[0][0]); u = uvl[f, k]
        return [int(u[0] * R), int((1 - u[1]) * R)]
    for name, k in (('cheek_R(img-left)', 50), ('cheek_L(img-right)', 280), ('forehead', 9)):
        if hp.get(str(k)) is None: continue
        nn = ttree.query(np.array(hp[str(k)]))[1]
        xyz = basis[nn] + (np.array([0, 0, 0.040]) if name == 'forehead' else 0)
        patches[name] = uv_of(xyz)
    tone = json.load(open(run_dir + '/bake/texture.json'))['photo_fit_mean_Lab']
    out = dict(pos=pos, cov=cov, du=du.astype(np.float32), dv=dv.astype(np.float32), w=w.astype(np.float32), brow=brow, eye=eye_uv, lip=lip_any, nostril=nostril, ear=ear,
               patches=json.dumps(patches), tone=np.array(tone))
    np.savez_compressed(cache, **out); return out

if __name__ == '__main__':
    import numpy as np
    g = build(sys.argv[1], force=True)
    m = g['w'] > 0.5
    print('skin texels', int(m.sum()), 'mm/texel u median %.4f v median %.4f' % (np.median(g['du'][m]), np.median(g['dv'][m])), 'p10/p90 u', np.percentile(g['du'][m], [10, 90]).round(4), 'patches', g['patches'])
