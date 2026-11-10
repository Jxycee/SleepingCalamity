"""Gate for generated turnaround views BEFORE any 3D work.
usage (facekeys python): python mv_views_check.py <views_dir> <tag> <guide_dir>
1. segments every view (Lab background fit), scores LEFT profile and (mirrored) RIGHT profile against side_prior.json (z-scores in words, nominal mm),
2. compares left vs right profile (a real head is near-symmetric front-back; a big difference = inconsistent generation),
3. circularity check: head-frame IoU of every generated silhouette against the DEPTH-GUIDE silhouette (same camera). IoU near 0.99 = the views just copy the guide (we measured
   the template, not a new head); the value must be read together with the guide's own side score.
Writes <views_dir>/<tag>_check.txt/.json and <tag>_check_sheet.png (generated image with guide outline in orange and generated outline in green)."""
import sys, os, json, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mv_common as MC, side_prior as SP

vd, tag, gd = sys.argv[1], sys.argv[2], sys.argv[3]; P = MC.prior(); L = []; J = {}
masks = {}; guide = {}
for v in MC.VIEWS:
    masks[v] = MC.seg_view(vd, tag, v); guide[v] = cv2.imread(f'{gd}/sil_{v}.png', 0) > 127
prof = {'left': masks['left'], 'right': masks['right'][:, ::-1]}; gprof = {'left': guide['left'], 'right': guide['right'][:, ::-1]}
ms = {}
for k in ('left', 'right'):
    m, lines, js = MC.score_profile(f'generated {k} profile', prof[k], P); ms[k] = m
    L += (lines if lines else [f'{k}: anchors failed']); L.append(''); J['gen_' + k] = js
for k in ('left',):
    m, lines, js = MC.score_profile('GUIDE (template) left profile', gprof[k], P); ms['guide'] = m
    L += (lines if lines else ['guide anchors failed']); L.append(''); J['guide_left'] = js
# left vs right profile agreement (radial curves in H units)
if ms['left'] and ms['right']:
    a, b = np.array(ms['left']['radial']), np.array(ms['right']['radial']); d = a - b
    L.append('left vs right profile: depth %.3f vs %.3f H, radial |diff| mean %.3f H max %.3f H (x225 mm: mean %.1f max %.1f)' % (ms['left']['scalars']['depth'], ms['right']['scalars']['depth'], np.abs(d).mean(), np.abs(d).max(), np.abs(d).mean() * 225, np.abs(d).max() * 225))
    J['lr_radial_mean_H'] = float(np.abs(d).mean()); J['lr_radial_max_H'] = float(np.abs(d).max())
# H for every view: the profile H in px (same camera for all tiles in one generation): use the mean of left/right
Hs = [m['H_px'] for m in (ms['left'], ms['right']) if m]
Hg = ms['guide']['H_px'] if ms.get('guide') else None
if Hs and Hg:
    Hgen = float(np.mean(Hs)); L.append('H (crown-menton) generated %.0f px vs guide %.0f px (ratio %.2f)' % (Hgen, Hg, Hgen / Hg)); J['H_ratio'] = Hgen / Hg
    L.append('head-frame silhouette IoU vs depth guide (crown aligned, scale 1/H each, centred; rows crown..0.95H):'); J['iou_guide'] = {}
    for v in MC.VIEWS:
        Hv = ms['left']['H_px'] if v != 'right' else ms['right']['H_px']; Hv = Hgen
        a = MC.canon(masks[v], Hgen); b = MC.canon(guide[v], Hg); i = MC.iou(a, b); J['iou_guide'][v] = i; L.append('  %-6s %.3f' % (v, i))
txt = '\n'.join(L); print(txt); open(f'{vd}/{tag}_check.txt', 'w').write(txt + '\n'); json.dump(J, open(f'{vd}/{tag}_check.json', 'w'), indent=1)
cells = []
for v in MC.VIEWS:
    im = cv2.imread(f'{vd}/{tag}_{v}.png')
    for mk, col in ((guide[v], (0, 140, 255)), (masks[v], (0, 220, 0))):
        mk8 = cv2.resize(mk.astype(np.uint8), (im.shape[1], im.shape[0]), interpolation=cv2.INTER_NEAREST); cs, _ = cv2.findContours(mk8, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE); cv2.drawContours(im, cs, -1, col, 2)
    cells.append(im)
cv2.imwrite(f'{vd}/{tag}_check_sheet.png', np.vstack([np.hstack(cells[:3]), np.hstack(cells[3:])]))
