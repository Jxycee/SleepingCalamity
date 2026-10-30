"""Live Blender bridge, Blender side: run inside a Blender with a window (cloud: under xvfb) so a session can drive it the way the PC's Blender MCP does.

    xvfb-run -a -s "-screen 0 1920x1080x24" blender --python Tools/cloud/sc_blender_live.py [-- --port 9876]

Normally started for you by Tools/cloud/sc_blender_mcp.py (the MCP server and CLI). Listens on 127.0.0.1 only. One JSON object per line in, one per
line out. Commands run on Blender's main thread (a bpy.app.timers poll), so bpy is safe to use:
    {"cmd": "exec", "code": "..."}          run Python in a namespace that persists between calls; set `result` to return a value
    {"cmd": "scene"}                        file, frame, and every object's name, type, location, dimensions, materials
    {"cmd": "display"}                      the X DISPLAY and XAUTHORITY Blender draws to (sc_blender_mcp.py grabs the window with ImageMagick:
                                            Blender's own screenshot reads an empty front buffer under xvfb)
    {"cmd": "viewport", "path": "..."}      PNG of the 3D view as drawn (bpy.ops.render.opengl), without the UI
    {"cmd": "ping"}
Replies: {"ok": true, ...} or {"ok": false, "error": "...", "traceback": "..."}.
"""
import contextlib
import io
import json
import os
import queue
import socket
import sys
import threading
import traceback

import bpy

PORT = 9876
if "--" in sys.argv:
    rest = sys.argv[sys.argv.index("--") + 1:]
    if "--port" in rest:
        PORT = int(rest[rest.index("--port") + 1])

JOBS = queue.Queue()
NAMESPACE = {"bpy": bpy, "__name__": "sc_live"}


def _window_override():
    win = bpy.context.window_manager.windows[0]
    area = max(win.screen.areas, key=lambda a: (a.type == "VIEW_3D", a.width * a.height))
    region = next(r for r in area.regions if r.type == "WINDOW")
    return {"window": win, "screen": win.screen, "area": area, "region": region}


def _run(req):
    cmd = req.get("cmd")
    if cmd == "ping":
        return {"ok": True, "version": bpy.app.version_string}
    if cmd == "exec":
        NAMESPACE.pop("result", None)
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
            exec(compile(req["code"], "<sc_live>", "exec"), NAMESPACE)
        res = NAMESPACE.get("result")
        try:
            json.dumps(res)
        except TypeError:
            res = repr(res)
        return {"ok": True, "stdout": out.getvalue()[-20000:], "stderr": err.getvalue()[-5000:], "result": res}
    if cmd == "scene":
        sc = bpy.context.scene
        objs = [{"name": o.name, "type": o.type, "location": [round(v, 4) for v in o.location],
                 "dimensions": [round(v, 4) for v in o.dimensions], "hidden": o.hide_get(),
                 "materials": [s.material.name for s in getattr(o, "material_slots", []) if s.material]}
                for o in sc.objects]
        return {"ok": True, "file": bpy.data.filepath, "scene": sc.name, "frame": sc.frame_current,
                "engine": sc.render.engine, "objects": objs[:500], "object_count": len(objs)}
    if cmd == "display":
        with bpy.context.temp_override(**_window_override()):
            bpy.ops.wm.redraw_timer(type="DRAW_WIN_SWAP", iterations=1)
        return {"ok": True, "display": os.environ.get("DISPLAY", ""), "xauthority": os.environ.get("XAUTHORITY", "")}
    if cmd == "viewport":
        sc = bpy.context.scene
        old = sc.render.filepath
        sc.render.filepath = req["path"]
        try:
            with bpy.context.temp_override(**_window_override()):
                bpy.ops.render.opengl(write_still=True, view_context=True)
        finally:
            sc.render.filepath = old
        return {"ok": True, "path": req["path"]}
    return {"ok": False, "error": f"unknown cmd {cmd!r}"}


def _pump():
    while True:
        try:
            req, box, done = JOBS.get_nowait()
        except queue.Empty:
            return 0.05
        try:
            box["reply"] = _run(req)
        except Exception as e:
            box["reply"] = {"ok": False, "error": f"{type(e).__name__}: {e}", "traceback": traceback.format_exc()[-8000:]}
        done.set()


def _serve(conn):
    with conn, conn.makefile("rwb") as f:
        for line in f:
            try:
                req = json.loads(line)
            except ValueError:
                reply = {"ok": False, "error": "bad json"}
            else:
                box, done = {}, threading.Event()
                JOBS.put((req, box, done))
                done.wait()
                reply = box["reply"]
            f.write((json.dumps(reply) + "\n").encode())
            f.flush()


def _listen():
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", PORT))
    srv.listen(4)
    print(f"SC_LIVE listening on 127.0.0.1:{PORT}", flush=True)
    while True:
        conn, _ = srv.accept()
        threading.Thread(target=_serve, args=(conn,), daemon=True).start()


bpy.app.timers.register(_pump, persistent=True)
threading.Thread(target=_listen, daemon=True).start()
