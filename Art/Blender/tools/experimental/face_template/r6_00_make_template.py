"""Round 6: extend the head shape keys of the template: append the 28 SC_ sliders (new_sliders.npz) + 6 eye sliders (eye_sliders6.npz) as real keys
(basis + delta) after the existing 150 (existing indices unchanged). -> fit6/tobin_head_template6.npz (all other arrays copied).  usage: r6_00_make_template.py"""
import numpy as np
SL = 'C:/AI/test_out/heroes/face_spike2/sliders/'; OUTP = 'C:/AI/test_out/heroes/face_spike2/fit6/tobin_head_template6.npz'
z = np.load('C:/AI/test_out/heroes/face_spike/template/tobin_head_template.npz', allow_pickle=True); a = {k: z[k] for k in z.files}
B = a['head/basis'].astype(np.float64); keys = a['head/keys']; names = list(a['head/key_names'])
n1 = np.load(SL + 'new_sliders.npz'); n2 = np.load(SL + 'eye_sliders6.npz')
assert np.abs(n1['basis'] - B).max() < 1e-6 and np.abs(n2['basis'] - B).max() < 1e-6
nn = [str(x) for x in n1['names']] + [str(x) for x in n2['names']]; dd = np.concatenate([n1['delta'], n2['delta']]).astype(np.float64)
assert not any(n in names for n in nn)
a['head/keys'] = np.concatenate([keys, (B[None] + dd).astype(keys.dtype)]); a['head/key_names'] = np.array(names + nn)
a['tpl/new_key_idx'] = np.arange(150, 150 + len(nn)); a['tpl/new_key_names'] = np.array(nn)
np.savez_compressed(OUTP, **a); print('keys', a['head/keys'].shape, nn)
