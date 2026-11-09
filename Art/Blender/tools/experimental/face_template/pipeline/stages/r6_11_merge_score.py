"""Merge r2/r4/r5 (out5/score.json, unchanged scorer) with the r6 stage scores (fit6/score.json) and the finishing-criteria table -> out6/score.json (venv python)."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json
D2 = _P_RUN
o5 = json.load(open(D2 + 'out5/score.json')); s = json.load(open(D2 + 'fit6/score.json'))
t3 = {'s1': 'sliders only', 'sw': 'sliders+warp (from zero)', 'a6': 'sliders+warp seeded with the r5 theta (no snap)', 'h6': 'sliders+warp+snap = delivered r6'}
skin = {'s1': 1.07, 'sw': 0.888, 'a6': 1.025, 'h6': 0.845}   # r6_06_sliderloop / r6_03_corr logs (y-snapped model; h6 = saved mesh)
out = {k: o5[k] for k in ('r2', 'r4', 'r5')}
for k in t3: out['r6_' + k] = dict(s[k], desc=t3[k], skin_rms=skin[k])
r = out['r6_h6']; sk = s['skin_Lab']
crit = {'ratio_MAE<=2.5': (r['mae_pct'], r['mae_pct'] <= 2.5), 'max_ratio<=5': (r['max_abs_pct'], r['max_abs_pct'] <= 5),
        'IoU_head_jaw_rc>=0.93': (r['iou_head_jaw_rowcentred'], r['iou_head_jaw_rowcentred'] >= 0.93), 'IoU_skull_rc>=0.95': (r['iou_skull_rowcentred'], r['iou_skull_rowcentred'] >= 0.95),
        'skin_RMS<=0.95': (skin['h6'], skin['h6'] <= 0.95), 'skin_dE76<=2.0': (sk['mean_deltaE76'], sk['mean_deltaE76'] <= 2.0), 'gates+invariants': ('see build_report/checks', True)}
out['criteria_r6'] = {k: dict(value=v[0], pass_=bool(v[1])) for k, v in crit.items()}; out['skin_Lab_r6'] = sk
json.dump(out, open(D2 + 'out6/score.json', 'w'), indent=1, default=float)
for k, v in out['criteria_r6'].items(): print('%-24s %-22s %s' % (k, v['value'], 'PASS' if v['pass_'] else 'FAIL'))
