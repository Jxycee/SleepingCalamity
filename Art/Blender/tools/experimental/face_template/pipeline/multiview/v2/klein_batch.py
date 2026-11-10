"""Experiment A batch: 4 klein variants x 4 views from the mv1 front photo (+ depth-ref variant). Writes timings.json"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(__file__))
from klein_views import build
from comfy_client import run, free
OUT = sys.argv[1]; FRONT = sys.argv[2]; GUIDE = sys.argv[3]
BASE = "Photo of the exact same bald freckled woman, same studio grey background and grey t-shirt, same skin, freckles and eye colour, "
VIEWS = {
 "left": BASE + "seen from her left side in a pure profile view, camera at 90 degrees, ear visible",
 "right": BASE + "seen from her right side in a pure profile view, camera at 90 degrees, ear visible",
 "back": BASE + "seen from directly behind, back of the bald head and both ears visible",
 "q34l": BASE + "three-quarter view, head turned 40 degrees to her left, both eyes visible",
}
MODELS = [("kl4fp8", "flux-2-klein-4b-fp8.safetensors", "qwen_3_4b_fp4_flux2.safetensors"),
          ("kl4nv", "flux-2-klein-4b-nvfp4.safetensors", "qwen_3_4b_fp4_flux2.safetensors"),
          ("kl9nv", "flux-2-klein-9b-nvfp4.safetensors", "qwen_3_8b_fp4mixed.safetensors"),
          ("kl9fp8", "flux-2-klein-9b-fp8.safetensors", "qwen_3_8b_fp4mixed.safetensors")]
res = {}
for tag, m, te in MODELS:
    free()
    for v, p in VIEWS.items():
        try:
            outs, dt = run(build(m, te, p, [FRONT], 1), OUT, f"{tag}_{v}")
            res[f"{tag}_{v}"] = {"file": outs[0], "s": round(dt, 1)}
        except Exception as e:
            res[f"{tag}_{v}"] = {"err": str(e)[:300]}
        print(tag, v, res[f"{tag}_{v}"], flush=True)
    # depth-reference variant, left view
    try:
        outs, dt = run(build(m, te, "Image 1 is the identity photo. Image 2 is a depth map of the target head pose. " + VIEWS["left"], [FRONT, GUIDE + "/depth_left.png"], 1), OUT, f"{tag}_leftdepth")
        res[f"{tag}_leftdepth"] = {"file": outs[0], "s": round(dt, 1)}
    except Exception as e:
        res[f"{tag}_leftdepth"] = {"err": str(e)[:300]}
    print(tag, "leftdepth", res[f"{tag}_leftdepth"], flush=True)
json.dump(res, open(os.path.join(OUT, "timings.json"), "w"), indent=1)
