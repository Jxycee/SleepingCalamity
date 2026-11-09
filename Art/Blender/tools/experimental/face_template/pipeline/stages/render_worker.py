"""Landmark measurement of candidate meshes: Workbench render (Blender) + MediaPipe detection, through persistent workers.
measure_landmarks(npz_files, work, jit=None) -> list of (468, 2) pixel landmark arrays (None where no face), the same numbers as the legacy path
  `blender -b -P r6_01b_batch_blender.py` + `python r6_01c_batch_lm.py` (one launch of each per call), without the ~2.3 s Blender and ~0.9 s MediaPipe start-up per call, and with the
  render batch split over several Blender workers (Jacobian columns: 55 meshes).
Switches (env): SCP_RENDER_CPU=1 -> legacy launch-per-call path, no GPU use (the sheet stage then renders Cycles on the CPU); SCP_RENDER_WORKER=0 -> legacy path only;
  SCP_RENDER_WORKERS=<n> -> Blender workers for big batches (default 3, 1 = serial). A worker that fails to start or answer makes the call fall back to the legacy path (printed).
Determinism: each render starts from empty factory settings and the detector is stateless, so results do not depend on the worker, the batch split or the call order (checked bit-identical to the legacy path).
Workers are started on the first call and closed at interpreter exit (stdin EOF)."""
import os, sys, json, subprocess, atexit, threading, numpy as np
BLENDER = os.environ.get('SCP_BLENDER', 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'); MP = os.environ.get('SCP_MP_PY', 'C:/AI/envs/facekeys/Scripts/python.exe')
SC = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/') + '/'
Z0, Z1 = '1.46', '1.92'
def legacy(): return os.environ.get('SCP_RENDER_CPU') == '1' or os.environ.get('SCP_RENDER_WORKER') == '0'
class _Proc:
    def __init__(s, cmd, env=None):
        s.p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, env=env, bufsize=1)
        while True:
            l = s.p.stdout.readline()
            if not l: raise RuntimeError('worker exited during start-up: ' + ' '.join(map(str, cmd))[:120])
            if l.startswith('@@READY'): break
    def ask(s, q):
        s.p.stdin.write(json.dumps(q) + '\n'); s.p.stdin.flush(); tail = []
        while True:
            l = s.p.stdout.readline()
            if not l: raise RuntimeError('worker died: ' + ' '.join(tail[-5:]))
            if l.startswith('@@DONE'): return
            if l.startswith('@@ERR'): raise RuntimeError(l[:600])
            tail.append(l.strip())
    def close(s):
        try: s.p.stdin.close(); s.p.wait(timeout=15)
        except Exception: s.p.kill()
_state = {'bl': [], 'mp': None, 'dead': False}
def _close_all():
    for p in _state['bl'] + ([_state['mp']] if _state['mp'] else []): p.close()
    _state['bl'] = []; _state['mp'] = None
atexit.register(_close_all)
def _blenders(n):
    while len(_state['bl']) < n: _state['bl'].append(_Proc([BLENDER, '-b', '--factory-startup', '-P', SC + 'r6_render_server.py']))
    return _state['bl'][:n]
def _mp():
    if _state['mp'] is None: _state['mp'] = _Proc([MP, SC + 'r6_lm_server.py'])
    return _state['mp']
def _legacy(files, work, jit):
    r = subprocess.run([BLENDER, '-b', '--factory-startup', '-P', SC + 'r6_01b_batch_blender.py', '--', Z0, Z1, work + '_i'] + files, capture_output=True, text=True)
    assert 'OK' in r.stdout, r.stdout[-500:] + r.stderr[-500:]
    pngs = [work + f'_i{i}.png' for i in range(len(files))]
    env = dict(os.environ, SCP_LM_JIT=str(jit)); subprocess.run([MP, SC + 'r6_01c_batch_lm.py', work + '_lm.json'] + pngs, check=True, capture_output=True, env=env)
    return pngs
def _worker(files, work, jit, nw):
    pngs = [work + f'_i{i}.png' for i in range(len(files))]; nw = max(1, min(nw, len(files), 1 + len(files) // 6)); procs = _blenders(nw)
    chunks = [[[i, f] for i, f in enumerate(files) if i % nw == k] for k in range(nw)]; errs = []
    def go(p, jobs):
        try: p.ask(dict(z0=float(Z0), z1=float(Z1), pref=work + '_i', jobs=jobs))
        except Exception as e: errs.append(e)
    ths = [threading.Thread(target=go, args=(p, c)) for p, c in zip(procs, chunks)]
    [t.start() for t in ths]; [t.join() for t in ths]
    if errs: raise errs[0]
    _mp().ask(dict(out=work + '_lm.json', jit=int(jit), paths=pngs)); return pngs
def measure_landmarks(files, work, jit=None):
    jit = int(os.environ.get('SCP_LM_JIT', '0') or 0) if jit is None else int(jit); files = list(files)
    if legacy() or _state['dead']: pngs = _legacy(files, work, jit)
    else:
        try: pngs = _worker(files, work, jit, int(os.environ.get('SCP_RENDER_WORKERS', '3')))
        except Exception as e:
            print('render worker failed (%s); legacy launches from now on' % str(e)[:200], flush=True); _state['dead'] = True; _close_all(); pngs = _legacy(files, work, jit)
    lm = json.load(open(work + '_lm.json')); return [None if lm[p] is None else np.array(lm[p]) for p in pngs]
