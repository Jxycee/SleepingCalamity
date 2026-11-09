"""clay sheet: python r5_sheet.py out.png label=basis.npy ... (rows = candidates, cols = front/q34/side)"""
import sys, subprocess, numpy as np
from PIL import Image, ImageDraw
from r5_common import *
t = Tpl(); out = sys.argv[1]; items = [a.split('=', 1) for a in sys.argv[2:]]; fs = []
for i, (lab, p) in enumerate(items):
    np.savez(FIT + f'_sh_{i}.npz', v=np.load(p).astype(float), f=t.faces); fs.append(FIT + f'_sh_{i}.npz')
subprocess.run([BLENDER, '-b', '-P', REPO + 'Art/Blender/tools/experimental/face_template/r5_07_sheet_blender.py', '--', FIT + '_shc'] + fs, capture_output=True)
W = 360; im = Image.new('RGB', (W * 3, W * len(items)), (30, 30, 30)); dr = ImageDraw.Draw(im)
for i, (lab, p) in enumerate(items):
    for j, tg in enumerate(('front', 'q34', 'side')):
        c = Image.open(FIT + f'_shc_{i}_{tg}.png').convert('RGB'); c = c.crop((100, 40, 700, 800)).resize((W, int(W * 760 / 600))).crop((0, 0, W, W)); im.paste(c, (j * W, i * W))
    dr.text((6, i * W + 4), lab, fill=(255, 220, 120))
im.save(out)
