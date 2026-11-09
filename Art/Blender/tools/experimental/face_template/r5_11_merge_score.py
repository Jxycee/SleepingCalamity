"""Merge the r2/r4/r5 numbers into out5/score.json (venv python). Run r5_10_score.py (facekeys env) first: r5_10_score.py r2=..rn_r2.png r4=..rn_a2.png r5=..rn_f0.png --tex bake5/texture.json  (writes fit5/score.json)."""
import json, sys
from r5_common import *
import r5_metrics as MT
sc = json.load(open(FIT + 'score.json'))
bases = {'r2': FIT2 + 'wrap_r3_basis.npy', 'r4': D2 + 'fit4/wrap_ra2_basis.npy', 'r5': FIT + f'wrap_r{FINAL_TAG}_basis.npy'}
for k, p in bases.items():
    if k in sc: sc[k]['target3d'] = MT.tgt_stats(np.load(p).astype(float))
sc['skin_Lab_r4'] = json.load(open(D2 + 'out4/score.json')).get('skin_Lab')
tc = json.load(open(B4 + "teeth_counts.json")); sc['teeth'] = dict(template=tc['template'], r5_nominal=tc['nominal'], r5_final=tc.get('final'))
try: sc['teeth']['r4_final'] = json.load(open(D2 + 'bake4/teeth_counts.json')).get('final')
except Exception: pass
json.dump(sc, open(O4 + 'score.json', 'w'), indent=1, default=float)
for k in ('r2', 'r4', 'r5'):
    v = sc[k]; print(k, 'MAE %.2f' % v['mae_pct'], 'IoU hj %.3f skull %.3f rc %.3f/%.3f' % (v['iou_head_jaw'], v['iou_skull'], v['iou_head_jaw_rowcentred'], v['iou_skull_rowcentred']), 'skin_rms', v['target3d']['skin_rms'], 'nose', v['target3d']['nose_rms'], 'lips', v['target3d']['lips_rms'], 'brow', v['target3d']['brow_rms'])
print('skin dE r5', sc.get('skin_Lab', {}).get('mean_deltaE76'), 'r4', (sc['skin_Lab_r4'] or {}).get('mean_deltaE76')); print('teeth', sc['teeth'])
