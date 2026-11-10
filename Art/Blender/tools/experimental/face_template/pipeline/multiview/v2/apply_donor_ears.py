"""Post-snap step used for mv1_v2: swap in donor ears (from a single-photo slider fit) onto a finished multiview snap result, keeping the skull-source face.
usage: apply_donor_ears.py <template.npz> <donor_rh6_basis.npy> <run_dir/fit>   (needs piece_merge.py beside it; backs up wrap_rh6_{basis,keys}.npy as *_pre.npy once)
Then continue the pipeline: face_pipeline.py run --config <cfg> (key_transfer ... sheet). Keys are shifted by the same per-vertex delta as the basis (as r7 does)."""
import sys, os, shutil, subprocess, numpy as np
tpl, donor, fit = sys.argv[1:4]; here = os.path.dirname(os.path.abspath(__file__))
for n in ('basis', 'keys'):
    pre = f'{fit}/wrap_rh6_{n}_pre.npy'
    if not os.path.exists(pre): shutil.copy(f'{fit}/wrap_rh6_{n}.npy', pre)
subprocess.check_call([sys.executable, f'{here}/piece_merge.py', tpl, donor, f'{fit}/wrap_rh6_basis_pre.npy', f'{fit}/ears_only.npy', '--noface', '--earfade', '18'])
b = np.load(f'{fit}/wrap_rh6_basis_pre.npy').astype(np.float64); m = np.load(f'{fit}/ears_only.npy').astype(np.float64); k = np.load(f'{fit}/wrap_rh6_keys_pre.npy').astype(np.float64)
nk = (k + (m - b)[None]).astype(np.float32); nk[0] = m.astype(np.float32); np.save(f'{fit}/wrap_rh6_keys.npy', nk); np.save(f'{fit}/wrap_rh6_basis.npy', m.astype(np.float32)); print('donor ears applied')
