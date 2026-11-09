"""Stage 12 helper (venv python): merge the r6_10_score output (fit/score.json, labels 'fit' and 'final') with the 3D report, gate/invariant facts and the config thresholds
-> out/score.json (the scorecard) and a printed table. Replaces r6_11_merge_score (which needed the Cloud-only out5/score.json); an optional cfg.compare_to
(path to another score.json with the same keys) adds a delta column.
usage: score_final.py <cfg.json> <final_tag>"""
import os, sys, json, re
cfg = json.load(open(sys.argv[1])); tag = sys.argv[2]; R = os.environ['SCP_RUN'].rstrip('/') + '/'
s = json.load(open(R + 'fit/score.json')); f = s['final']; th = cfg['thresholds']
rep = json.load(open(R + 'fit/wrap_r%s_report.json' % tag)); tg = rep.get('target', {}) or {}
skin_rms = tg.get('skin_rms'); chk = rep.get('checks', {}) or {}
def rd(p, d=None):
    try: return json.load(open(p))
    except Exception: return d
teeth = rd(R + 'bake/teeth_counts.json'); build = ''
try: build = open(R + 'fit/build.log', errors='ignore').read()
except Exception: pass
inv_ok = ('BUILD DONE' in build) and ('Traceback' not in build) and ('AssertionError' not in build)
_k = re.findall(r'INVARIANT \{"variant": "full", "part": "head".*?"nkeys": (\d+)', build); keys = int(_k[-1]) if _k else None
sk = s.get('skin_Lab') or {}
pl = None      # round 15: skull plausibility vs the SDXL face prior (vision/plausibility.py, no clay render): value <= 3 sigma, a feature the mesh shares with its own photo (|z| <= photo + 0.5) is waived
try:
    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'vision')); import plausibility as _PL
    pl = _PL.skull_check(R.rstrip('/'), tag)
except Exception as _ex: print('plausibility not scored:', _ex)
sp = None      # round 16: side-profile joint distance vs the SDXL side prior (mesh silhouette rasterised, no Blender). REPORT-ONLY: the front of the skull (theta 120-165) is pinned to the photo and sits ~2 sigma inside the SDXL median on all four heads, so the joint distance cannot reach the prior p95 (3.7) from the back-of-skull warp alone
try:
    import numpy as _np; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import r6_16_skulldepth as _SD
    _d = _np.load(R + 'fit/_rr_%s.npz' % tag); _m = _SD.measure(_d['v'], _d['f']); _P = _SD.prior(); _z = _np.array(_m['radial']) - _np.array(_P['radial']['median']); _z = _z / _np.array(_P['radial']['sigma']); _th = _np.array(_SD.SP.TH)
    sp = dict(joint=_SD.joint_distance(_m), p95=_P['pca_radial']['dist_p95'], back_mean_z=float(_z[(_th >= -20) & (_th <= 85)].mean()), back_min_z=float(_z[(_th >= -20) & (_th <= 85)].min()), front_mean_z=float(_z[(_th >= 120) & (_th <= 165)].mean()),
              dome_round_z=float(_SD.z_scalars(_m)['dome_round']), depth_H=float(_m['scalars']['depth']), report_only=True)
except Exception as _ex: print('side profile not scored:', _ex)
crit = {
 'ratio_mae_pct': (f['mae_pct'], th['ratio_mae_max'], '<='), 'ratio_worst_pct': (f['max_abs_pct'], th['ratio_worst_max'], '<='),
 'iou_head_jaw_rc': (f.get('iou_head_jaw_rowcentred_oval', f['iou_head_jaw_rowcentred']), th['iou_head_jaw_min'], '>='), 'iou_skull_rc': (f['iou_skull_rowcentred'], th['iou_skull_min'], '>='),
 'head_aspect_err_pct': (max(abs(f['head_aspect']['err_pct']), abs(f['head_aspect']['err_cheek_pct'])) if f.get('head_aspect') else None, th.get('head_aspect_err_max', 3.0), '<='),
 'skin_rms_3d_mm': (skin_rms, th['skin_rms_3d_max'], '<='), 'skin_dE76': (sk.get('mean_deltaE76'), th['skin_dE76_max'], '<='),
 'plausibility_skull_z': (pl['value'] if pl else None, th.get('plausibility_skull_z_max', 3.0), '<='),
 'flips': (rep.get('flips'), 0, '<='), 'invariants_build_ok': (1 if inv_ok else 0, 1, '>='), 'head_keys': (keys, th.get('head_keys_min', 184), '>=')}
out = dict(name=cfg['name'], final_tag=tag, final=f, plausibility_skull=pl, side_profile=sp, ratio_jitter={k: f.get(k) for k in ('mae_nominal', 'mae_min', 'mae_max', 'max_abs_nominal', 'max_abs_min', 'max_abs_max', 'jitter_n', 'err_pct_median')}, fit_stage=s.get('fit'), skin_Lab=sk, skin_rms_3d_mm=skin_rms, teeth=teeth, head_keys=keys, invariants_ok=inv_ok, gates=chk,
           criteria={k: dict(value=v[0], threshold=v[1], op=v[2], **{'pass': (v[0] is not None and (v[0] <= v[1] if v[2] == '<=' else v[0] >= v[1]))}) for k, v in crit.items()})
if cfg.get('compare_to'):
    o = rd(cfg['compare_to'], {})
    out['compare_to'] = cfg['compare_to']
    for k, v in out['criteria'].items():
        ref = (o.get('criteria') or {}).get(k, {}).get('value')
        if ref is not None and v['value'] is not None: v['ref'] = ref; v['delta'] = v['value'] - ref
json.dump(out, open(R + 'out/score.json', 'w'), indent=1, default=float)
print('ratio scoring = median over %s framings (+-1%% scale/shift): MAE %.2f [%.2f..%.2f] nominal %.2f | worst %.2f [%.2f..%.2f] nominal %.2f' % (f.get('jitter_n'), f['mae_pct'], f['mae_min'], f['mae_max'], f['mae_nominal'], f['max_abs_pct'], f['max_abs_min'], f['max_abs_max'], f['max_abs_nominal']))
if sp: print('side profile (report-only): joint %.2f (prior p95 %.1f), back z mean %+.1f min %+.1f, front z mean %+.1f, dome_round z %+.1f, depth %.3f H' % (sp['joint'], sp['p95'], sp['back_mean_z'], sp['back_min_z'], sp['front_mean_z'], sp['dome_round_z'], sp['depth_H']))
for k, v in out['criteria'].items():
    print('%-22s %-10s %s %-6s %s %s' % (k, ('%.3f' % v['value']) if isinstance(v['value'], float) else v['value'], v['op'], v['threshold'], 'PASS' if v['pass'] else 'FAIL', ('delta %+.3f' % v['delta']) if 'delta' in v else ''))
