"""clay sheet: python r6_sheet.py out.png label=basis.npy ... (rows = candidates, cols = front/q34/side)"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, subprocess, numpy as np
from PIL import Image, ImageDraw
from r6_common import *
t = Tpl(); out = sys.argv[1]; items = [a.split('=', 1) for a in sys.argv[2:]]; fs = []
for i, (lab, p) in enumerate(items):
    np.savez(FIT + f'_sh_{i}.npz', v=np.load(p).astype(float), f=t.faces); fs.append(FIT + f'_sh_{i}.npz')
subprocess.run([BLENDER, '-b', '-P', REPO + 'Art/Blender/tools/experimental/face_template/r6_07_sheet_blender.py', '--', FIT + '_shc'] + fs, capture_output=True)
W = 360; im = Image.new('RGB', (W * 3, W * len(items)), (30, 30, 30)); dr = ImageDraw.Draw(im)
for i, (lab, p) in enumerate(items):
    for j, tg in enumerate(('front', 'q34', 'side')):
        c = Image.open(FIT + f'_shc_{i}_{tg}.png').convert('RGB'); c = c.crop((100, 40, 700, 800)).resize((W, int(W * 760 / 600))).crop((0, 0, W, W)); im.paste(c, (j * W, i * W))
    dr.text((6, i * W + 4), lab, fill=(255, 220, 120))
im.save(out)
