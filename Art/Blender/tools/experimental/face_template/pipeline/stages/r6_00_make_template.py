"""Round 6: extend the head shape keys of the template: append the 28 SC_ sliders (new_sliders.npz) + 6 eye sliders (eye_sliders6.npz) as real keys
(basis + delta) after the existing 150 (existing indices unchanged). -> fit6/tobin_head_template6.npz (all other arrays copied).  usage: r6_00_make_template.py"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import numpy as np
SL = _P_SLIDERS; OUTP = _P_FIT + 'tobin_head_template6.npz'
z = np.load(_P_TPL0, allow_pickle=True); a = {k: z[k] for k in z.files}
B = a['head/basis'].astype(np.float64); keys = a['head/keys']; names = list(a['head/key_names'])
n1 = np.load(SL + 'new_sliders.npz'); n2 = np.load(SL + 'eye_sliders6.npz')
assert np.abs(n1['basis'] - B).max() < 1e-6 and np.abs(n2['basis'] - B).max() < 1e-6
nn = [str(x) for x in n1['names']] + [str(x) for x in n2['names']]; dd = np.concatenate([n1['delta'], n2['delta']]).astype(np.float64)
assert not any(n in names for n in nn)
a['head/keys'] = np.concatenate([keys, (B[None] + dd).astype(keys.dtype)]); a['head/key_names'] = np.array(names + nn)
a['tpl/new_key_idx'] = np.arange(150, 150 + len(nn)); a['tpl/new_key_names'] = np.array(nn)
np.savez_compressed(OUTP, **a); print('keys', a['head/keys'].shape, nn)
