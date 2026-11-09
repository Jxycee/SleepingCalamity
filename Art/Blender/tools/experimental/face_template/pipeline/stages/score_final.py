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
crit = {
 'ratio_mae_pct': (f['mae_pct'], th['ratio_mae_max'], '<='), 'ratio_worst_pct': (f['max_abs_pct'], th['ratio_worst_max'], '<='),
 'iou_head_jaw_rc': (f['iou_head_jaw_rowcentred'], th['iou_head_jaw_min'], '>='), 'iou_skull_rc': (f['iou_skull_rowcentred'], th['iou_skull_min'], '>='),
 'skin_rms_3d_mm': (skin_rms, th['skin_rms_3d_max'], '<='), 'skin_dE76': (sk.get('mean_deltaE76'), th['skin_dE76_max'], '<='),
 'flips': (rep.get('flips'), 0, '<='), 'invariants_build_ok': (1 if inv_ok else 0, 1, '>='), 'head_keys': (keys, th.get('head_keys_min', 184), '>=')}
out = dict(name=cfg['name'], final_tag=tag, final=f, ratio_jitter={k: f.get(k) for k in ('mae_nominal', 'mae_min', 'mae_max', 'max_abs_nominal', 'max_abs_min', 'max_abs_max', 'jitter_n', 'err_pct_median')}, fit_stage=s.get('fit'), skin_Lab=sk, skin_rms_3d_mm=skin_rms, teeth=teeth, head_keys=keys, invariants_ok=inv_ok, gates=chk,
           criteria={k: dict(value=v[0], threshold=v[1], op=v[2], **{'pass': (v[0] is not None and (v[0] <= v[1] if v[2] == '<=' else v[0] >= v[1]))}) for k, v in crit.items()})
if cfg.get('compare_to'):
    o = rd(cfg['compare_to'], {})
    out['compare_to'] = cfg['compare_to']
    for k, v in out['criteria'].items():
        ref = (o.get('criteria') or {}).get(k, {}).get('value')
        if ref is not None and v['value'] is not None: v['ref'] = ref; v['delta'] = v['value'] - ref
json.dump(out, open(R + 'out/score.json', 'w'), indent=1, default=float)
print('ratio scoring = median over %s framings (+-1%% scale/shift): MAE %.2f [%.2f..%.2f] nominal %.2f | worst %.2f [%.2f..%.2f] nominal %.2f' % (f.get('jitter_n'), f['mae_pct'], f['mae_min'], f['mae_max'], f['mae_nominal'], f['max_abs_pct'], f['max_abs_min'], f['max_abs_max'], f['max_abs_nominal']))
for k, v in out['criteria'].items():
    print('%-22s %-10s %s %-6s %s %s' % (k, ('%.3f' % v['value']) if isinstance(v['value'], float) else v['value'], v['op'], v['threshold'], 'PASS' if v['pass'] else 'FAIL', ('delta %+.3f' % v['delta']) if 'delta' in v else ''))
