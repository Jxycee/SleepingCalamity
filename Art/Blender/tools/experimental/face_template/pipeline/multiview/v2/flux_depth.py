"""Experiment D: FLUX.1 Depth [dev] (NVFP4 full model) head view from a template depth guide. usage: flux_depth.py <out_dir> <tag> <depth.png> <seed> "<prompt>" [model]
ComfyUI native: UNETLoader + DualCLIPLoader(flux) + InstructPixToPixConditioning(depth as pixels) + FluxGuidance 10 + KSampler cfg 1 euler/simple 28 steps."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run, stage_input
def build(depth, prompt, seed, model="flux1-depth-dev-nvfp4.safetensors", steps=28, size=1024, guidance=10.0):
    wf = {"1": {"class_type": "UNETLoader", "inputs": {"unet_name": model, "weight_dtype": "default"}},
          "2": {"class_type": "DualCLIPLoader", "inputs": {"clip_name1": "t5xxl_fp8_e4m3fn_scaled.safetensors", "clip_name2": "clip_l.safetensors", "type": "flux", "device": "default"}},
          "3": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
          "4": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}},
          "5": {"class_type": "FluxGuidance", "inputs": {"conditioning": ["4", 0], "guidance": guidance}},
          "6": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["4", 0]}},
          "7": {"class_type": "LoadImage", "inputs": {"image": stage_input(depth)}},
          "8": {"class_type": "ImageScale", "inputs": {"image": ["7", 0], "upscale_method": "lanczos", "width": size, "height": size, "crop": "center"}},
          "9": {"class_type": "InstructPixToPixConditioning", "inputs": {"positive": ["5", 0], "negative": ["6", 0], "vae": ["3", 0], "pixels": ["8", 0]}},
          "10": {"class_type": "KSampler", "inputs": {"model": ["1", 0], "positive": ["9", 0], "negative": ["9", 1], "latent_image": ["9", 2], "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
          "11": {"class_type": "VAEDecode", "inputs": {"samples": ["10", 0], "vae": ["3", 0]}},
          "12": {"class_type": "SaveImage", "inputs": {"images": ["11", 0], "filename_prefix": "v2eval/fluxdepth"}}}
    return wf
if __name__ == "__main__":
    out, tag, depth, seed, prompt = sys.argv[1:6]; model = sys.argv[6] if len(sys.argv) > 6 else "flux1-depth-dev-nvfp4.safetensors"
    outs, dt = run(build(depth, prompt, int(seed), model), out, tag); print(outs, round(dt, 1))
