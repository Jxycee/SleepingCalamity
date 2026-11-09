"""Skin detail layer for a face-pipeline run. venv python (torch cu128, numpy, scipy, cv2, skimage, PIL):
    C:/AI/trellis2/venv/Scripts/python.exe apply_skin_detail.py <run_dir> [--strength 1.0] [--pore-mm 0.8] [--line-mm 1.6] [--normal 1] [--albedo 1] [--rough 1] [--mottle 0.25] [--seed 1]
Reads (read-only): <run>/bake/Head_BaseColor_char.png, the repo's Tobin Head_Normal.png, the template UV/mesh (via the pipeline's c6_common) and fit/tpl_hits.json.
Writes <run>/out/skin_detail/: albedo_detail.png, normal_detail.png (Tobin normal + detail, whiteout blend, OpenGL +Y), roughness_detail.png (0.5 base + modulation, the value the build uses),
  layer_normal.png / layer_albedo_delta.png (detail only, for a shader-side stack), skin_mask.png, report.json (dE check), preview_uv.png."""
import os, sys, json, argparse, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, cv2, torch
from PIL import Image
from skimage.color import rgb2lab, lab2rgb
import uvgeom, synth


def lab_of(rgb8): return rgb2lab(rgb8.astype(np.float32) / 255)
def rgb8_of(lab): return (np.clip(lab2rgb(lab), 0, 1) * 255 + 0.5).astype(np.uint8)


def patch_report(rgb8, patches, tone, r=14):
    """exactly the pipeline's report: median Lab of a (2r+1)^2 patch at each dE patch centre (c6_02 tex_patch_lab), dE76 vs the photo mean skin tone"""
    lab = lab_of(rgb8); out = {}
    for k, (px, py) in patches.items():
        m = np.median(lab[py - r:py + r + 1, px - r:px + r + 1].reshape(-1, 3), 0); out[k] = dict(Lab=m.tolist(), dE76=float(np.linalg.norm(m - tone)))
    out['mean_dE76'] = float(np.mean([v['dE76'] for v in out.values()])); return out


def medfilt(x, k, scale):
    """median filter of a float field via uint8 quantisation (scale = levels per unit)"""
    q = np.clip(np.round(x * scale) + 128, 0, 255).astype(np.uint8)
    return (cv2.medianBlur(q, k).astype(np.float32) - 128) / scale


def synth_maps(pos_mm, mask, args, tile_mm=None, chunk=1 << 20):
    """evaluate the layers at every texel of mask. pos_mm (R,R,3). returns dict of (R,R) float32 maps"""
    R = pos_mm.shape[0]; idx = np.flatnonzero(mask.ravel()); P = pos_mm.reshape(-1, 3)
    outs = {k: np.zeros(R * R, np.float32) for k in ('height', 'pore', 'line', 'low')}
    for s in range(0, len(idx), chunk):
        sl = idx[s:s + chunk]; p = torch.from_numpy(P[sl].astype(np.float32)).to(synth.DEV)
        d = synth.skin_layers(p, pore_mm=args.pore_mm, line_mm=args.line_mm, tile_mm=tile_mm, seed=args.seed)
        for k in outs: outs[k][sl] = synth.to_np(d[k])
    return {k: v.reshape(R, R) for k, v in outs.items()}


def whiteout(n1, n2):
    xy = n1[..., :2] + n2[..., :2]; z = n1[..., 2:3] * n2[..., 2:3]; n = np.concatenate([xy, z], -1); return n / (np.linalg.norm(n, axis=-1, keepdims=True) + 1e-9)


def main(argv=None):
    ap = argparse.ArgumentParser(); ap.add_argument('run'); ap.add_argument('--strength', type=float, default=1.5)
    ap.add_argument('--pore-mm', type=float, default=0.8); ap.add_argument('--line-mm', type=float, default=1.6)
    ap.add_argument('--normal', type=float, default=1.0); ap.add_argument('--albedo', type=float, default=1.0); ap.add_argument('--rough', type=float, default=1.0)
    ap.add_argument('--mottle', type=float, default=0.25); ap.add_argument('--seed', type=int, default=1); ap.add_argument('--out', default='skin_detail')
    a = ap.parse_args(argv); run = uvgeom.norm(a.run); t0 = time.time()
    uvgeom.setup_env(run); g = uvgeom.build(run); R = g['w'].shape[0]; w = g['w'].astype(np.float32); cov = g['cov']
    od = run + '/out/' + a.out + '/'; os.makedirs(od, exist_ok=True)
    pos_mm = g['pos'].astype(np.float64) * 1000.0
    m = synth_maps(pos_mm, cov, a); print('synth %.1fs on %s' % (time.time() - t0, synth.DEV))
    H = m['height']; du = g['du']; dv = g['dv']
    # --- normal (tangent space, OpenGL: +X = +u, +Y = +v (image up)). slope = dH(mm)/dx(mm)
    sx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) / (2 * du); sy = -(np.roll(H, -1, 0) - np.roll(H, 1, 0)) / (2 * dv)
    k = a.strength * a.normal * w
    n2 = np.stack([-sx * k, -sy * k, np.ones_like(sx)], -1); n2 /= np.linalg.norm(n2, axis=-1, keepdims=True)
    from c6_common import TEX
    base_n = np.array(Image.open(TEX + 'Head_Normal.png').convert('RGB')).astype(np.float32) / 255 * 2 - 1
    if base_n.shape[0] != R: base_n = cv2.resize(base_n, (R, R), interpolation=cv2.INTER_AREA)
    base_n /= np.linalg.norm(base_n, axis=-1, keepdims=True) + 1e-9
    n = whiteout(base_n, n2); Image.fromarray((np.clip(n * 0.5 + 0.5, 0, 1) * 255 + 0.5).astype(np.uint8)).save(od + 'normal_detail.png')
    Image.fromarray((np.clip(n2 * 0.5 + 0.5, 0, 1) * 255 + 0.5).astype(np.uint8)).save(od + 'layer_normal.png')
    # --- roughness: 0.5 (the build's constant) + zero-mean modulation (pores / furrows rougher)
    base_r = 0.5; hp = H - cv2.GaussianBlur(H, (0, 0), 6)
    mod = (0.16 * m['pore'] + 0.07 * m['line'] + 0.9 * hp / 0.035 * 0.02) * a.strength * a.rough * w
    mod -= (mod * w).sum() / max((w * w).sum(), 1e-6) * w
    Image.fromarray(np.clip((base_r + mod) * 255 + 0.5, 0, 255).astype(np.uint8)).save(od + 'roughness_detail.png')
    # --- albedo: Lab delta with the local (29-31 px) median removed, so the pipeline's median patch (and the skin mean tone) is unchanged
    alb = np.array(Image.open(run + '/bake/Head_BaseColor_char.png').convert('RGB')); lab0 = lab_of(alb)
    s = a.strength * a.albedo
    dL = s * 1.7 * hp / 0.035 * w                                 # pores/furrows darker, ridges lighter
    da = s * (0.40 * m['pore'] + 0.15 * m['line']) * w           # pores a touch redder
    db = s * (0.10 * m['pore']) * w
    for ch in (dL, da, db):
        ch -= w * medfilt(ch, 31, 60)
    lowd = a.mottle * s * w
    mL, ma, mb = 0.30 * m['low'] * lowd, 0.55 * m['low'] * lowd, 0.35 * np.roll(m['low'], 97, 0) * lowd
    for ch in (mL, ma, mb): ch -= w * medfilt(ch, 91, 60)
    d = np.stack([dL + mL, da + ma, db + mb], -1)
    # anchor correction: the metric patches (cheeks, forehead) must keep their median Lab. A smooth (sigma 150 px) offset field, built from the measured patch shifts, is
    # subtracted (iterated: 8-bit rounding moves a median by a fraction of a level), so the mean skin tone is preserved where the pipeline measures it, and nowhere is a seam created.
    patches = json.loads(str(g['patches'])); tone = np.asarray(g['tone'], np.float64); yy, xx = np.mgrid[0:R, 0:R]
    gk = {k: np.exp(-((xx - px) ** 2 + (yy - py) ** 2) / (2 * 150.0 ** 2)).astype(np.float32) for k, (px, py) in patches.items()}
    gs = np.maximum(sum(gk.values()), 1.0)
    for it in range(4):
        new = rgb8_of(lab0 + d); labn = lab_of(new); fld = np.zeros_like(d)
        for k, (px, py) in patches.items():
            sh = np.median(labn[py - 14:py + 15, px - 14:px + 15].reshape(-1, 3), 0) - np.median(lab0[py - 14:py + 15, px - 14:px + 15].reshape(-1, 3), 0)
            fld += gk[k][..., None] / gs[..., None] * sh
        if np.abs(fld).max() < 0.01: break
        d = d - fld * w[..., None]
    new = rgb8_of(lab0 + d)
    Image.fromarray(new).save(od + 'albedo_detail.png')
    dd = lab_of(new) - lab0; Image.fromarray((np.clip(dd[..., 0] / 8 + 0.5, 0, 1) * 255).astype(np.uint8)).save(od + 'layer_albedo_delta.png')
    Image.fromarray((w * 255).astype(np.uint8)).save(od + 'skin_mask.png')
    # --- verification vs the pipeline metric
    r0 = patch_report(alb, patches, tone); r1 = patch_report(new, patches, tone)
    sk = w > 0.5
    rep = dict(args=vars(a), mm_per_texel_u=float(np.median(du[sk])), mm_per_texel_v=float(np.median(dv[sk])), skin_texels=int(sk.sum()),
               baseline=r0, detailed=r1, delta_mean_dE76=r1['mean_dE76'] - r0['mean_dE76'], within_0p05=bool(abs(r1['mean_dE76'] - r0['mean_dE76']) <= 0.05),
               masked_mean_Lab_shift=(dd[sk].mean(0)).tolist(), masked_median_Lab_shift=np.median(dd[sk], 0).tolist(), masked_std_L_added=float(dd[sk][:, 0].std()),
               normal_slope_rms=float(np.sqrt(((n2[..., :2][sk]) ** 2).sum(1).mean())), rough_range=[float((base_r + mod)[sk].min()), float((base_r + mod)[sk].max())])
    json.dump(rep, open(od + 'report.json', 'w'), indent=1)
    print(json.dumps({k: rep[k] for k in ('mm_per_texel_u', 'delta_mean_dE76', 'within_0p05', 'masked_mean_Lab_shift', 'masked_median_Lab_shift', 'masked_std_L_added', 'normal_slope_rms')}, indent=1))
    # --- uv preview: cheek crop before/after + detail normal
    px, py = patches['cheek_R(img-left)']; sl = (slice(py - 160, py + 160), slice(px - 160, px + 160))
    up = lambda x: cv2.resize(x, (640, 640), interpolation=cv2.INTER_NEAREST)
    nimg = (np.clip(n * 0.5 + 0.5, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(np.concatenate([up(alb[sl]), up(new[sl]), up(nimg[sl])], 1)).save(od + 'preview_uv.png')
    print('done %.1fs -> %s' % (time.time() - t0, od)); return rep


if __name__ == '__main__': main()
