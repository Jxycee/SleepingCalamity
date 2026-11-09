"""Copy the round-2 inputs that round 3 reuses into fit3/ (small files only)."""
import shutil, os
from r3_common import *
for f in ('target_head.npz', 'target_aligned_full.npz', 'target_head_clean.npz', 'align.json', 'tgt_hits.json', 'tpl_hits.json', 'slider_verts.npy', 'slider_verts_noeye.npy',
          'slider_coeffs_noeye.json', 'slider_coeffs.json', 'eye_shift.npz', 'tpl_to_tgt_landmarks.npz', 'lm_ids.json', 'tpl_cam.json', 'tgt_cam.json'):
    if os.path.exists(FIT2 + f): shutil.copy(FIT2 + f, FIT + f)
print(sorted(os.listdir(FIT)))
