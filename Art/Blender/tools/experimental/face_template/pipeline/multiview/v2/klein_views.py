"""FLUX.2 klein multi-reference edit -> new head view. Usage:
 klein_views.py <model.safetensors> <te.safetensors> <out_dir> <tag> <seed> <prompt> <ref1.png> [ref2.png ...]
Refs: first = identity photo; optional further refs (e.g. depth render of target view)."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run, stage_input

def build(model, te, prompt, refs, seed, steps=4, size=1024, cfg=1.0, neg=""):
    wf = {}
    wf["1"] = {"class_type": "UNETLoader", "inputs": {"unet_name": model, "weight_dtype": "default"}}
    wf["2"] = {"class_type": "CLIPLoader", "inputs": {"clip_name": te, "type": "flux2", "device": "default"}}
    wf["3"] = {"class_type": "VAELoader", "inputs": {"vae_name": "flux2-vae.safetensors"}}
    wf["4"] = {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}}
    wf["5"] = {"class_type": "CLIPTextEncode", "inputs": {"text": neg, "clip": ["2", 0]}}
    pos, negc = ["4", 0], ["5", 0]
    n = 10
    for r in refs:
        li, sc, en = str(n), str(n + 1), str(n + 2)
        wf[li] = {"class_type": "LoadImage", "inputs": {"image": stage_input(r)}}
        wf[sc] = {"class_type": "ImageScaleToTotalPixels", "inputs": {"image": [li, 0], "upscale_method": "lanczos", "megapixels": 1.0, "resolution_steps": 16}}
        wf[en] = {"class_type": "VAEEncode", "inputs": {"pixels": [sc, 0], "vae": ["3", 0]}}
        a, b = str(n + 3), str(n + 4)
        wf[a] = {"class_type": "ReferenceLatent", "inputs": {"conditioning": pos, "latent": [en, 0]}}
        wf[b] = {"class_type": "ReferenceLatent", "inputs": {"conditioning": negc, "latent": [en, 0]}}
        pos, negc = [a, 0], [b, 0]
        n += 6
    wf["50"] = {"class_type": "EmptyFlux2LatentImage", "inputs": {"width": size, "height": size, "batch_size": 1}}
    wf["51"] = {"class_type": "Flux2Scheduler", "inputs": {"steps": steps, "width": size, "height": size}}
    wf["52"] = {"class_type": "KSamplerSelect", "inputs": {"sampler_name": "euler"}}
    wf["53"] = {"class_type": "RandomNoise", "inputs": {"noise_seed": seed}}
    wf["54"] = {"class_type": "CFGGuider", "inputs": {"model": ["1", 0], "positive": pos, "negative": negc, "cfg": cfg}}
    wf["55"] = {"class_type": "SamplerCustomAdvanced", "inputs": {"noise": ["53", 0], "guider": ["54", 0], "sampler": ["52", 0], "sigmas": ["51", 0], "latent_image": ["50", 0]}}
    wf["56"] = {"class_type": "VAEDecode", "inputs": {"samples": ["55", 0], "vae": ["3", 0]}}
    wf["57"] = {"class_type": "SaveImage", "inputs": {"images": ["56", 0], "filename_prefix": "v2eval/klein"}}
    return wf

if __name__ == "__main__":
    model, te, out, tag, seed, prompt = sys.argv[1:7]
    outs, dt = run(build(model, te, prompt, sys.argv[7:], int(seed)), out, tag)
    print(outs, round(dt, 1))
