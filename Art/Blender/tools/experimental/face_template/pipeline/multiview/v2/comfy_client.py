"""Tiny ComfyUI API client for the v2 evaluation (server on 8191 by default).
run(workflow_dict) -> list of output image paths (copied into out_dir). Stdlib only."""
import json, time, shutil, urllib.request, uuid, os, sys

HOST = os.environ.get("COMFY_HOST", "127.0.0.1:8191")
COMFY_ROOT = os.environ.get("COMFY_ROOT", "C:/AI/comfy_new")


def _post(path, data):
    req = urllib.request.Request(f"http://{HOST}{path}", json.dumps(data).encode(),
                                 {"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req))


def _get(path):
    return json.load(urllib.request.urlopen(f"http://{HOST}{path}"))


def free():
    req = urllib.request.Request(f"http://{HOST}/free", json.dumps({"unload_models": True, "free_memory": True}).encode(),
                                 {"Content-Type": "application/json"})
    urllib.request.urlopen(req).read()


def stage_input(path, name=None):
    """copy an image into ComfyUI/input, return its name for LoadImage"""
    name = name or os.path.basename(path)
    shutil.copy(path, os.path.join(COMFY_ROOT, "input", name))
    return name


def run(wf, out_dir=None, prefix=None, timeout=1800):
    pid = _post("/prompt", {"prompt": wf, "client_id": str(uuid.uuid4())})
    if "prompt_id" not in pid:
        raise RuntimeError(json.dumps(pid)[:2000])
    pid = pid["prompt_id"]
    t0 = time.time()
    while time.time() - t0 < timeout:
        h = _get(f"/history/{pid}")
        if pid in h:
            h = h[pid]
            if h["status"].get("status_str") == "error":
                raise RuntimeError(json.dumps(h["status"]["messages"][-3:])[:3000])
            outs = []
            for nid, o in h["outputs"].items():
                for im in o.get("images", []) + o.get("gifs", []):
                    src = os.path.join(COMFY_ROOT, im.get("type", "output"), im.get("subfolder", ""), im["filename"])
                    if out_dir:
                        os.makedirs(out_dir, exist_ok=True)
                        dst = os.path.join(out_dir, (prefix + "_" if prefix else "") + im["filename"])
                        shutil.copy(src, dst); src = dst
                    outs.append(src)
            return outs, time.time() - t0
        time.sleep(1.0)
    raise TimeoutError(pid)
