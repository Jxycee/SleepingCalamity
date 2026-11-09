#!/usr/bin/env python
"""Face pipeline: one reference photo -> a character head on Tobin's MetaPerson topology (see README.md).
  python face_pipeline.py run    --config cfg.json [--from STAGE] [--to STAGE] [--force STAGE ...]
  python face_pipeline.py status --config cfg.json
  python face_pipeline.py score  --config cfg.json
  python face_pipeline.py clean  --config cfg.json [--stage STAGE | --all]
Stages are resumable: <out_dir>/manifest.json stores, per stage, a hash of (stage scripts + parameters + input file contents), the output hashes, seconds and status.
A stage is skipped when its recorded hash equals the current one and its outputs exist; changing a parameter or an upstream output re-runs it and everything after."""
import os, sys, json, glob, time, shutil, hashlib, argparse, subprocess, urllib.request
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
from paths import Paths, STAGES as STAGE_DIR

DEFAULTS = os.path.join(HERE, 'defaults.json')

def deep_merge(a, b):
    o = dict(a)
    for k, v in b.items(): o[k] = deep_merge(a[k], v) if isinstance(v, dict) and isinstance(a.get(k), dict) else v
    return o

def load_cfg(path):
    cfg = deep_merge(json.load(open(DEFAULTS)), json.load(open(path)))
    for k in ('image', 'name', 'out_dir'):
        if not cfg.get(k): raise SystemExit('config needs "%s"' % k)
    return cfg

def fhash(p):
    if not os.path.exists(p): return 'MISSING'
    h = hashlib.sha1(); n = os.path.getsize(p); h.update(str(n).encode())
    with open(p, 'rb') as f:
        if n < 60e6:
            for b in iter(lambda: f.read(1 << 20), b''): h.update(b)
        else: h.update(f.read(1 << 20)); f.seek(-(1 << 20), 2); h.update(f.read())
    return h.hexdigest()[:16]

class Ctx:
    def __init__(s, cfg):
        s.cfg = cfg; s.P = Paths(cfg); s.env = s.P.env(); s.T = s.P.tools; s.logdir = s.P.run + '/logs'; os.makedirs(s.logdir, exist_ok=True); s.log = None
    def run(s, cmd, name=None, expect=None, cwd=STAGE_DIR):
        """run a command in the stages dir with the SCP_* environment; stdout+stderr appended to logs/<stage>.log; fail on non-zero exit or when `expect` is missing from the output."""
        cmd = [str(c) for c in cmd]; t0 = time.time()
        with open(s.log, 'a', encoding='utf-8', errors='replace') as lf: lf.write('\n$ ' + ' '.join(cmd) + '\n'); lf.flush()
        r = subprocess.run(cmd, cwd=cwd, env=s.env, capture_output=True, text=True, encoding='utf-8', errors='replace')
        out = (r.stdout or '') + (r.stderr or '')
        with open(s.log, 'a', encoding='utf-8', errors='replace') as lf: lf.write(out + '\n[%.1fs exit %d]\n' % (time.time() - t0, r.returncode))
        if r.returncode != 0 or 'Traceback (most recent call last)' in out and not name == 'tolerate':
            raise RuntimeError('command failed (%d): %s\n%s' % (r.returncode, ' '.join(cmd)[:200], out[-1500:]))
        if expect and expect not in out: raise RuntimeError('expected %r in output of %s\n%s' % (expect, ' '.join(cmd)[:200], out[-1500:]))
        return out
    def py(s, script, *a, **k): return s.run([s.T['py'], script] + list(a), **k)
    def mp(s, script, *a, **k): return s.run([s.T['mp'], script] + list(a), **k)
    def bl(s, script, *a, **k): return s.run([s.T['blender'], '-b', '-P', os.path.join(STAGE_DIR, script), '--'] + list(a), **k)
    def render_lm(s, tag, z=None):
        """front-ortho Workbench render of fit/wrap_r<tag>_mesh.npz + mediapipe landmarks -> fit/{_rr_,rn_,_cam_,lm_}<tag>"""
        F = s.P.fit; z = z or s.cfg['render']['z']
        s.py('r6_prep_render.py', F + '/wrap_r%s_mesh.npz' % tag, F + '/_rr_%s.npz' % tag)
        s.bl('r6_01_blender.py', 'render', F + '/_rr_%s.npz' % tag, F + '/rn_%s.png' % tag, F + '/_cam_%s.json' % tag, z[0], z[1])
        s.mp('r2_02_landmarks.py', F + '/rn_%s.png' % tag, F + '/lm_%s.json' % tag, expect='faces 1')

# ---------------------------------------------------------------- GPU helpers
def gpu_temp():
    try: return int(subprocess.run(['nvidia-smi', '--query-gpu=temperature.gpu', '--format=csv,noheader'], capture_output=True, text=True, timeout=20).stdout.split()[0])
    except Exception: return 0

def gpu_cool(limit=82, maxwait=900):
    t0 = time.time()
    while gpu_temp() > limit and time.time() - t0 < maxwait: time.sleep(20)

def port_up(port):
    try: urllib.request.urlopen('http://127.0.0.1:%d/system_stats' % port, timeout=3); return True
    except Exception: return False

# ---------------------------------------------------------------- stage functions
def s_prep_image(c):
    c.mp('prep_image.py', c.cfg['_cfg_path'])

def s_target_3d(c):
    H = c.cfg['hunyuan']; T = c.P.target; os.makedirs(T + '/glb', exist_ok=True)
    if H.get('pinned_glb'):
        shutil.copy(H['pinned_glb'], T + '/shape.glb'); json.dump(dict(chosen='pinned', glb=H['pinned_glb']), open(T + '/pick.json', 'w')); return
    if port_up(8188):   # free the TRELLIS server's models before Hunyuan loads
        try: urllib.request.urlopen(urllib.request.Request('http://127.0.0.1:8188/free', json.dumps({'unload_models': True, 'free_memory': True}).encode(), {'Content-Type': 'application/json'}), timeout=20)
        except Exception: pass
    if not port_up(8190):
        bat = 'C:/AI/stablegen/start_comfy_8190.bat'
        subprocess.Popen(['cmd', '/c', 'start', '', '/min', bat], cwd='C:/AI/stablegen'); t0 = time.time()
        while not port_up(8190) and time.time() - t0 < 240: time.sleep(5)
        if not port_up(8190): raise RuntimeError('ComfyUI port 8190 did not come up (start C:/AI/stablegen/start_comfy_8190.bat)')
    for seed in H['seeds']:
        out = T + '/glb/shape_s%s.glb' % seed
        if not os.path.exists(out):
            gpu_cool(); pref = '3D/pipe_%s_s%s' % (c.cfg['name'], seed)
            c.run([c.T['comfy_py'], c.T['hy_script'], T + '/cutout_rgba.png', pref, H.get('steps', 30), seed, H.get('faces', 40000)])
            cand = sorted(glob.glob('C:/AI/stablegen/ComfyUI/output/3D/pipe_%s_s%s_*.glb' % (c.cfg['name'], seed)), key=os.path.getmtime)
            if not cand: raise RuntimeError('no Hunyuan output for seed %s' % seed)
            shutil.copy(cand[-1], out)
        c.bl('cloud_render_clay.py', out, T + '/glb/clay_s%s' % seed, 'clay', 'front')
    c.mp('target_pick.py', c.cfg['_cfg_path'])
    if H.get('trellis_tex_fallback'):   # optional, untested here: texture for a visual check only; downstream stages use the shape
        c.run([c.T['py'], 'C:/AI/scripts/run_tex_ext.py', T + '/shape.glb', T + '/cutout_rgba.png', 'pipe_%s_tex' % c.cfg['name']])

def s_photo_landmarks(c): c.mp('photo_landmarks.py')

def s_template_prep(c):
    F = c.P.fit
    for f in ('tpl_hits.json', 'tpl_cam.json', 'lm_ids.json'): shutil.copy(c.P.data_dir + '/' + f, F + '/' + f)
    if c.cfg['template']['sliders_enabled']: c.py('r6_00_make_template.py')
    else:
        z = __import__('numpy').load(c.P.tpl_npz, allow_pickle=True); __import__('numpy').savez(F + '/tobin_head_template6.npz', **{k: z[k] for k in z.files})

def s_align(c):
    F, A = c.P.fit, c.cfg['align']; z = A['target_render_z']
    c.bl('r6_01_blender.py', 'export', F + '/target_raw.npz', expect='EXPORT')
    c.bl('r6_01_blender.py', 'render', F + '/target_raw.npz', F + '/tgt_front.png', F + '/tgt_cam.json', z[0], z[1])
    c.mp('r2_02_landmarks.py', F + '/tgt_front.png', F + '/tgt_pix.json', expect='faces 1')
    ids = json.load(open(F + '/lm_ids.json')); p = json.load(open(F + '/tgt_pix.json'))
    json.dump({str(i): p[str(i)] for i in ids if str(i) in p}, open(F + '/tgt_pix_sel.json', 'w'))
    c.bl('r6_01_blender.py', 'ray', F + '/target_raw.npz', F + '/tgt_cam.json', F + '/tgt_pix_sel.json', F + '/tgt_hits.json')
    c.py('r2_03_align.py', A['lm_weight'], A['zcut_offset']); c.py('r2_03b_crop.py')
    c.py('r2_04_sliders.py', A['slider_lam'], A['slider_zr']); c.py('r2_04_sliders.py', A['slider_lam'], A['slider_zr'], 'noeye', '_noeye'); c.py('r2_03c_lmpairs.py')
    W = A['wrap']
    c.py('r3_02_wrap.py', '1', '--pass2', W['pass2'], '--prewarp', W['prewarp'], '--eyelm', W['eyelm'], '--sliders', 'slider_verts_noeye.npy', expect='PASS')
    c.render_lm('1')
    S = c.cfg['skull']
    c.py('r4_02_global.py', F + '/wrap_r1_basis.npy', F + '/rn_1.png', F + '/_cam_1.json', F + '/lm_1.json', '51', '--hmax', S['hmax'], *(['--nowidth'] if S['nowidth'] else []), expect='GATES PASS=True')
    c.render_lm('51')

def s_fit(c):
    F, Q = c.P.fit, c.cfg['fit']
    cols = 'all' if c.cfg['template']['sliders_enabled'] else 'warp'
    c.py('r6_06_sliderloop.py', F + '/wrap_r51_basis.npy', F + '/lm_51.json', F + '/_cam_51.json', F + '/rn_51.png', Q['tag'], '--cols', cols, '--seed', Q['seed_theta'],
         '--iters', Q['iters'], '--tr', Q['tr'], '--w3', Q['w3'], '--skincap', Q['skincap'], '--wnose', Q['wnose'], '--wlips', Q['wlips'], '--wskin', Q['wskin'],
         '--sreg', Q['sreg'], '--remeasure', Q['remeasure'])
    shutil.copy(c.log, F + '/fit_%s.log' % Q['tag'])

def s_snap(c):
    F, Q, N = c.P.fit, c.cfg['fit'], c.cfg['snap']
    c.render_lm(Q['tag'])
    c.py('r6_03_corr.py', F + '/wrap_r%s_basis.npy' % Q['tag'], F + '/lm_%s.json' % Q['tag'], F + '/_cam_%s.json' % Q['tag'], F + '/rn_%s.png' % Q['tag'], N['tag'],
         '--damp', N['damp'], '--snapmode', N['snapmode'], '--snapsmooth', N['snapsmooth'], expect='GATES PASS=True')
    c.render_lm(N['tag'])

def s_key_transfer(c):
    """Keys are delta-transferred inside r6_06/r6_03 (wrap_r<tag>_keys.npy); this stage proves them and records the jaw-key hinge rescale input (the rescale itself is applied in reseat)."""
    import numpy as np
    F, tag = c.P.fit, c.cfg['snap']['tag']
    k = np.load(F + '/wrap_r%s_keys.npy' % tag); b = np.load(F + '/wrap_r%s_basis.npy' % tag); t = np.load(F + '/tobin_head_template6.npz', allow_pickle=True)
    assert k.shape == t['head/keys'].shape and k.shape[1:] == b.shape and np.isfinite(k).all() and np.isfinite(b).all(), 'key block shape/NaN check failed %s' % (k.shape,)
    json.dump(dict(keys=int(k.shape[0]), verts=int(k.shape[1]), max_abs_delta_mm=float(np.abs(k - b).max() * 1e3)), open(F + '/key_transfer.json', 'w'), indent=1)

def s_reseat(c):
    F, B, tag = c.P.fit, c.P.bake, c.cfg['snap']['tag']
    for src, dst in (('_cam_%s.json' % tag, 'wrap_cam.json'), ('lm_%s.json' % tag, 'wrap_lm.json'), ('rn_%s.png' % tag, 'wrap_front.png')): shutil.copy(F + '/' + src, B + '/' + dst)
    if os.path.exists(B + '/parts_nominal.npz'): os.remove(B + '/parts_nominal.npz')
    c.env['SC_FINAL_TAG'] = tag
    c.py('c6_00_reseat.py')
    if c.cfg['switches']['teeth_opt']: c.py('c6_00b_teeth_opt.py', '--apply')

def s_texture(c):
    c.env['SC_FINAL_TAG'] = c.cfg['snap']['tag']
    c.py('c6_01_mapfit.py'); c.py('c6_02_texture.py', expect='TEXTURE DONE'); c.py('c6_03_eyes.py')

def s_build(c):
    c.env['SC_FINAL_TAG'] = c.cfg['snap']['tag']
    out = c.run([c.T['blender'], '-b', '--factory-startup', '--python', os.path.join(STAGE_DIR, 'c6_04_build.py')], expect='BUILD DONE')
    open(c.P.fit + '/build.log', 'w', encoding='utf-8').write(out)

def s_score(c):
    F, B, Q, N = c.P.fit, c.P.bake, c.cfg['fit'], c.cfg['snap']
    c.mp('r6_10_score.py', 'fit=%s/rn_%s.png' % (F, Q['tag']), 'final=%s/rn_%s.png' % (F, N['tag']), '--tex', B + '/texture.json')
    c.py('score_final.py', c.cfg['_cfg_path'], N['tag'])

def s_sheet(c):
    O = c.P.out; c.env['SC_FINAL_TAG'] = c.cfg['snap']['tag']
    if c.cfg['switches']['render_sheet']:
        c.run([c.T['blender'], '-b', '--factory-startup', '--python', os.path.join(STAGE_DIR, 'c6_05_render.py')], expect='RENDER DONE')
        c.py('c6_06_compose.py')
    n = c.cfg['name']
    for a, b in (('char_proof_sheet.png', 'proof_sheet.png'), ('char_closeups.png', 'closeups.png'), ('char_overlay.png', 'overlay.png')):
        if os.path.exists(O + '/' + a): shutil.copy(O + '/' + a, O + '/%s_%s' % (n, b))
    if os.path.exists(O + '/char.blend'): shutil.copy(O + '/char.blend', O + '/%s.blend' % n)

# name, fn, scripts, cfg param keys, inputs (relative to run dir; '@image' = config image), outputs, est. minutes, gpu
STAGE_TABLE = [
 ('prep_image',      s_prep_image,      ['prep_image.py'], ['prep'], ['@image'], ['target/crop_src.png', 'target/mask_vis.png', 'target/cutout_rgba.png'], 0.1, False),
 ('target_3d',       s_target_3d,       ['target_pick.py', 'cloud_render_clay.py'], ['hunyuan'], ['target/cutout_rgba.png'], ['target/shape.glb', 'target/pick.json'], 8, True),
 ('photo_landmarks', s_photo_landmarks, ['photo_landmarks.py'], [], ['target/crop_src.png'], ['target/landmarks.json'], 0.1, False),
 ('template_prep',   s_template_prep,   ['r6_00_make_template.py'], ['template'], [], ['fit/tobin_head_template6.npz'], 0.3, False),
 ('align',           s_align,           ['r6_01_blender.py', 'r2_02_landmarks.py', 'r2_03_align.py', 'r2_03b_crop.py', 'r2_03c_lmpairs.py', 'r2_04_sliders.py', 'r3_02_wrap.py', 'r4_02_global.py', 'r6_prep_render.py', 'r2_common.py', 'r3_common.py', 'r4_common.py', 'fit_checks.py'],
                     ['align', 'skull', 'render'], ['target/shape.glb', 'fit/tobin_head_template6.npz'], ['fit/wrap_r51_basis.npy', 'fit/lm_51.json', 'fit/_cam_51.json', 'fit/rn_51.png'], 1, False),
 ('fit',             s_fit,             ['r6_06_sliderloop.py', 'r6_02_warp.py', 'fit_checks6.py', 'r6_metrics.py', 'r6_common.py'], ['fit', 'template'], ['fit/wrap_r51_basis.npy', 'fit/lm_51.json', '@seed'], ['fit/wrap_ra6_basis.npy', 'fit/wrap_ra6_mesh.npz'], 35, False),
 ('snap',            s_snap,            ['r6_03_corr.py', 'r6_metrics.py'], ['snap', 'fit', 'render'], ['fit/wrap_ra6_basis.npy'], ['fit/wrap_rh6_basis.npy', 'fit/wrap_rh6_keys.npy', 'fit/rn_h6.png', 'fit/lm_h6.json'], 3, False),
 ('key_transfer',    s_key_transfer,    [], ['snap'], ['fit/wrap_rh6_keys.npy', 'fit/wrap_rh6_basis.npy'], ['fit/key_transfer.json'], 0.1, False),
 ('reseat',          s_reseat,          ['c6_00_reseat.py', 'c6_00b_teeth_opt.py', 'c6_common.py'], ['switches', 'snap'], ['fit/wrap_rh6_basis.npy', 'fit/wrap_rh6_keys.npy', 'fit/rn_h6.png'], ['bake/parts.npz', 'bake/reseat.json'], 14, False),
 ('texture',         s_texture,         ['c6_01_mapfit.py', 'c6_02_texture.py', 'c6_03_eyes.py'], ['iris_colour', 'snap'], ['bake/parts.npz', 'target/crop_src.png', 'target/landmarks.json'], ['bake/texture.json', 'bake/Head_BaseColor_char.png', 'bake/Eye_BaseColor_char.png'], 1, False),
 ('build',           s_build,           ['c6_04_build.py'], ['snap'], ['bake/parts.npz', 'bake/texture.json', 'bake/Head_BaseColor_char.png'], ['out/char.blend', 'fit/build.log'], 1, False),
 ('score',           s_score,           ['r6_10_score.py', 'score_final.py'], ['thresholds', 'compare_to', 'snap', 'fit'], ['fit/rn_h6.png', 'fit/rn_a6.png', 'bake/texture.json', 'fit/build.log', 'fit/wrap_rh6_report.json'], ['out/score.json'], 0.5, False),
 ('sheet',           s_sheet,           ['c6_05_render.py', 'c6_06_compose.py'], ['switches', 'name'], ['out/char.blend', 'out/score.json'], ['out/proof_sheet.png'] , 5, False),
]
NAMES = [s[0] for s in STAGE_TABLE]

def stage_inputs(c, spec):
    out = []
    for i in spec:
        if i == '@image': out.append(c.cfg['image'])
        elif i == '@seed': out.append(c.cfg['fit']['seed_theta'])
        else: out.append(c.P.run + '/' + i)
    return out

def stage_outputs(c, spec):
    r = []
    for o in spec:
        if o.endswith('proof_sheet.png'): o = 'out/proof_sheet.png' if c.cfg['switches']['render_sheet'] else 'out/score.json'
        r.append(c.P.run + '/' + o)
    return r

def stage_hash(c, row):
    name, fn, scripts, keys, ins, outs = row[:6]
    h = hashlib.sha1(name.encode())
    for sname in scripts + ['paths.py']: h.update(fhash(os.path.join(STAGE_DIR, sname) if sname != 'paths.py' else os.path.join(HERE, 'paths.py')).encode())
    h.update(json.dumps({k: c.cfg.get(k) for k in keys}, sort_keys=True, default=str).encode())
    for p in stage_inputs(c, ins): h.update(fhash(p).encode())
    return h.hexdigest()

def load_manifest(c):
    p = c.P.run + '/manifest.json'
    return json.load(open(p)) if os.path.exists(p) else {'stages': {}}

def save_manifest(c, m): json.dump(m, open(c.P.run + '/manifest.json', 'w'), indent=1)

def cmd_run(a):
    c = Ctx(a.cfg_obj); m = load_manifest(c)
    i0 = NAMES.index(a.__dict__['from']) if a.__dict__['from'] else 0; i1 = NAMES.index(a.to) if a.to else len(NAMES) - 1
    t_all = time.time(); forced = set(a.force or [])
    for i, row in enumerate(STAGE_TABLE):
        name = row[0]
        if i < i0 or i > i1: continue
        if c.cfg['switches'].get('skip', {}).get(name): print('[%-15s] skipped by config' % name); continue
        h = stage_hash(c, row); rec = m['stages'].get(name, {}); outs = stage_outputs(c, row[5])
        if name not in forced and rec.get('status') == 'done' and rec.get('hash') == h and all(os.path.exists(o) for o in outs):
            print('[%-15s] up to date (%.0fs)' % (name, rec.get('seconds', 0))); continue
        print('[%-15s] running (est %s min%s) ...' % (name, row[6], ', GPU' if row[7] else ''), flush=True)
        c.log = '%s/%s.log' % (c.logdir, name); open(c.log, 'w').close(); t0 = time.time()
        m['stages'][name] = dict(status='running', hash=h, started=time.strftime('%Y-%m-%d %H:%M:%S')); save_manifest(c, m)
        try:
            row[1](c)
            miss = [o for o in outs if not os.path.exists(o)]
            if miss: raise RuntimeError('stage did not produce: %s' % miss)
        except Exception as e:
            m['stages'][name].update(status='failed', seconds=time.time() - t0, error=str(e)[:2000]); save_manifest(c, m)
            print('[%-15s] FAILED after %.0fs; see %s\n%s' % (name, time.time() - t0, c.log, str(e)[:1500])); sys.exit(1)
        m['stages'][name].update(status='done', seconds=round(time.time() - t0, 1), outputs={os.path.relpath(o, c.P.run): fhash(o) for o in outs}, inputs=[os.path.relpath(p, c.P.run) if p.startswith(c.P.run) else p for p in stage_inputs(c, row[4])])
        save_manifest(c, m); print('[%-15s] done in %.0fs' % (name, time.time() - t0), flush=True)
    m['last_run_seconds'] = round(time.time() - t_all, 1); save_manifest(c, m)
    print('run finished in %.0fs' % (time.time() - t_all))
    if os.path.exists(c.P.out + '/score.json') and i1 >= NAMES.index('score'): cmd_score(a)

def cmd_status(a):
    c = Ctx(a.cfg_obj); m = load_manifest(c); tot = 0
    for row in STAGE_TABLE:
        r = m['stages'].get(row[0], {}); cur = stage_hash(c, row) == r.get('hash') and all(os.path.exists(o) for o in stage_outputs(c, row[5]))
        st = r.get('status', 'pending'); st = 'stale' if st == 'done' and not cur else st; tot += r.get('seconds', 0) if r.get('status') == 'done' else 0
        print('%-16s %-8s %8s s   est %5s min %s' % (row[0], st, r.get('seconds', '-'), row[6], 'GPU' if row[7] else ''))
    print('total recorded %.0f s' % tot)

def cmd_score(a):
    c = Ctx(a.cfg_obj); p = c.P.out + '/score.json'
    if not os.path.exists(p): raise SystemExit('no score yet: run through the score stage')
    s = json.load(open(p)); print('scorecard %s (final tag %s)' % (s['name'], s['final_tag']))
    for k, v in s['criteria'].items():
        val = '%.3f' % v['value'] if isinstance(v['value'], float) else v['value']
        print('  %-22s %-9s %s %-6s %s%s' % (k, val, v['op'], v['threshold'], 'PASS' if v['pass'] else 'FAIL', ('   delta vs ref %+.3f' % v['delta']) if 'delta' in v else ''))

def cmd_clean(a):
    c = Ctx(a.cfg_obj)
    if a.all:
        for d in ('fit', 'bake', 'out', 'logs'): shutil.rmtree(c.P.run + '/' + d, ignore_errors=True)
        if os.path.exists(c.P.run + '/manifest.json'): os.remove(c.P.run + '/manifest.json')
        print('removed fit/ bake/ out/ logs/ manifest (target/ kept: delete it by hand to redo prep and Hunyuan)'); return
    m = load_manifest(c); names = NAMES[NAMES.index(a.stage):]
    for n in names:
        for rel in m['stages'].get(n, {}).get('outputs', {}):
            try: os.remove(c.P.run + '/' + rel)
            except OSError: pass
        m['stages'].pop(n, None)
    save_manifest(c, m); print('cleaned stages', names)

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter); ap.add_argument('cmd', choices=['run', 'status', 'score', 'clean'])
    ap.add_argument('--config', required=True); ap.add_argument('--from', dest='from', choices=NAMES); ap.add_argument('--to', choices=NAMES); ap.add_argument('--force', action='append', choices=NAMES)
    ap.add_argument('--stage', choices=NAMES); ap.add_argument('--all', action='store_true'); a = ap.parse_args()
    a.cfg_obj = load_cfg(a.config); a.cfg_obj['_cfg_path'] = os.path.abspath(a.config)
    if a.cmd == 'clean' and not (a.all or a.stage): raise SystemExit('clean needs --stage S or --all')
    # the stage scripts read the merged config (defaults + file) from out_dir/config.resolved.json
    os.makedirs(a.cfg_obj['out_dir'], exist_ok=True); rp = a.cfg_obj['out_dir'].rstrip('/') + '/config.resolved.json'
    json.dump({k: v for k, v in a.cfg_obj.items() if k != '_cfg_path'}, open(rp, 'w'), indent=1); a.cfg_obj['_cfg_path'] = rp
    {'run': cmd_run, 'status': cmd_status, 'score': cmd_score, 'clean': cmd_clean}[a.cmd](a)

if __name__ == '__main__': main()
