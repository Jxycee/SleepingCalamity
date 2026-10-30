#!/usr/bin/env python3
"""Live Blender for cloud sessions: an MCP server (stdio) and a CLI over Tools/cloud/sc_blender_live.py, which runs inside a windowed Blender under xvfb.
The PC uses its own Blender MCP; this is the cloud's stand-in, registered as `blender` so the same deny rule applies (CLAUDE.md: Blender stays
denied except in art sessions; the cloud art environment lifts it). No dependencies beyond the Python standard library (Pillow optional, for
resizing; ImageMagick `import` for window screenshots).

    python3 Tools/cloud/sc_blender_mcp.py                      MCP server on stdio (what `claude mcp add` registers)
    python3 Tools/cloud/sc_blender_mcp.py start|stop|status    manage the live Blender (start is automatic on the first tool call)
    python3 Tools/cloud/sc_blender_mcp.py exec 'CODE'          run Python in the live Blender; prints the reply JSON
    python3 Tools/cloud/sc_blender_mcp.py scene|screenshot|viewport [PATH]

Tools: blender_exec, blender_scene, blender_screenshot, blender_viewport, blender_restart. Blender is ~/apps/blender-4.5.3-linux-x64/blender
(override with SC_BLENDER); the bridge listens on 127.0.0.1:$SC_BLENDER_PORT (9876). Its log is ~/.sc-setup/blender-live.log.
"""
import base64
import glob
import io
import json
import os
import shutil
import signal
import socket
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
LIVE = os.path.join(HERE, "sc_blender_live.py")
PORT = int(os.environ.get("SC_BLENDER_PORT", "9876"))
BLENDER = os.environ.get("SC_BLENDER") or shutil.which("blender") or os.path.expanduser("~/apps/blender-4.5.3-linux-x64/blender")
STATE = os.path.expanduser("~/.sc-setup")
PIDFILE = os.path.join(STATE, "blender-live.pid")
LOG = os.path.join(STATE, "blender-live.log")
SHOTS = os.path.join(tempfile.gettempdir(), "sc-blender-live")


def call(req, timeout=600):
    with socket.create_connection(("127.0.0.1", PORT), timeout=timeout) as s:
        f = s.makefile("rwb")
        f.write((json.dumps(req) + "\n").encode())
        f.flush()
        line = f.readline()
    if not line:
        raise ConnectionError("Blender closed the connection")
    return json.loads(line)


def alive():
    try:
        return call({"cmd": "ping"}, timeout=5).get("ok", False)
    except OSError:
        return False


def first_run_prefs():
    """Save preferences once with the splash and quick-setup off, so the live window opens straight on the scene."""
    if glob.glob(os.path.expanduser("~/.config/blender/*/config/userpref.blend")):
        return
    subprocess.run([BLENDER, "-b", "--factory-startup", "--python-expr",
                    "import bpy; bpy.context.preferences.view.show_splash = False; bpy.ops.wm.save_userpref()"],
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


def start(wait=120):
    if alive():
        return "already running"
    os.makedirs(STATE, exist_ok=True)
    first_run_prefs()
    cmd = [BLENDER, "--python", LIVE, "--", "--port", str(PORT)]
    if not os.environ.get("DISPLAY"):
        cmd = ["xvfb-run", "-a", "-s", "-screen 0 1920x1080x24"] + cmd
    log = open(LOG, "ab")
    p = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT, stdin=subprocess.DEVNULL, start_new_session=True)
    open(PIDFILE, "w").write(str(p.pid))
    t0 = time.time()
    while time.time() - t0 < wait:
        if p.poll() is not None:
            raise RuntimeError(f"Blender exited with {p.returncode}; see {LOG}")
        if alive():
            return f"started (pid {p.pid}, {time.time() - t0:.0f} s)"
        time.sleep(1)
    raise TimeoutError(f"Blender did not answer on port {PORT} within {wait} s; see {LOG}")


def stop():
    """Ends the live Blender (and its xvfb-run) by pid file, then any other bridge Blender on this port."""
    stopped = False
    try:
        os.killpg(int(open(PIDFILE).read()), signal.SIGTERM)
        stopped = True
    except (OSError, ValueError):
        pass
    try:
        os.remove(PIDFILE)
    except OSError:
        pass
    if subprocess.run(["pkill", "-f", f"sc_blender_live.py -- --port {PORT}"], capture_output=True).returncode == 0:
        stopped = True
    for _ in range(20):
        if not alive():
            break
        time.sleep(0.5)
    return "stopped" if stopped else "not running"


def shot(cmd, path=None, max_size=1280):
    os.makedirs(SHOTS, exist_ok=True)
    path = path or os.path.join(SHOTS, f"{cmd}-{int(time.time() * 1000)}.png")
    if cmd == "screenshot":  # the whole X screen: Blender's own screenshot reads an empty front buffer under xvfb
        reply = call({"cmd": "display"})
        if not reply.get("ok"):
            return reply, None
        env = dict(os.environ, DISPLAY=reply["display"], XAUTHORITY=reply["xauthority"])
        r = subprocess.run(["import", "-window", "root", path], env=env, capture_output=True, text=True)
        if r.returncode != 0:
            return {"ok": False, "error": f"import failed: {r.stderr.strip()}"}, None
        reply = {"ok": True, "path": path}
    else:
        reply = call({"cmd": cmd, "path": path})
        if not reply.get("ok"):
            return reply, None
    data = open(path, "rb").read()
    try:
        from PIL import Image
        im = Image.open(io.BytesIO(data))
        if max(im.size) > max_size:
            im.thumbnail((max_size, max_size))
            buf = io.BytesIO()
            im.save(buf, "PNG")
            data = buf.getvalue()
    except ImportError:
        pass
    return reply, data


# ---------- MCP (JSON-RPC 2.0 over newline-delimited stdio) ----------

TOOLS = [
    {"name": "blender_exec", "description": "Run Python inside the live Blender (bpy available; the namespace persists between calls). Assign `result` to return a JSON value; stdout is returned too. Starts Blender on first use (about 10 s).",
     "inputSchema": {"type": "object", "properties": {"code": {"type": "string"}}, "required": ["code"]}},
    {"name": "blender_scene", "description": "The open file, scene, frame, render engine and every object's name, type, location, dimensions and materials.",
     "inputSchema": {"type": "object", "properties": {}}},
    {"name": "blender_screenshot", "description": "Screenshot of the whole Blender window (UI included).",
     "inputSchema": {"type": "object", "properties": {"max_size": {"type": "integer", "default": 1280}}}},
    {"name": "blender_viewport", "description": "The 3D viewport as drawn (solid/material preview), without the UI.",
     "inputSchema": {"type": "object", "properties": {"max_size": {"type": "integer", "default": 1280}}}},
    {"name": "blender_restart", "description": "Stop and start the live Blender (fresh factory scene).",
     "inputSchema": {"type": "object", "properties": {}}},
]


def tool_call(name, args):
    if name == "blender_restart":
        stop()
        return [{"type": "text", "text": start()}], False
    start()
    if name == "blender_exec":
        r = call({"cmd": "exec", "code": args.get("code", "")})
    elif name == "blender_scene":
        r = call({"cmd": "scene"})
    elif name in ("blender_screenshot", "blender_viewport"):
        r, data = shot(name.split("_")[1], max_size=int(args.get("max_size", 1280)))
        if data is not None:
            return [{"type": "image", "data": base64.b64encode(data).decode(), "mimeType": "image/png"},
                    {"type": "text", "text": f"saved {r['path']}"}], False
    else:
        return [{"type": "text", "text": f"unknown tool {name}"}], True
    return [{"type": "text", "text": json.dumps(r, indent=1)}], not r.get("ok", False)


def serve():
    def send(msg):
        sys.stdout.write(json.dumps(msg) + "\n")
        sys.stdout.flush()
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            msg = json.loads(line)
        except ValueError:
            continue
        mid, method = msg.get("id"), msg.get("method")
        if mid is None:
            continue  # notifications (initialized, cancelled)
        try:
            if method == "initialize":
                ver = msg.get("params", {}).get("protocolVersion", "2024-11-05")
                res = {"protocolVersion": ver, "capabilities": {"tools": {}},
                       "serverInfo": {"name": "sc-blender-live", "version": "1.0"}}
            elif method == "ping":
                res = {}
            elif method == "tools/list":
                res = {"tools": TOOLS}
            elif method == "tools/call":
                p = msg.get("params", {})
                try:
                    content, is_err = tool_call(p.get("name"), p.get("arguments") or {})
                except Exception as e:
                    content, is_err = [{"type": "text", "text": f"{type(e).__name__}: {e}"}], True
                res = {"content": content, "isError": is_err}
            else:
                send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32601, "message": f"method not found: {method}"}})
                continue
            send({"jsonrpc": "2.0", "id": mid, "result": res})
        except Exception as e:
            send({"jsonrpc": "2.0", "id": mid, "error": {"code": -32603, "message": str(e)}})


def main(argv):
    if not argv:
        return serve()
    cmd = argv[0]
    if cmd == "start":
        print(start())
    elif cmd == "stop":
        print(stop())
    elif cmd == "status":
        print("running" if alive() else "not running")
    elif cmd == "exec":
        start()
        print(json.dumps(call({"cmd": "exec", "code": argv[1] if len(argv) > 1 else sys.stdin.read()}), indent=1))
    elif cmd == "scene":
        start()
        print(json.dumps(call({"cmd": "scene"}), indent=1))
    elif cmd in ("screenshot", "viewport"):
        start()
        r, _ = shot(cmd, argv[1] if len(argv) > 1 else None)
        print(json.dumps(r, indent=1))
    else:
        print(__doc__)
        return 4
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]) or 0)
