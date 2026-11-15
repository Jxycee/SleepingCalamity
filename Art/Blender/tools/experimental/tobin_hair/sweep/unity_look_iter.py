"""One blocking iteration of the Unity hair colour round (run with the trellis venv python: needs numpy + cv2):
  C:/AI/trellis2/venv/Scripts/python.exe Art/Blender/tools/experimental/tobin_hair/sweep/unity_look_iter.py [--tag NAME] [--look unity_look.json] [--out DIR] [--skip-unity]
Applies unity_look.json (read by TobinHairMaterialBuilder inside Unity), shoots the rest-pose front/side/back through THIS worktree's Tools/unity.ps1,
builds <out>/sheet.png (reference row above capture row, ~1100 px wide, via Art/Blender/tools/sc_ab_sheet.py --grid) and prints/writes colour numbers:
mean hue (deg), saturation, value of the hair region in capture vs reference, per view. Capture hair region = pixels that change when the hair is hidden;
reference hair region = hull mask above z 1.68 on refs/crop_<view>.png (same as sweep_score.py; includes some forehead skin)."""
import argparse, json, os, subprocess, sys, time
import numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, *['..'] * 5))
sys.path.insert(0, os.path.join(HERE, '..', 'hairdeck'))
ap = argparse.ArgumentParser(); ap.add_argument('--tag', default=time.strftime('%H%M%S')); ap.add_argument('--look', default=os.path.join(HERE, 'unity_look.json'))
ap.add_argument('--out', default='C:/AI/test_out/heroes/tobin/hair_sweep/look'); ap.add_argument('--skip-unity', action='store_true')
ap.add_argument('--hull', default='C:/AI/test_out/heroes/tobin/hair_v9/hull'); ap.add_argument('--refs', default='C:/AI/test_out/heroes/tobin/refs')
ap.add_argument('--size', type=int, default=700); ap.add_argument('--timeout', type=int, default=9); A = ap.parse_args()
out = os.path.join(A.out, A.tag); cap = os.path.join(out, 'cap'); logd = os.path.join(out, 'unitylog'); os.makedirs(cap, exist_ok=True); os.makedirs(logd, exist_ok=True)
VIEWS = {'front': 'f', 'side': 's', 'back': 'b'}
if not A.skip_unity:
    env = dict(os.environ, SC_REAL='1', SC_HQ='1', SC_SIZE=str(A.size), SC_OUT_DIR=cap, SC_HAIR_LOOK=os.path.abspath(A.look), SC_HAIRMASK='1',
               SC_CAPTURE_ONLY=','.join('rest_' + c for c in VIEWS.values()))
    cmd = ['powershell', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', os.path.join(ROOT, 'Tools', 'unity.ps1'), 'exec', '-Graphics',
           '-Method', 'SleepingCalamity.EditorTools.TobinHairMotionCapture.Run', '-OutDir', logd, '-TimeoutMinutes', str(A.timeout)]
    r = subprocess.run(cmd, env=env, cwd=ROOT); print('unity exit', r.returncode)
    if r.returncode != 0: sys.exit(r.returncode)

def hsv_stats(rgb, m):                       # rgb uint8 HxWx3, m bool mask -> (hue deg, sat 0..1, val 0..1); hue = circular mean
    if m.sum() < 50: return None
    h = cv2.cvtColor(rgb[m].reshape(-1, 1, 3).astype(np.float32) / 255, cv2.COLOR_RGB2HSV).reshape(-1, 3)
    a = np.radians(h[:, 0]); hue = float(np.degrees(np.arctan2(np.sin(a).mean(), np.cos(a).mean())) % 360)
    return hue, float(h[:, 1].mean()), float(h[:, 2].mean())
rd = lambda p: cv2.cvtColor(cv2.imread(p), cv2.COLOR_BGR2RGB)
from hd_sil import load_cal                  # noqa: E402
cal, M = load_cal(A.hull); res = {}
for v, c in VIEWS.items():
    cp = rd(os.path.join(cap, f'rest_{c}.png')); npth = rd(os.path.join(cap, f'rest_{c}_nohair.png'))
    mask = np.abs(cp.astype(int) - npth.astype(int)).sum(2) > 24
    mask = cv2.morphologyEx(mask.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)).astype(bool)
    k = cal[v]; sz = k.get('sz', k['s']); row = int(k['eye_py'] - sz * (1.68 - cal['eye_z'])); rm = M[v].copy(); rm[max(row, 0):] = False
    ref = rd(os.path.join(A.refs, f'crop_{v}.png'))
    res[v] = {'capture': hsv_stats(cp, mask), 'reference': hsv_stats(ref, rm), 'capture_px': int(mask.sum())}
sheet = os.path.join(out, 'sheet.png')
imgs = [os.path.join(A.refs, f'crop_{v}.png') for v in VIEWS] + [os.path.join(cap, f'rest_{c}.png') for c in VIEWS.values()]
subprocess.run([sys.executable, os.path.join(ROOT, 'Art', 'Blender', 'tools', 'sc_ab_sheet.py'), sheet, '--grid', '3', '366'] + imgs, check=True)
lines = ['view     capture hue/sat/val          reference hue/sat/val         dHue   dSat   dVal']
for v, r in res.items():
    c, f = r['capture'], r['reference']
    if not c or not f: lines.append(f'{v:8s} missing region'); continue
    lines.append(f'{v:8s} {c[0]:6.1f} {c[1]:.2f} {c[2]:.2f}          {f[0]:6.1f} {f[1]:.2f} {f[2]:.2f}        {c[0]-f[0]:+6.1f} {c[1]-f[1]:+.2f} {c[2]-f[2]:+.2f}')
open(os.path.join(out, 'colour.txt'), 'w').write('\n'.join(lines) + '\n'); json.dump(res, open(os.path.join(out, 'colour.json'), 'w'), indent=1)
print('\n'.join(lines)); print('SHEET', sheet)
