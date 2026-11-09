# Blender bg PERSISTENT render worker: `blender -b --factory-startup -P r6_render_server.py`. Reads one JSON request per stdin line {z0, z1, pref, jobs: [[i, mesh.npz], ...]},
# renders <pref><i>.png/.json for each job with bl_render.render_one (the same code as the one-shot r6_01b_batch_blender.py) and answers with a line '@@DONE <n>' (or '@@ERR <text>').
# Closing stdin ends the worker. Saves the ~2.3 s Blender start-up per measurement.
import os as _os, sys, json, traceback
sys.path.insert(0, _os.path.dirname(_os.path.abspath(__file__)))
import bl_render
print('@@READY', flush=True)
while True:
    line = sys.stdin.readline()
    if not line: break
    try:
        q = json.loads(line)
        for i, fp in q['jobs']: bl_render.render_one(fp, q['z0'], q['z1'], f"{q['pref']}{i}.png", f"{q['pref']}{i}.json")
        print('@@DONE', len(q['jobs']), flush=True)
    except Exception: print('@@ERR', traceback.format_exc().replace('\n', ' | '), flush=True)
