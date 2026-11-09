# Blender bg: render many front-ortho Workbench images in one session.  usage: -- z0 z1 out_prefix mesh1.npz mesh2.npz ...   -> <out_prefix><i>.png + <out_prefix><i>.json (cam)
# (the render itself is bl_render.render_one, shared with the persistent worker r6_render_server.py; render_worker.py chooses between the two)
import os as _os, sys
sys.path.insert(0, _os.path.dirname(_os.path.abspath(__file__)))
import bl_render
a = sys.argv[sys.argv.index('--') + 1:]; z0, z1, pref, files = float(a[0]), float(a[1]), a[2], a[3:]
for i, fp in enumerate(files): bl_render.render_one(fp, z0, z1, f'{pref}{i}.png', f'{pref}{i}.json')
print('OK', len(files))
