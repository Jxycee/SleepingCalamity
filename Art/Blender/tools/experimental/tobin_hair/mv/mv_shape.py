# Hunyuan3D-2mv shape from the mv_cutouts.py views, keeping their shared frame (face pipeline's hy3d_mv.py re-normalises each view by
# its own bbox height, which would break the common scale). ComfyUI 8191 (C:/AI/comfy_new, start_8191.bat) must be running.
#   python mv_shape.py <cut_dir> <out_prefix> [--seed 1] [--octree 384] [--steps 30] [--cfg 5.5]
import sys, os, glob, shutil, cv2
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'face_template', 'pipeline', 'multiview', 'v2'))
from comfy_client import run, COMFY_ROOT          # noqa: E402
from hy3d_mv import build                          # noqa: E402
cut, out = sys.argv[1], sys.argv[2]
arg = lambda k, d, t=int: t(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
seed, octree, steps, cfg = arg('--seed', 1), arg('--octree', 384), arg('--steps', 30), arg('--cfg', 5.5, float)
names = []
for v in ('front', 'left', 'right', 'back'):
    im = cv2.resize(cv2.imread(os.path.join(cut, f'hair_{v}.png'), cv2.IMREAD_UNCHANGED), (518, 518), interpolation=cv2.INTER_AREA)
    n = f'hairmv_{os.path.basename(out)}_{v}.png'; cv2.imwrite(os.path.join(COMFY_ROOT, 'input', n), im); names.append(n)
res, dt = run(build(names, seed, octree, steps, cfg=cfg)); print('HY3D', round(dt, 1), 's')
g = sorted(glob.glob(os.path.join(COMFY_ROOT, 'output', '3D', 'v2eval', 'hymv*.glb')), key=os.path.getmtime)[-1]
os.makedirs(os.path.dirname(out) or '.', exist_ok=True); shutil.copy(g, out + '.glb'); print(out + '.glb')
