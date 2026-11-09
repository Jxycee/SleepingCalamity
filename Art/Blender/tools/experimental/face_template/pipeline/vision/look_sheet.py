"""One image per character that shows shape problems at a glance.
usage (facekeys python, or any python with numpy+cv2+scipy+PIL):
    python look_sheet.py <run_dir> [--tag h6] [--force-clay] [--arrow-gain 3] [--no-plaus]      -> <run_dir>/out/look_sheet.png (+ <run_dir>/out/look/*)
Row 1  CLAY (untextured, matte, same fixed ortho cameras, scale and light for every character; 1 px = 0.49 mm, ruler = 5 cm): front, 3/4, side, top (nose up).
       The front panel carries thin proportion guides from the mesh (crown, eye line, nose base, mouth, chin) with head-height fractions (0 = crown, 1 = chin).
Row 2  the PHOTO with guides at the photo's own fractions; the 50 % BLEND of photo and the fitted render (render registered to the photo by the 4 eye corners, as the scorer does;
       guides: cyan = photo, orange = render); the DIFFERENCE map (red = render outside the photo head mask, blue = photo head the render does not cover, rows
       above the chin only, neck/collar excluded like the scorer); landmark MISS ARROWS (render landmark -> photo landmark, pixels, drawn x gain; eye corners are zero by construction).
Header: scorer numbers (ratio MAE / worst, IoU, head aspect) and the plausibility flags (needs human_prior.json; see plausibility.py). Footer: top landmark misses.
The mesh is the run's FINAL tag (out/score.json final_tag; fit/_rr_<tag>.npz with eyeballs), rendered by clay_views.py in Blender (background)."""
import os, sys, json, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import vcommon as VC, proportions as PR

P_W = 580; GUT = 8
KEY_LM = {'brow': (105, 334), 'eye': (33, 263, 133, 362), 'nose wing': (129, 358), 'nose base': (2,), 'mouth corner': (61, 291), 'lip mid': (13, 0, 17), 'chin': (152,), 'jaw': (172, 397), 'cheek': (234, 454), 'forehead': (10,)}
FONT = cv2.FONT_HERSHEY_SIMPLEX


def put(img, txt, xy, scale=0.5, col=(255, 255, 255), th=1, shadow=True):
    x, y = int(xy[0]), int(xy[1])
    if shadow:
        for dx, dy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):   # same thickness as the text: a thicker shadow gets a wider glyph advance and ghosts
            cv2.putText(img, txt, (x + dx, y + dy), FONT, scale, (0, 0, 0), th, cv2.LINE_AA)
    cv2.putText(img, txt, (x, y), FONT, scale, col, th, cv2.LINE_AA)


def square_crop(img, cx, cy, half):
    """crop a square (2*half) around (cx, cy), padding with edge colour; returns crop and (x0, y0)"""
    x0, y0 = int(round(cx - half)), int(round(cy - half)); n = int(round(2 * half))
    pad = max(0, -x0, -y0, x0 + n - img.shape[1], y0 + n - img.shape[0]) + 1
    big = cv2.copyMakeBorder(img, pad, pad, pad, pad, cv2.BORDER_REPLICATE)
    return big[y0 + pad:y0 + pad + n, x0 + pad:x0 + pad + n], (x0, y0)


def fit_panel(img, size=P_W):
    return cv2.resize(img, (size, size), interpolation=cv2.INTER_AREA if img.shape[0] > size else cv2.INTER_LINEAR)


def guides(lm, top, col, img, x0, y0, sc, label_side='left', tag=''):
    """horizontal guides at crown, eye line, nose base, mouth, chin; fractions of crown-to-chin height (0 crown, 1 chin). lm in the frame the image was cropped from."""
    chin = lm[152][1]; H = chin - top; eye = float(np.mean(lm[[33, 263, 133, 362], 1]))
    items = [('crown', top), ('eye', eye), ('nose', lm[2][1]), ('mouth', lm[13][1]), ('chin', chin)]
    w = img.shape[1]; out = {}
    for name, y in items:
        yy = int(round((y - y0) * sc)); frac = (y - top) / H; out[name] = frac
        cv2.line(img, (0, yy), (w, yy), col, 1, cv2.LINE_AA)
        txt = '%s %.2f' % (name, frac) if not tag else '%s%s %.2f' % (tag, name[0], frac)
        put(img, txt, (4 if label_side == 'left' else w - 6 - cv2.getTextSize(txt, FONT, 0.4, 1)[0][0], yy - 3), 0.4, col)
    return out


def clay_front_guides(run, img, mf):
    """guides on the clay front from the mesh landmarks (fit/lm_<tag>.json through the render camera into the clay camera) and the clay silhouette crown"""
    top = int(np.where(mf.sum(1) >= 3)[0].min())
    L = np.array([VC.xz_to_clay_px(*VC.render_px_to_xz(run, *p)) for p in run.lm_render])
    sc = img.shape[0] / VC.R2; ov = img.copy()
    fr = guides(L, top, (255, 200, 40), ov, 0, 0, sc)
    return ov, fr, L, top


def side_guides(run, img, L, top):
    ov = img.copy(); sc = img.shape[0] / VC.R2
    for name, y in (('crown', top), ('eye', float(np.mean(L[[33, 263, 133, 362], 1]))), ('nose', L[2][1]), ('chin', L[152][1])):
        yy = int(round(y * sc)); cv2.line(ov, (0, yy), (ov.shape[1], yy), (255, 200, 40), 1, cv2.LINE_AA)
    return ov


def ruler(img, cm=5):
    px = cm * 10 / (VC.S2 / VC.R2 * 1000) * img.shape[0] / VC.R2; h = img.shape[0]; x = 14; y = h - 14
    cv2.line(img, (x, y), (int(x + px), y), (240, 240, 240), 2); cv2.line(img, (x, y - 4), (x, y + 4), (240, 240, 240), 2); cv2.line(img, (int(x + px), y - 4), (int(x + px), y + 4), (240, 240, 240), 2)
    put(img, '%d cm' % cm, (x + px + 6, y + 4), 0.42)


def build(run, arrow_gain=3.0, force_clay=False, plaus=True):
    run.ensure_clay(force_clay)
    ph = run.photo; H, W = ph.shape[:2]
    # ---- registration render -> photo
    M = VC.sim_render_to_photo(run); rn = cv2.imread(run.rn)
    bgc = tuple(int(x) for x in rn[2, 2]); warp = cv2.warpAffine(rn, M, (W, H), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_CONSTANT, borderValue=bgc)
    wm = cv2.warpAffine(VC.bg_mask(rn).astype(np.uint8), M, (W, H), flags=cv2.INTER_NEAREST).astype(bool)
    head = VC.photo_head_mask(run); PM = cv2.transform(run.lm_render[None].astype(np.float32), M)[0]; PL = run.lm_photo
    ychin = int(PL[152][1]); yjaw = int(round((PL[172][1] + PL[397][1]) / 2)); ybrow = float(PL[[105, 334], 1].mean())
    sel = np.broadcast_to((np.arange(H) <= ychin)[:, None], (H, W))
    rtop = int(np.where(wm.sum(1) >= 3)[0].min()); ptop = int(np.where(head.sum(1) >= 3)[0].min())
    # ---- crop window around the head in photo frame
    cx = float((PL[234][0] + PL[454][0]) / 2); cy = (min(ptop, rtop) + ychin) / 2 + 0.04 * (ychin - ptop); half = 0.62 * (ychin - min(ptop, rtop))
    sc = P_W / (2 * half)
    # ---- panels row 2
    pc, (x0, y0) = square_crop(ph, cx, cy, half); photo_p = fit_panel(pc)
    wc, _ = square_crop(warp, cx, cy, half); render_p = fit_panel(wc)
    blend = cv2.addWeighted(photo_p, 0.5, render_p, 0.5, 0)
    # photo panel with its own guides
    pg = photo_p.copy(); pf = guides(PL, ptop, (255, 255, 0), pg, x0, y0, sc)
    # blend with both guide sets (cyan photo = BGR (255,255,0), orange render = (0,150,255))
    bg_ = blend.copy(); guides(PL, ptop, (255, 255, 0), bg_, x0, y0, sc, label_side='left'); rf = guides(PM, rtop, (0, 150, 255), bg_, x0, y0, sc, label_side='right')
    put(bg_, 'cyan photo / orange render', (6, 16), 0.45, (255, 255, 255))
    # difference map
    mine = (head & ~wm) & sel; extra = (wm & ~head) & sel
    dm = np.full((H, W, 3), 38, np.uint8); dm[head & wm & sel] = (200, 200, 200); dm[extra] = (40, 40, 235); dm[mine] = (235, 120, 30)
    below = ~sel; dm[below & head] = (90, 90, 90); dm[below & wm & ~head] = (70, 70, 110)
    dc, _ = square_crop(dm, cx, cy, half); diff_p = cv2.resize(dc, (P_W, P_W), interpolation=cv2.INTER_NEAREST)
    # outline of the photo mask on the diff for orientation
    cnt, _ = cv2.findContours(head.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    for c in cnt: cv2.polylines(diff_p, [np.round((c.reshape(-1, 2) - [x0, y0]) * sc).astype(np.int32)], True, (255, 255, 255), 1, cv2.LINE_AA)
    nred, nblue, nun = int(extra.sum()), int(mine.sum()), int((head | wm)[sel].sum())
    put(diff_p, 'red: render outside photo  blue: photo not covered', (6, 16), 0.42)
    put(diff_p, 'above chin only; red %.1f%% blue %.1f%% of union' % (100 * nred / max(nun, 1), 100 * nblue / max(nun, 1)), (6, 34), 0.42)
    # arrows
    io = float(np.linalg.norm(PL[33] - PL[263])); miss = PL - PM; ar = photo_p.copy(); ar = (0.55 * ar + 0.45 * 90).astype(np.uint8)
    rows = []
    for grp, ids in KEY_LM.items():
        for i in ids:
            a = (PM[i] - [x0, y0]) * sc; b = (PL[i] - [x0, y0]) * sc; d = miss[i]; mag = float(np.hypot(*d)); rows.append((grp, i, mag, d))
            tip = a + (b - a) * arrow_gain
            cv2.circle(ar, tuple(np.round(a).astype(int)), 3, (255, 160, 0), -1, cv2.LINE_AA)
            if mag * sc * arrow_gain > 1.5: cv2.arrowedLine(ar, tuple(np.round(a).astype(int)), tuple(np.round(tip).astype(int)), (60, 60, 255), 2, cv2.LINE_AA, tipLength=0.3)
    put(ar, 'miss render->photo x%g (orange dot = render landmark)' % arrow_gain, (6, 16), 0.42)
    put(ar, 'eye corners registered (zero by construction)', (6, 34), 0.42)
    rows.sort(key=lambda r: -r[2]); mean_miss = float(np.mean([r[2] for r in rows if r[0] != 'eye']))
    # ---- row 1 clay
    cl = {v: VC.read_clay(run.clay(v))[0] for v in ('front', 'q34', 'side', 'top')}; mfront = VC.read_clay(run.clay('front'))[1]
    cf, fr, L, ctop = clay_front_guides(run, cl['front'], mfront); cs = side_guides(run, cl['side'], L, ctop)
    r1 = [fit_panel(cf), fit_panel(cl['q34']), fit_panel(cs), fit_panel(cl['top'])]
    for p, t in zip(r1, ('CLAY front + guides', 'CLAY 3/4', 'CLAY side + guides', 'CLAY top, nose up')): put(p, t, (P_W - 8 - cv2.getTextSize(t, FONT, 0.5, 1)[0][0], P_W - 8), 0.5)
    ruler(r1[0]); ruler(r1[2]); ruler(r1[3])
    for p, t in zip((pg, bg_, diff_p, ar), ('PHOTO + its fractions', '50% BLEND', 'DIFFERENCE', 'LANDMARK MISS')): put(p, t, (P_W - 8 - cv2.getTextSize(t, FONT, 0.5, 1)[0][0], P_W - 8), 0.5)
    # ---- header / footer
    fin = run.final(); crit = run.score.get('criteria', {}); wtot = 4 * P_W + 3 * GUT + 2 * GUT
    head_lines = []
    def cv(k, fmt='%.2f'):
        v = crit.get(k, {}); return (fmt % v['value'] + (' ok' if v.get('pass') else ' FAIL')) if 'value' in v else 'n/a'
    ha = fin.get('head_aspect', {})
    head_lines.append('%s   final tag %s   ratio MAE %s  worst %s   IoU head+jaw %s  skull %s   head aspect err %s (render %.3f vs photo %.3f; cheek-width variant %+.1f%%)' % (
        run.name, run.tag, cv('ratio_mae_pct'), cv('ratio_worst_pct'), cv('iou_head_jaw_rc', '%.3f'), cv('iou_skull_rc', '%.3f'), cv('head_aspect_err_pct'),
        ha.get('render', {}).get('aspect', float('nan')), ha.get('photo', {}).get('aspect', float('nan')), ha.get('err_cheek_pct', float('nan'))))
    mfr = {k: rf.get(k) for k in rf}; head_lines.append('guides (fraction of crown->chin): photo eye %.2f nose %.2f mouth %.2f | render eye %.2f nose %.2f mouth %.2f | clay-front eye %.2f nose %.2f mouth %.2f' % (
        pf['eye'], pf['nose'], pf['mouth'], rf['eye'], rf['nose'], rf['mouth'], fr['eye'], fr['nose'], fr['mouth']))
    pl_lines = []
    try:
        import plausibility as PL_
        prior = json.load(open(PL_.PRIOR)); res = PL_.analyse(run, prior); txt, flags = PL_.report(res, prior)
        pl_lines.append('PLAUSIBILITY vs %d SDXL faces (mesh sigma, photo sigma in brackets): ' % prior['n_faces'])
        top = sorted(res['z_mesh'].items(), key=lambda kv: -abs(kv[1]))[:6]
        pl_lines.append('   '.join('%s %+.1f (%+.1f)' % (k.replace('_over_', '/').replace('_in_', '@'), z, res['z_photo'].get(k, float('nan'))) for k, z in top))
        pl_lines.append('flags: ' + ('; '.join(flags[:5]) if flags else 'none beyond +-2 sigma'))
        m = res['mahalanobis']; pl_lines.append('joint distance (Mahalanobis): mesh %s photo %s (typical %.1f)' % ('%.1f' % m['mesh'] if m['mesh'] else 'n/a', '%.1f' % m['photo'] if m['photo'] else 'n/a', np.sqrt(m['keys'])))
    except Exception as e:
        pl_lines.append('plausibility not available: %s' % e)
    foot = ['largest landmark misses (px in photo frame; inter-ocular = %.0f px): ' % io + ', '.join('%s#%d %.1fpx (%.1f%% IO)' % (g, i, m, 100 * m / io) for g, i, m, d in rows[:7]) + '  | mean non-eye miss %.1f px' % mean_miss]
    hh = 22 * (len(head_lines) + len(pl_lines)) + 12; fh_ = 22 * len(foot) + 12
    canvas = np.full((hh + 2 * P_W + 3 * GUT + fh_, wtot, 3), 24, np.uint8)
    y = 18
    for t in head_lines: put(canvas, t, (GUT, y), 0.5, shadow=False); y += 22
    for i, t in enumerate(pl_lines): put(canvas, t, (GUT, y), 0.5, (120, 220, 255) if i == 0 else (255, 255, 255) if not t.startswith('flags') else (90, 190, 255), shadow=False); y += 22
    for r, row in enumerate((r1, [pg, bg_, diff_p, ar])):
        for c, p in enumerate(row): canvas[hh + r * (P_W + GUT):hh + r * (P_W + GUT) + P_W, GUT + c * (P_W + GUT):GUT + c * (P_W + GUT) + P_W] = p
    y = hh + 2 * P_W + 3 * GUT + 18
    for t in foot: put(canvas, t, (GUT, y), 0.5, shadow=False)
    out = run.out + 'look_sheet.png'; cv2.imwrite(out, canvas)
    json.dump(dict(run=run.name, tag=run.tag, misses_px=[dict(group=g, landmark=int(i), px=m, dx=float(d[0]), dy=float(d[1])) for g, i, m, d in rows], mean_nonEye_miss_px=mean_miss, io_px=io,
                   guides=dict(photo=pf, render=rf, clay_front=fr), diff=dict(red_frac=nred / max(nun, 1), blue_frac=nblue / max(nun, 1))), open(run.look + 'look_metrics.json', 'w'), indent=1, default=float)
    return out


if __name__ == '__main__':
    a = sys.argv[1:]; tag = a[a.index('--tag') + 1] if '--tag' in a else None; gain = float(a[a.index('--arrow-gain') + 1]) if '--arrow-gain' in a else 3.0
    run = VC.Run(a[0], tag); print(build(run, gain, '--force-clay' in a))
