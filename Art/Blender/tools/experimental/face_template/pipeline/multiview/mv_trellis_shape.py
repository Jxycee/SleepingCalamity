"""TRELLIS.2 multi-view SHAPE generation (front/back/left/right cutouts -> head+neck mesh GLB) through ComfyUI 8188.
usage (any python): python mv_trellis_shape.py <api_raw.json> <prefix> <front.png> <back.png> <left.png> <right.png> [--seed 12345] [--views fb|fblr|f]
Images must already be in C:/AI/trellis2/ComfyUI/input (mv_make_cutouts.py writes them there). api_raw.json = ui2api conversion of MeshOnly_MultiView.json (C:/AI/scripts/ui2api.py);
only its mesh decode/export tail (nodes 1, 10-17, 22, 23) is reused, the multi-view generator nodes are wired here because ui2api mis-maps the seed widget.
Left/right: left_image = view of the subject's LEFT side (camera at subject's left), right_image likewise (run_tex_mv.py notes: side image = model's right). Prints seconds and peak GPU memory."""
import json, sys, time, subprocess, urllib.request, argparse
ap = argparse.ArgumentParser(); ap.add_argument('raw'); ap.add_argument('prefix'); ap.add_argument('front'); ap.add_argument('back'); ap.add_argument('left'); ap.add_argument('right')
ap.add_argument('--seed', type=int, default=12345); ap.add_argument('--views', default='fblr'); ap.add_argument('--steps', type=int, default=12); a = ap.parse_args()
raw = json.load(open(a.raw)); p = {k: raw[k] for k in ('1', '10', '11', '12', '13', '14', '15', '16', '17', '22', '23')}
p['22']['inputs']['value'] = a.prefix; p['1']['inputs']['pixal3d_multiview'] = False
def load(i, img):
    p[f'L{i}'] = {"class_type": "Trellis2LoadImageWithTransparency", "inputs": {"image": img}}
    p[f'P{i}'] = {"class_type": "Trellis2PreProcessImage", "inputs": {"image": [f'L{i}', 2], "padding": 10, "remove_background": False, "max_size": 1024}}
    return [f'P{i}', 0]
cond = {"pipeline": ["1", 0], "front_image": load(0, a.front)}
if 'b' in a.views: cond['back_image'] = load(1, a.back)
if 'l' in a.views: cond['left_image'] = load(2, a.left)
if 'r' in a.views: cond['right_image'] = load(3, a.right)
p['6'] = {"class_type": "Trellis2ImageCondMultiViewGenerator", "inputs": cond}
p['21'] = {"class_type": "Trellis2SparseMultiViewGenerator", "inputs": {"pipeline": ["6", 3], "image_conds": ["6", 0], "views_list": ["6", 2], "seed": a.seed, "sparse_structure_steps": a.steps,
           "sparse_structure_guidance_strength": 7.5, "sparse_structure_guidance_rescale": 0.01, "sparse_structure_rescale_t": 5, "sparse_structure_sampler": "heun", "sparse_structure_resolution": 32,
           "sparse_structure_guidance_interval_start": 0.1, "sparse_structure_guidance_interval_end": 1, "fill_holes": True, "hole_iterations": 1, "verbose": False, "dino_lock": 0, "dino_substeps": 4,
           "hole_fill_algorithm": "flood_fill", "dino_foundation_cap": 1, "keep_only_shell": True, "front_axis": "z", "blend_temperature": 1}}
sh = {"shape_steps": a.steps, "shape_guidance_strength": 7.5, "shape_guidance_rescale": 0.01, "shape_rescale_t": 3, "shape_sampler": "heun", "shape_guidance_interval_start": 0.1,
      "shape_guidance_interval_end": 1, "verbose": False, "dino_lock": 0, "dino_substeps": 4, "dino_foundation_cap": 1, "front_axis": "z", "blend_temperature": 1}
p['19'] = {"class_type": "Trellis2ShapeMultiViewGenerator", "inputs": dict(sh, pipeline=["21", 3], image_conds=["6", 0], views_list=["21", 2], coords=["21", 0], resolution=1024)}
p['20'] = {"class_type": "Trellis2ShapeCascadeMultiViewGenerator", "inputs": dict(sh, pipeline=["19", 3], image_conds=["6", 1], views_list=["19", 2], shape_slat=["19", 0], from_resolution=["19", 1],
           to_resolution=1024, sparse_structure_resolution=["21", 1], max_num_tokens=999999)}
p['10']['inputs'].update(pipeline=["20", 3], shape_slat=["20", 0], resolution=["20", 1])
req = urllib.request.Request("http://127.0.0.1:8188/prompt", json.dumps({"prompt": p}).encode(), {"Content-Type": "application/json"})
try: pid = json.load(urllib.request.urlopen(req))["prompt_id"]
except urllib.error.HTTPError as e: print(e.read().decode()[:3000]); sys.exit(1)
t0 = time.time(); peak = 0
while time.time() - t0 < 1800:
    m = int(subprocess.run(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"], capture_output=True, text=True).stdout.split()[0]); peak = max(peak, m)
    h = json.load(urllib.request.urlopen(f"http://127.0.0.1:8188/history/{pid}"))
    if pid in h: break
    time.sleep(1.5)
r = h[pid]; print('status', r['status']['status_str'], 'secs', round(time.time() - t0, 1), 'peak_gpu_MiB', peak); print(json.dumps(r['outputs'])[:500])
for m in r['status']['messages']:
    if m[0] == 'execution_error': print(json.dumps(m[1])[:2500])
