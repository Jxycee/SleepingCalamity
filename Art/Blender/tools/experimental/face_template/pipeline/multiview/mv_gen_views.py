"""Generate a multi-view bald-head turnaround with SDXL (RealVisXL V5) + ControlNet-depth on ComfyUI 8190.
usage (facekeys or trellis2 python):  python mv_gen_views.py <guide_dir> <out_dir> --mode grid|frontfirst [--seed N] [--cn 0.55] [--cn_end 0.55] [--prompt-id woman1]
guide_dir = output of mv_depth_views.py (depth_<view>.png, 576 px tiles).
mode grid: ONE canvas 3x2 tiles (front, q34l, left / right, q34r, back); the tiled depth maps are the ControlNet input, so all views share attention (identity).
mode frontfirst: front view alone (seed picks), then every other view alone with IP-Adapter-plus on the chosen front (weight --ipw; 0.25 for back).
Writes <out_dir>/<tag>_grid.png (grid mode) and <tag>_<view>.png (cropped tiles)."""
import json, sys, time, shutil, argparse, urllib.request, urllib.error
from pathlib import Path
import cv2, numpy as np

HOST = "127.0.0.1:8190"; COMFY = Path(r"C:\AI\stablegen\ComfyUI")
GRID = [['front', 'q34l', 'left'], ['right', 'q34r', 'back']]
SUBJECTS = {
    'woman1': "a middle-aged woman in her late forties with light freckled skin, many freckles across nose cheeks and forehead, high cheekbones, soft jaw, thin lips, light green eyes, subtle smile lines",
}
NEG = ("hair, wig, hat, cap, glasses, makeup, open mouth, smiling teeth, shadows, dramatic lighting, cartoon, painting, illustration, 3d render, blurry, jewelry, earrings, "
       "headphones, hood, scarf, text, watermark, collage of different people, two different people, extra faces, deformed ears")


def post(wf):
    req = urllib.request.Request(f"http://{HOST}/prompt", json.dumps({"prompt": wf}).encode(), {"Content-Type": "application/json"})
    try: pid = json.load(urllib.request.urlopen(req))["prompt_id"]
    except urllib.error.HTTPError as e: sys.exit(e.read().decode()[:3000])
    t0 = time.time()
    while time.time() - t0 < 900:
        h = json.load(urllib.request.urlopen(f"http://{HOST}/history/{pid}"))
        if pid in h: break
        time.sleep(1.5)
    else: sys.exit('timeout')
    r = h[pid]
    if r['status']['status_str'] != 'success': sys.exit(json.dumps(r['status']['messages'])[:3000])
    im = [x for o in r['outputs'].values() for x in o.get('images', [])]
    return COMFY / 'output' / im[0].get('subfolder', '') / im[0]['filename']


def workflow(pos, depth_name, w, h, seed, cn, cn_end, prefix, ip_name=None, ipw=0.0, steps=30, cfg=5.5):
    wf = {"1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "RealVisXL_V5.0_fp16.safetensors"}},
          "2": {"class_type": "CLIPTextEncode", "inputs": {"text": pos, "clip": ["1", 1]}},
          "3": {"class_type": "CLIPTextEncode", "inputs": {"text": NEG, "clip": ["1", 1]}},
          "4": {"class_type": "EmptyLatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
          "10": {"class_type": "LoadImage", "inputs": {"image": depth_name}},
          "11": {"class_type": "ControlNetLoader", "inputs": {"control_net_name": "controlnet_depth_sdxl.safetensors"}},
          "12": {"class_type": "ControlNetApplyAdvanced", "inputs": {"positive": ["2", 0], "negative": ["3", 0], "control_net": ["11", 0], "image": ["10", 0], "strength": cn,
                 "start_percent": 0.0, "end_percent": cn_end, "vae": ["1", 2]}},
          "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
          "7": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": prefix}}}
    model = ["1", 0]
    if ip_name:
        wf["20"] = {"class_type": "IPAdapterModelLoader", "inputs": {"ipadapter_file": "ip-adapter-plus_sdxl_vit-h.safetensors"}}
        wf["21"] = {"class_type": "CLIPVisionLoader", "inputs": {"clip_name": "CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors"}}
        wf["22"] = {"class_type": "LoadImage", "inputs": {"image": ip_name}}
        wf["23"] = {"class_type": "IPAdapterAdvanced", "inputs": {"model": ["1", 0], "ipadapter": ["20", 0], "image": ["22", 0], "clip_vision": ["21", 0], "weight": ipw,
                    "weight_type": "linear", "combine_embeds": "concat", "start_at": 0.0, "end_at": 0.8, "embeds_scaling": "V only"}}
        model = ["23", 0]
    wf["5"] = {"class_type": "KSampler", "inputs": {"model": model, "positive": ["12", 0], "negative": ["12", 1], "latent_image": ["4", 0], "seed": seed, "steps": steps, "cfg": cfg,
               "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}}
    return wf


def stage(path):
    dst = COMFY / 'input' / Path(path).name; shutil.copy(path, dst); return dst.name


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('guide'); ap.add_argument('out'); ap.add_argument('--mode', default='grid'); ap.add_argument('--seed', type=int, default=11)
    ap.add_argument('--cn', type=float, default=0.55); ap.add_argument('--cn_end', type=float, default=0.55); ap.add_argument('--subject', default='woman1'); ap.add_argument('--tag', default='g1')
    ap.add_argument('--ipw', type=float, default=0.6); ap.add_argument('--front', default=None, help='front png used as IP-Adapter reference (frontfirst mode)')
    ap.add_argument('--views', default='front,q34l,left,right,q34r,back'); a = ap.parse_args()
    g, out = Path(a.guide), Path(a.out); out.mkdir(parents=True, exist_ok=True); subj = SUBJECTS[a.subject]
    style = ("bald, completely shaved smooth scalp, neutral relaxed closed mouth, plain grey crew-neck t-shirt, plain neutral mid-grey seamless background, perfectly even soft diffuse studio lighting, "
             "no shadows, sharp focus, photorealistic skin texture, natural pores")
    if a.mode == 'grid':
        T = cv2.imread(str(g / 'depth_front.png'), 0).shape[0]
        rows = [np.hstack([cv2.imread(str(g / f'depth_{v}.png'), 0) for v in r]) for r in GRID]; canvas = np.vstack(rows)
        cp = out / f'{a.tag}_guide_canvas.png'; cv2.imwrite(str(cp), canvas); name = stage(cp)
        pos = ("character turnaround reference sheet of the same head, six photographs of one person arranged in a 3 by 2 grid: front view, three-quarter view, left profile view / "
               f"right profile view, three-quarter view, back of the head view, studio photographs of {subj}, {style}")
        p = post(workflow(pos, name, canvas.shape[1], canvas.shape[0], a.seed, a.cn, a.cn_end, f'mv1_{a.tag}'))
        im = cv2.imread(str(p)); cv2.imwrite(str(out / f'{a.tag}_grid.png'), im)
        for r, row in enumerate(GRID):
            for c, v in enumerate(row): cv2.imwrite(str(out / f'{a.tag}_{v}.png'), im[r * T:(r + 1) * T, c * T:(c + 1) * T])
        print('grid done', p)
    else:
        T = 576; size = 1024
        view_txt = dict(front='front view, looking straight into the camera', q34l="three-quarter view, the head turned 40 degrees showing the subject's right cheek",
                        q34r="three-quarter view, the head turned 40 degrees showing the subject's left cheek", left='exact side profile view of the left side of the head, one ear visible, nose in profile',
                        right='exact side profile view of the right side of the head, one ear visible, nose in profile', back='view from directly behind, the back of the head and both ears, no face visible')
        ref = stage(a.front) if a.front else None
        for v in a.views.split(','):
            d = cv2.resize(cv2.imread(str(g / f'depth_{v}.png'), 0), (size, size), interpolation=cv2.INTER_LINEAR); dp = out / f'{a.tag}_depth_{v}.png'; cv2.imwrite(str(dp), d)
            pos = f"studio portrait photograph, {view_txt[v]}, of {subj}, {style}"
            use_ip = ref is not None and v != 'front'; ipw = a.ipw * (0.4 if v == 'back' else 1.0)
            p = post(workflow(pos, stage(dp), size, size, a.seed, a.cn, a.cn_end, f'mv1_{a.tag}_{v}', ip_name=ref if use_ip else None, ipw=ipw))
            shutil.copy(p, out / f'{a.tag}_{v}.png'); print(v, 'done', flush=True)
            if v == 'front' and ref is None: ref = stage(out / f'{a.tag}_front.png')


if __name__ == '__main__':
    main()
