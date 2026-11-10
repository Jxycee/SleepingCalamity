"""FLUX.1 Kontext dev (GGUF Q5_K_M via ComfyUI-GGUF) single-reference view edit. usage: kontext_views.py <out_dir> <tag> <seed> "<prompt>" <ref.png> [unet=flux1-kontext-dev-Q5_K_M.gguf]
Standard Kontext graph: FluxKontextImageScale -> VAEEncode -> ReferenceLatent -> FluxGuidance 2.5 -> KSampler euler/simple 20 steps cfg 1."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run, stage_input
def build(ref, prompt, seed, unet="flux1-kontext-dev-Q5_K_M.gguf", steps=20):
    loader = {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": unet}} if unet.endswith(".gguf") else {"class_type": "UNETLoader", "inputs": {"unet_name": unet, "weight_dtype": "default"}}
    return {"1": loader,
            "2": {"class_type": "DualCLIPLoader", "inputs": {"clip_name1": "t5xxl_fp8_e4m3fn_scaled.safetensors", "clip_name2": "clip_l.safetensors", "type": "flux", "device": "default"}},
            "3": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
            "4": {"class_type": "LoadImage", "inputs": {"image": stage_input(ref)}},
            "5": {"class_type": "FluxKontextImageScale", "inputs": {"image": ["4", 0]}},
            "6": {"class_type": "VAEEncode", "inputs": {"pixels": ["5", 0], "vae": ["3", 0]}},
            "7": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}},
            "8": {"class_type": "ReferenceLatent", "inputs": {"conditioning": ["7", 0], "latent": ["6", 0]}},
            "9": {"class_type": "FluxGuidance", "inputs": {"conditioning": ["8", 0], "guidance": 2.5}},
            "10": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["7", 0]}},
            "11": {"class_type": "KSampler", "inputs": {"model": ["1", 0], "positive": ["9", 0], "negative": ["10", 0], "latent_image": ["6", 0], "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
            "12": {"class_type": "VAEDecode", "inputs": {"samples": ["11", 0], "vae": ["3", 0]}},
            "13": {"class_type": "SaveImage", "inputs": {"images": ["12", 0], "filename_prefix": "v2eval/kontext"}}}
if __name__ == "__main__":
    out, tag, seed, prompt, ref = sys.argv[1:6]; unet = sys.argv[6] if len(sys.argv) > 6 else "flux1-kontext-dev-Q5_K_M.gguf"
    outs, dt = run(build(ref, prompt, int(seed), unet), out, tag); print(outs, round(dt, 1))
