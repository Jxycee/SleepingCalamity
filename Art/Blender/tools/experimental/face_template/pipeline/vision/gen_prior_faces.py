"""Generate N frontal bald-head portraits (RealVisXL V5 via ComfyUI 8190) for the human-proportion prior.
usage: python gen_prior_faces.py [--n 200] [--out C:/AI/test_out/heroes/vision_prior/faces] [--seed0 5000]
Bald / shaved heads on a plain light backdrop (the pipeline's own photo type, see ../sdxl_char4.py) so the head
silhouette (crown, dome width) can be measured as well as the MediaPipe landmarks. Prompts vary sex, age,
ethnicity/skin tone, beard, and head shape words are deliberately NOT used (they would bias the prior).
Resumable: skips files that exist. Checks the server and refuses to start if the GPU is busy (>40 % util)."""
import json, sys, time, random, shutil, subprocess, urllib.request, argparse
from pathlib import Path
HOST = "127.0.0.1:8190"; COMFY_OUT = Path(r"C:\AI\stablegen\ComfyUI\output")
SEX = [("man", 0.5), ("woman", 0.5)]
AGES = ["in his twenties", "in his thirties", "in his forties", "in his fifties", "in his sixties", "in his seventies"]
ETH = [("Northern European", "fair skin"), ("Southern European", "olive skin"), ("East Asian", "light tan skin"), ("South Asian", "medium brown skin"),
       ("West African", "dark brown skin"), ("East African", "deep brown skin"), ("Middle Eastern", "tan olive skin"), ("Latino", "warm tan skin"),
       ("Southeast Asian", "golden tan skin"), ("Slavic", "pale skin"), ("Polynesian", "bronze skin"), ("Native American", "copper brown skin")]
EYES = ["brown", "dark brown", "hazel", "green", "blue", "grey"]
NEG = "long hair, hair, wig, hat, cap, glasses, makeup, open mouth, side view, tilted head, head turned, shadows, dramatic lighting, cartoon, painting, blurry, closed eyes, cropped head, jewelry, earrings"
def prompt(rng):
    sex = rng.choice(["man", "woman"]); age = rng.choice(AGES).replace("his", "her") if sex == "woman" else rng.choice(AGES)
    eth, skin = rng.choice(ETH); eyes = rng.choice(EYES)
    beard = (", short stubble beard" if sex == "man" and rng.random() < 0.35 else "")
    expr = rng.choice(["neutral closed mouth", "neutral closed mouth, relaxed face", "faint calm smile, lips closed"])
    shirt = rng.choice(["plain navy blue crew-neck t-shirt", "plain grey crew-neck t-shirt", "plain white t-shirt", "plain dark green crew-neck sweater", "plain black t-shirt"])
    pos = (f"front view studio portrait photo of a {eth} {sex} {age}, {skin}, completely shaved bald head, smooth scalp{beard}, {eyes} eyes open looking straight at camera, "
           f"{expr}, ears fully visible, neck and shoulders visible, wearing a {shirt}, head centered with wide empty margin above the head and on both sides, "
           "perfectly even soft frontal lighting, plain light grey seamless background, symmetrical, sharp focus, 85mm")
    return pos, dict(sex=sex, age=age, eth=eth, skin=skin, beard=bool(beard))
def wf(pos, seed, prefix, steps=26):
    return {"1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "RealVisXL_V5.0_fp16.safetensors"}},
            "2": {"class_type": "CLIPTextEncode", "inputs": {"text": pos, "clip": ["1", 1]}}, "3": {"class_type": "CLIPTextEncode", "inputs": {"text": NEG, "clip": ["1", 1]}},
            "4": {"class_type": "EmptyLatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
            "5": {"class_type": "KSampler", "inputs": {"model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0], "seed": seed, "steps": steps, "cfg": 5.0,
                  "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
            "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}}, "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": prefix}}}
def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--n', type=int, default=200); ap.add_argument('--out', default='C:/AI/test_out/heroes/vision_prior/faces'); ap.add_argument('--seed0', type=int, default=5000)
    ap.add_argument('--force_gpu', action='store_true'); a = ap.parse_args()
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    try: urllib.request.urlopen(f"http://{HOST}/system_stats", timeout=5)
    except Exception as e: sys.exit(f"ComfyUI not reachable on {HOST}: {e}")
    try:
        u = int(subprocess.check_output(['nvidia-smi', '--query-gpu=utilization.gpu', '--format=csv,noheader,nounits']).decode().split()[0])
        if u > 40 and not a.force_gpu: sys.exit(f"GPU busy ({u} %), pass --force_gpu to run anyway")
    except FileNotFoundError: pass
    rng = random.Random(a.seed0); meta = {}
    mp_ = out / 'meta.json'
    if mp_.exists(): meta = json.load(open(mp_))
    for i in range(a.n):
        pos, m = prompt(rng); name = f'face_{i:03d}.png'; seed = a.seed0 + i
        if (out / name).exists(): continue
        pre = f'vprior_{i:03d}'; req = urllib.request.Request(f"http://{HOST}/prompt", json.dumps({"prompt": wf(pos, seed, pre)}).encode(), {"Content-Type": "application/json"})
        pid = json.load(urllib.request.urlopen(req))["prompt_id"]
        while True:
            h = json.load(urllib.request.urlopen(f"http://{HOST}/history/{pid}"))
            if pid in h: break
            time.sleep(1.5)
        im = [x for o in h[pid]["outputs"].values() for x in o.get("images", [])]
        if not im: print('no image', i); continue
        shutil.copy(COMFY_OUT / im[0].get('subfolder', '') / im[0]['filename'], out / name)
        meta[name] = dict(m, seed=seed, prompt=pos); json.dump(meta, open(mp_, 'w'), indent=0)
        if i % 10 == 0: print(i, time.strftime('%H:%M:%S'), flush=True)
    print('done', len(list(out.glob('face_*.png'))))
main()
