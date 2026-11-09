"""Generate bald-head SIDE-profile and 3/4 portraits (RealVisXL V5 via ComfyUI 8190) for the side-profile skull prior.
usage: python side_gen_faces.py [--n_side 150] [--n_q34 50] [--out C:/AI/test_out/heroes/vision_prior/side] [--seed0 9000]
Same recipe as gen_prior_faces.py (sex / age / ethnicity / beard variation, plain light backdrop, no head-shape words) but the pose is a pure side view
(half facing left, half facing right: side_prior.py mirrors them to one direction) or a three-quarter view. Resumable (skips existing files). Needs the
ComfyUI server (C:/AI/stablegen/start_comfy_8190.bat). Pose filtering happens in side_prior.py (SDXL does not always obey 'profile')."""
import json, sys, time, random, shutil, urllib.request, argparse
from pathlib import Path
HOST = "127.0.0.1:8190"; COMFY_OUT = Path(r"C:\AI\stablegen\ComfyUI\output")
AGES = ["in his twenties", "in his thirties", "in his forties", "in his fifties", "in his sixties", "in his seventies"]
ETH = [("Northern European", "fair skin"), ("Southern European", "olive skin"), ("East Asian", "light tan skin"), ("South Asian", "medium brown skin"),
       ("West African", "dark brown skin"), ("East African", "deep brown skin"), ("Middle Eastern", "tan olive skin"), ("Latino", "warm tan skin"),
       ("Southeast Asian", "golden tan skin"), ("Slavic", "pale skin"), ("Polynesian", "bronze skin"), ("Native American", "copper brown skin")]
SHIRT = ["plain navy blue crew-neck t-shirt", "plain grey crew-neck t-shirt", "plain white t-shirt", "plain dark green crew-neck sweater", "plain black t-shirt"]
NEG = ("long hair, hair, wig, hat, cap, glasses, makeup, open mouth, front view, facing the camera, looking at camera, two faces, shadows, dramatic lighting, cartoon, painting, "
       "blurry, cropped head, jewelry, earrings, headphones, hood, scarf, turtleneck, high collar")


def prompt(rng, view):
    sex = rng.choice(["man", "woman"]); age = rng.choice(AGES).replace("his", "her") if sex == "woman" else rng.choice(AGES)
    eth, skin = rng.choice(ETH); beard = (", short stubble beard" if sex == "man" and rng.random() < 0.35 else "")
    face = rng.choice(["left", "right"]); shirt = rng.choice(SHIRT)
    if view == 'side':
        pose = f"perfect side profile view studio portrait photo, the head seen exactly from the side, facing {face}, only one ear visible, nose in profile"
    else:
        pose = f"three-quarter view studio portrait photo, the head turned about 45 degrees to the {face}, one ear visible"
    pos = (f"{pose}, of a {eth} {sex} {age}, {skin}, completely shaved bald head, smooth scalp{beard}, neutral closed mouth, "
           f"ear fully visible, whole head and neck visible, wearing a {shirt}, head centered with wide empty margin above the head and on both sides, "
           "perfectly even soft lighting, plain light grey seamless background, sharp focus, 85mm")
    return pos, dict(view=view, sex=sex, age=age, eth=eth, skin=skin, beard=bool(beard), facing=face)


def wf(pos, seed, prefix, steps=26):
    return {"1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "RealVisXL_V5.0_fp16.safetensors"}},
            "2": {"class_type": "CLIPTextEncode", "inputs": {"text": pos, "clip": ["1", 1]}}, "3": {"class_type": "CLIPTextEncode", "inputs": {"text": NEG, "clip": ["1", 1]}},
            "4": {"class_type": "EmptyLatentImage", "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
            "5": {"class_type": "KSampler", "inputs": {"model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["4", 0], "seed": seed, "steps": steps, "cfg": 5.5,
                  "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
            "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}}, "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": prefix}}}


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('--n_side', type=int, default=150); ap.add_argument('--n_q34', type=int, default=50)
    ap.add_argument('--out', default='C:/AI/test_out/heroes/vision_prior/side'); ap.add_argument('--seed0', type=int, default=9000); a = ap.parse_args()
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    try: urllib.request.urlopen(f"http://{HOST}/system_stats", timeout=5)
    except Exception as e: sys.exit(f"ComfyUI not reachable on {HOST}: {e}")
    rng = random.Random(a.seed0); meta = {}; mp_ = out / 'meta.json'
    if mp_.exists(): meta = json.load(open(mp_))
    jobs = [('side', i) for i in range(a.n_side)] + [('q34', i) for i in range(a.n_q34)]
    for k, (view, i) in enumerate(jobs):
        pos, m = prompt(rng, view); name = f'{view}_{i:03d}.png'; seed = a.seed0 + k
        if (out / name).exists(): continue
        req = urllib.request.Request(f"http://{HOST}/prompt", json.dumps({"prompt": wf(pos, seed, f'sideprior_{view}_{i:03d}')}).encode(), {"Content-Type": "application/json"})
        pid = json.load(urllib.request.urlopen(req))["prompt_id"]
        while True:
            h = json.load(urllib.request.urlopen(f"http://{HOST}/history/{pid}"))
            if pid in h: break
            time.sleep(1.0)
        im = [x for o in h[pid]["outputs"].values() for x in o.get("images", [])]
        if not im: print('no image', name); continue
        shutil.copy(COMFY_OUT / im[0].get('subfolder', '') / im[0]['filename'], out / name)
        meta[name] = dict(m, seed=seed, prompt=pos); json.dump(meta, open(mp_, 'w'), indent=0)
        if k % 10 == 0: print(k, time.strftime('%H:%M:%S'), flush=True)
    print('done', len(list(out.glob('side_*.png'))), len(list(out.glob('q34_*.png'))))


if __name__ == '__main__':
    main()
