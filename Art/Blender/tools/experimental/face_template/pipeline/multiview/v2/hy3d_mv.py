"""Hunyuan3D-2mv shape (kijai wrapper on ComfyUI 8191) from front/left/right/back RGBA cutouts.
usage: python hy3d_mv.py <out_prefix> <seed> <front.png> <left.png> <right.png> <back.png> [--octree 384] [--steps 30]
Writes <out_prefix>.glb (largest-piece cleaning is done by the evaluation scripts) and prints seconds. Images are padded to 518 square RGBA first."""
import sys, os, cv2, numpy as np, shutil
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run, stage_input, COMFY_ROOT

def prep(p, tag):
    im = cv2.imread(p, cv2.IMREAD_UNCHANGED); a = im[..., 3] > 20; ys, xs = np.where(a)
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max(); s = 0.9 * 518 / (y1 - y0)   # height-normalised (shared neck cut row), not per-view bbox
    crop = cv2.resize(im[y0:y1 + 1, x0:x1 + 1], None, fx=s, fy=s, interpolation=cv2.INTER_AREA)
    out = np.zeros((518, 518, 4), np.uint8); h, w = crop.shape[:2]; oy, ox = (518 - h) // 2, (518 - w) // 2; out[oy:oy + h, ox:ox + w] = crop
    d = os.path.join(COMFY_ROOT, 'input', f'hy_{tag}.png'); cv2.imwrite(d, out); return f'hy_{tag}.png'

def build(names, seed, octree, steps, model='hunyuan3d-dit-v2-mv-fp16.safetensors', cfg=5.5):
    wf = {'1': {'class_type': 'Hy3DModelLoader', 'inputs': {'model': model, 'attention_mode': 'sdpa', 'cublas_ops': False}}}
    for i, (k, n) in enumerate(zip(['front', 'left', 'right', 'back'], names)):
        b = 10 + i * 4
        wf[str(b)] = {'class_type': 'LoadImage', 'inputs': {'image': n}}
        # LoadImage mask = 1 where transparent, and JoinImageWithAlpha expects exactly that convention: NO InvertMask (an extra invert gives a solid box)
        wf[str(b + 2)] = {'class_type': 'JoinImageWithAlpha', 'inputs': {'image': [str(b), 0], 'alpha': [str(b), 1]}}
    wf['30'] = {'class_type': 'Hy3DGenerateMeshMultiView', 'inputs': {'pipeline': ['1', 0], 'guidance_scale': cfg, 'steps': steps, 'seed': seed,
               'front': ['12', 0], 'left': ['16', 0], 'right': ['20', 0], 'back': ['24', 0], 'scheduler': 'FlowMatchEulerDiscreteScheduler'}}
    wf['31'] = {'class_type': 'Hy3DVAEDecode', 'inputs': {'vae': ['1', 1], 'latents': ['30', 0], 'box_v': 1.01, 'octree_resolution': octree, 'num_chunks': 32000, 'mc_level': 0.0, 'mc_algo': 'mc', 'enable_flash_vdm': True, 'force_offload': True}}
    wf['32'] = {'class_type': 'Hy3DExportMesh', 'inputs': {'trimesh': ['31', 0], 'filename_prefix': '3D/v2eval/hymv', 'file_format': 'glb', 'save_file': True}}
    return wf

if __name__ == '__main__':
    out, seed = sys.argv[1], int(sys.argv[2]); files = sys.argv[3:7]
    octree = int(sys.argv[sys.argv.index('--octree') + 1]) if '--octree' in sys.argv else 384
    steps = int(sys.argv[sys.argv.index('--steps') + 1]) if '--steps' in sys.argv else 30
    names = [prep(f, f'{os.path.basename(out)}_{k}') for f, k in zip(files, ['f', 'l', 'r', 'b'])]
    res, dt = run(build(names, seed, octree, steps)); print(res, round(dt, 1))
    # export node returns a path string, find the newest glb
    import glob
    g = sorted(glob.glob(os.path.join(COMFY_ROOT, 'output', '3D', 'v2eval', 'hymv*.glb')), key=os.path.getmtime)[-1]
    shutil.copy(g, out + '.glb'); print(out + '.glb')
