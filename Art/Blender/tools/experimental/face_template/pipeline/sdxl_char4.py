"""SDXL (RealVisXL V5) txt2img via ComfyUI 8190. usage: sdxl_concept.py out_prefix seed [n]"""
import json, sys, time, urllib.request, shutil
from pathlib import Path
HOST = "127.0.0.1:8190"
OUT = Path(r"C:\AI\stablegen\ComfyUI\output")
pos = ("front view studio portrait photo of a older man in his sixties, pale olive skin, completely shaved bald head, short grey beard stubble, deep smile lines, "
       "smooth scalp, brown eyes open looking straight at camera, bushy grey eyebrows, neutral closed mouth, relaxed face, ears fully visible, neck and shoulders visible, "
       "wearing a plain navy blue crew-neck t-shirt, head centered with wide empty margin above the head and on both sides, "
       "perfectly even soft frontal lighting, plain light grey seamless background, symmetrical, sharp focus, 85mm")
neg = "long hair, wig, hat, glasses, makeup, smile, open mouth, side view, tilted head, shadows, dramatic lighting, cartoon, painting, blurry, closed eyes"
prefix = sys.argv[1]; seed = int(sys.argv[2]); n = int(sys.argv[3]) if len(sys.argv) > 3 else 4
p = {
 "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "RealVisXL_V5.0_fp16.safetensors"}},
 "2": {"class_type": "CLIPTextEncode", "inputs": {"text": pos, "clip": ["1", 1]}},
 "3": {"class_type": "CLIPTextEncode", "inputs": {"text": neg, "clip": ["1", 1]}},
 "4": {"class_type": "EmptyLatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": n}},
 "5": {"class_type": "KSampler", "inputs": {"model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0],
       "seed": seed, "steps": 30, "cfg": 5.0, "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
 "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
 "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": prefix}},
}
req = urllib.request.Request(f"http://{HOST}/prompt", json.dumps({"prompt": p}).encode(), {"Content-Type": "application/json"})
pid = json.load(urllib.request.urlopen(req))["prompt_id"]
while True:
    h = json.load(urllib.request.urlopen(f"http://{HOST}/history/{pid}"))
    if pid in h:
        print(h[pid]["status"]["status_str"], json.dumps(h[pid]["outputs"])[:500]); break
    time.sleep(2)
