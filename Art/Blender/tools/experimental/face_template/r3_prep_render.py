"""usage: mesh.npz out.npz  -> adds the template eyeballs (for the front-ortho Workbench render)"""
import numpy as np, sys
z = np.load('C:/AI/test_out/heroes/face_spike/template/tobin_head_template.npz'); d = np.load(sys.argv[1])
ev = np.concatenate([z['eyeL/basis'], z['eyeR/basis']]); ef = np.concatenate([z['eyeL/faces'], z['eyeR/faces'] + 512])
if 'ev' in d.files: ev, ef = d['ev'], d['ef']
np.savez(sys.argv[2], v=d['v'].astype(float), f=d['f'], ev=ev, ef=ef)
