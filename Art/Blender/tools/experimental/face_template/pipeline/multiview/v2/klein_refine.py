"""Klein as delighter / face-detail refiner (9B fp8 default). usage: klein_refine.py <out_dir> <tag> <mode delight|refine> <image.png> [seed]
delight: studio photo -> flat even albedo-like lighting (texture source); refine: low-detail textured render -> photoreal skin keeping the identity and pose."""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
from comfy_client import run
from klein_views import build
PROMPTS = {"delight": "Same person, same pose, same framing. Remove all lighting: flat, even, shadowless diffuse illumination, no highlights, no specular shine, no cast shadows, matte skin albedo texture, keep every freckle and skin detail and the eye colour exactly.",
           "refine": "Same person, same pose, same framing and head shape. Turn this rough 3D render into a photograph: realistic skin pores, fine freckles, natural lips and eyes with catchlights, soft studio light. Do not change the face geometry."}
if __name__ == "__main__":
    out, tag, mode, img = sys.argv[1:5]; seed = int(sys.argv[5]) if len(sys.argv) > 5 else 1
    outs, dt = run(build("flux-2-klein-9b-fp8.safetensors", "qwen_3_8b_fp4mixed.safetensors", PROMPTS[mode], [img], seed, size=1024), out, tag); print(outs, round(dt, 1))
