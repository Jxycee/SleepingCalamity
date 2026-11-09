"""Post-snap ratio re-fit: accept-or-keep (venv python). The snap (normal projection onto the 3D target + skull width/crown step) improves the silhouette but moves the detector landmarks
(char3: loop ratio MAE 1.7 % / worst 3.3 % before the snap, 2.5 % / 9.5 % after). A short slider-only fit loop is run on top of the snapped mesh (r6_06_sliderloop, same gates), written as tag <cand>;
the caller scored both renders with r6_10_score (labels base, cand -> fit/score.json). The candidate replaces the snapped mesh only when it does not regress: key (median MAE + median worst / 4 - 15 * mean IoU)
lower by >= 0.1, 3D skin RMS not worse than max(base * 1.03, 0.94 mm; the criterion is 0.95), head+jaw and skull IoU each no more than 0.04 lower, gates pass, no flips.
usage: r6_12_refit_accept.py <base_tag> <cand_tag>     writes fit/refit_decision.json; on accept copies wrap_r<cand>_{basis,keys,mesh} over wrap_r<base>_* and updates wrap_r<base>_report.json"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json, shutil
import numpy as np
from r6_common import *
from fit_common import count_flips
base, cand = sys.argv[1], sys.argv[2]
s = json.load(open(FIT + 'score.json')); b, c = s['base'], s['cand']
key = lambda v: v['mae_pct'] + v['max_abs_pct'] / 4 - 15 * (v['iou_head_jaw'] + v['iou_skull']) / 2   # 1 % of IoU is worth 0.15 ratio-%
w = json.load(open(FIT + f'warp_{cand}.json')); gates_ok = bool(w.get('checks', {}).get('PASS'))
t = Tpl(); P = np.load(FIT + f'wrap_r{cand}_basis.npy').astype(np.float64); fl = int(count_flips(t, P).sum())
rb = json.load(open(FIT + f'wrap_r{base}_report.json')); sk_b = (rb.get('target') or {}).get('skin_rms'); sk_c = (w.get('target') or {}).get('skin_rms')
sk_ok = sk_b is None or sk_c is None or sk_c <= max(sk_b * 1.03, 0.94)      # the 3D skin RMS must not worsen (a re-fit trades surface error for ratios otherwise)
ok = sk_ok and (key(c) <= key(b) - 0.1) and (c['iou_head_jaw'] >= b['iou_head_jaw'] - 0.04) and (c['iou_skull'] >= b['iou_skull'] - 0.04) and gates_ok and fl == 0
dec = dict(accepted=bool(ok), base=dict(mae=b['mae_pct'], worst=b['max_abs_pct'], iou_hj=b['iou_head_jaw'], iou_sk=b['iou_skull'], key=key(b)), cand=dict(mae=c['mae_pct'], worst=c['max_abs_pct'], iou_hj=c['iou_head_jaw'], iou_sk=c['iou_skull'], key=key(c)), gates_ok=gates_ok, flips=fl, skin_rms=dict(base=sk_b, cand=sk_c, ok=bool(sk_ok)))
json.dump(dec, open(FIT + 'refit_decision.json', 'w'), indent=1)
print('REFIT %s: base MAE %.2f worst %.2f IoU %.3f/%.3f | cand MAE %.2f worst %.2f IoU %.3f/%.3f | gates %s flips %d' % ('ACCEPTED' if ok else 'REJECTED (snapped mesh kept)', b['mae_pct'], b['max_abs_pct'], b['iou_head_jaw'], b['iou_skull'],
      c['mae_pct'], c['max_abs_pct'], c['iou_head_jaw'], c['iou_skull'], gates_ok, fl))
if ok:
    for suf in ('basis.npy', 'keys.npy', 'mesh.npz'): shutil.copy(FIT + f'wrap_r{cand}_{suf}', FIT + f'wrap_r{base}_{suf}')
    rep = json.load(open(FIT + f'wrap_r{base}_report.json')); rep.update(flips=fl, target=w.get('target'), checks=w.get('checks'), refit=dict(theta=w.get('theta'), tag=cand))
    json.dump(rep, open(FIT + f'wrap_r{base}_report.json', 'w'), indent=1, default=float)
print('REFIT DONE')
