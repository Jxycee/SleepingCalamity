"""Before/after closeup sheet for a run (Cycles OPTIX, fixed seed). venv python.
    C:/AI/trellis2/venv/Scripts/python.exe render_compare.py <run_dir> [--samples 96] [--detail-dir skin_detail]
Needs out/skin_detail/{albedo,normal,roughness}_detail.png from apply_skin_detail.py. Writes out/<detail-dir>/renders/{before,after}_<shot>.png and out/<detail-dir>/<name>_skin_detail_sheet.png
(columns: before | after | amplified difference (x6, centred grey))."""
import os, sys, json, subprocess, argparse, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
from PIL import Image, ImageDraw
import uvgeom

BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('run'); ap.add_argument('--samples', type=int, default=96); ap.add_argument('--detail-dir', default='skin_detail')
    ap.add_argument('--shots', default='front,q34,cheek,nose,forehead'); ap.add_argument('--closeup-scale', type=float, default=1.0, help='multiplies the camera distance of cheek/nose/forehead'); a = ap.parse_args(); run = uvgeom.norm(a.run)
    od = run + '/out/' + a.detail_dir + '/'; rd = od + 'renders/'; os.makedirs(rd, exist_ok=True)
    g = uvgeom.build(run); pos = g['pos']; w = g['w']; patches = json.loads(str(g['patches']))
    def tnormal(px, py):
        pu = pos[py, px + 3] - pos[py, px - 3]; pv = pos[py - 3, px] - pos[py + 3, px]; n = np.cross(pu, pv); n /= np.linalg.norm(n) + 1e-12
        return n if n[1] < 0 else -n
    shots = []
    face = (w > 0.5) & (pos[..., 1] < -0.06); fc = pos[face].mean(0); fc[0] = 0
    ry = lambda v, deg: [v[0] * math.cos(math.radians(deg)) - v[1] * math.sin(math.radians(deg)), v[0] * math.sin(math.radians(deg)) + v[1] * math.cos(math.radians(deg)), v[2]]
    cands = {
        'front': dict(target=fc.tolist(), dir=[0, -1, 0], dist=0.85, lens=85, res=1100, key=55),
        'q34': dict(target=(fc + np.array([0.01, 0, 0])).tolist(), dir=ry([0, -1, 0], -38), dist=0.85, lens=85, res=1100, key=55),
    }
    px, py = patches['cheek_R(img-left)']; cn = tnormal(px, py); cands['cheek'] = dict(target=pos[py, px].tolist(), dir=(cn + np.array([0, 0, 0.1])).tolist(), dist=0.32, lens=85, res=1100, key=68)
    ys = np.where(w > 0.5, pos[..., 1], 9); iy, ix = np.unravel_index(np.argmin(ys), ys.shape); nt = pos[iy, ix] + np.array([0, 0.012, -0.004])
    cands['nose'] = dict(target=nt.tolist(), dir=[0, -1, 0.15], dist=0.30, lens=85, res=1100, key=62)
    px, py = patches['forehead']; cands['forehead'] = dict(target=pos[py, px].tolist(), dir=(tnormal(px, py) + np.array([0, 0, 0.0])).tolist(), dist=0.32, lens=85, res=1100, key=68)
    for k in ('cheek', 'nose', 'forehead'): cands[k]['dist'] *= a.closeup_scale
    shots = [dict(name=k, **cands[k]) for k in a.shots.split(',')]
    blend = [f for f in os.listdir(run + '/out') if f.endswith('.blend') and not f.startswith('char.')][0]
    for mode in ('before', 'after'):
        job = dict(out_dir=rd.rstrip('/'), mode=mode, shots=shots, samples=a.samples,
                   albedo=(od + 'albedo_detail.png') if mode == 'after' else None, normal=(od + 'normal_detail.png') if mode == 'after' else None, rough=(od + 'roughness_detail.png') if mode == 'after' else None)
        jp = od + 'job_%s.json' % mode; json.dump(job, open(jp, 'w'))
        r = subprocess.run([BLENDER, '-b', run + '/out/' + blend, '--python', os.path.dirname(os.path.abspath(__file__)) + '/blender_render.py', '--', jp], capture_output=True, text=True)
        print(mode, 'rc', r.returncode, r.stdout.count('RENDERED'), 'renders');
        if r.returncode or 'Error' in r.stdout[-2000:]: print(r.stdout[-1500:], r.stderr[-800:])
    rows = []
    for s in shots:
        b = np.array(Image.open(rd + 'before_%s.png' % s['name']).convert('RGB')).astype(np.float32); f = np.array(Image.open(rd + 'after_%s.png' % s['name']).convert('RGB')).astype(np.float32)
        d = np.clip((f - b) * 6 + 128, 0, 255)
        row = np.concatenate([b, f, d], 1).astype(np.uint8); im = Image.fromarray(row); dr = ImageDraw.Draw(im)
        for i, t in enumerate(('before', 'after', 'difference x6')): dr.text((8 + i * b.shape[1], 6), '%s  %s' % (s['name'], t), fill=(255, 255, 0))
        rows.append(np.array(im))
    name = os.path.basename(run); sheet = os.path.join(od, name + '_skin_detail_sheet.png'); Image.fromarray(np.concatenate(rows, 0)).save(sheet); print('SHEET', sheet)

if __name__ == '__main__': main()
