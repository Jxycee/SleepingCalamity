"""Experiment C: MV-Adapter i2mv SDXL (ComfyUI-MVAdapter @9d6d8af on 8191) from one front reference. The pinned node has no ig2mv adapter entry (geometry mode needs upstream scripts), so only i2mv runs.
usage: mvadapter_i2mv.py <out_dir> <tag> <front_rgba_or_rgb.png> <seed> "<prompt>" [ckpt=RealVisXL_V5.0_fp16.safetensors]
Reference is composited on mid grey, head scaled to 0.85 of the 768 canvas (MV-Adapter expects an object-centred, background-free input)."""
import sys, os, cv2, numpy as np
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run, stage_input, free
def prep(p, name):
    im = cv2.imread(p, cv2.IMREAD_UNCHANGED)
    if im.shape[2] == 4: a = im[..., 3] > 20; ys, xs = np.where(a); y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max(); c = im[y0:y1 + 1, x0:x1 + 1]
    else: c = np.dstack([im, np.full(im.shape[:2], 255, np.uint8)])
    s = 0.85 * 768 / max(c.shape[:2]); c = cv2.resize(c, None, fx=s, fy=s, interpolation=cv2.INTER_AREA); h, w = c.shape[:2]
    out = np.full((768, 768, 3), 127, np.uint8); oy, ox = (768 - h) // 2, (768 - w) // 2; al = c[..., 3:] / 255.
    out[oy:oy + h, ox:ox + w] = (c[..., :3] * al + 127 * (1 - al)).astype(np.uint8)
    d = f'C:/AI/test_out/heroes/v2_eval/_mvref_{name}.png'; cv2.imwrite(d, out); return d
def build(ref_name, prompt, seed, ckpt="RealVisXL_V5.0_fp16.safetensors", steps=40, cfg=3.0):
    return {"1": {"class_type": "LdmPipelineLoader", "inputs": {"ckpt_name": ckpt, "pipeline_name": "MVAdapterI2MVSDXLPipeline"}},
            "2": {"class_type": "DiffusersMVSchedulerLoader", "inputs": {"pipeline": ["1", 0], "scheduler_name": "DDPM", "shift_snr": True, "shift_mode": "interpolated", "shift_scale": 8.0}},
            "3": {"class_type": "DiffusersMVModelMakeup", "inputs": {"pipeline": ["1", 0], "scheduler": ["2", 0], "autoencoder": ["1", 1], "load_mvadapter": True, "adapter_path": "C:/AI/models_staging/mv_adapter", "adapter_name": "mvadapter_i2mv_sdxl.safetensors", "num_views": 6, "enable_vae_slicing": True, "enable_vae_tiling": False}},
            "4": {"class_type": "LoadImage", "inputs": {"image": ref_name}},
            "5": {"class_type": "ViewSelector", "inputs": {"front_view": True, "front_right_view": True, "right_view": True, "back_view": True, "left_view": True, "front_left_view": True}},
            "6": {"class_type": "DiffusersMVSampler", "inputs": {"pipeline": ["3", 0], "num_views": 6, "prompt": prompt, "negative_prompt": "watermark, ugly, deformed, noisy, blurry, low contrast", "width": 768, "height": 768, "steps": steps, "cfg": cfg, "seed": seed, "reference_image": ["4", 0], "azimuth_degrees": ["5", 0]}},
            "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "v2eval/mvad"}}}
if __name__ == "__main__":
    out, tag, front, seed, prompt = sys.argv[1:6]; ckpt = sys.argv[6] if len(sys.argv) > 6 else "RealVisXL_V5.0_fp16.safetensors"
    ref = stage_input(prep(front, tag), f"mvref_{tag}.png"); outs, dt = run(build(ref, prompt, int(seed), ckpt), out, tag); print(outs, round(dt, 1))
