"""Tobin hair sweep-and-score. One command: HairDeck variants (config JSON) -> numeric scores -> ranked CSV + one comparison sheet.
    python hair_sweep.py <config.json> --out <dir> [--force] [--only id,id] [--sheet-only]
Any python 3 runs this driver (stdlib only); it launches the HairDeck venv (hd_strands), the trellis venv (hd_mix, hd_opt, scoring, sheet)
and Blender 5.2 headless (hd_fit, sweep_render) per variant, sequentially. Stages are cached by hash in <out>/round<N>/variants/<id>/.key_*,
so a re-run only does new or changed variants; a failing variant is recorded (error column) and the sweep continues. See README.md."""
import sys, os, json, csv, hashlib, subprocess, time, argparse, shutil
HERE = os.path.dirname(os.path.abspath(__file__)); HD = os.path.normpath(os.path.join(HERE, '..', 'hairdeck'))
ap = argparse.ArgumentParser(); ap.add_argument('config'); ap.add_argument('--out', required=True); ap.add_argument('--force', action='store_true')
ap.add_argument('--only', default=''); ap.add_argument('--sheet-only', action='store_true'); A = ap.parse_args()
CFG = json.load(open(A.config)); T = dict(
    hd_py='C:/AI/hairdeck/.venv/Scripts/python.exe', tr_py='C:/AI/trellis2/venv/Scripts/python.exe',
    blender='C:/Program Files/Blender Foundation/Blender 5.2/blender.exe', head='C:/AI/test_out/heroes/tobin/hairdeck/opt/head.npz',
    hull='C:/AI/test_out/heroes/tobin/hair_v9/hull', refs='C:/AI/test_out/heroes/tobin/refs', cards_root='C:/AI/test_out/heroes/tobin/hairdeck'); T.update(CFG.get('tools', {}))
RD = os.path.join(A.out, 'round%d' % CFG.get('round', 1)); VROOT = os.path.join(RD, 'variants'); os.makedirs(VROOT, exist_ok=True)
def merged(d, v, k): x = dict(d.get(k) or {}); x.update(v.get(k) or {}); return x
def sha(*parts):
    h = hashlib.sha1()
    for p in parts: h.update((p if isinstance(p, bytes) else json.dumps(p, sort_keys=True).encode()))
    return h.hexdigest()[:12]
def fhash(path): return hashlib.sha1(open(path, 'rb').read()).hexdigest()[:12] if os.path.exists(path) else 'missing'
def cards_path(p): return p if os.path.isabs(p) or ':' in p else os.path.join(T['cards_root'], p)
def run(cmd, log):
    with open(log, 'w', errors='ignore') as f: r = subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT)
    if r.returncode: raise RuntimeError('%s failed (exit %d), see %s' % (os.path.basename(cmd[1] if len(cmd) > 1 else cmd[0]), r.returncode, log))
def stage(vd, name, key, fn, outs):
    kf = os.path.join(vd, '.key_' + name)
    if not A.force and os.path.exists(kf) and open(kf).read() == key and all(os.path.exists(os.path.join(vd, o)) for o in outs): return False
    if os.path.exists(kf): os.remove(kf)
    fn(); open(kf, 'w').write(key); return True
def opts(d, flag='--'):
    out = []
    for k, v in d.items(): out += [flag + k, str(v)]
    return out
def do_variant(v):
    vd = os.path.join(VROOT, v['id']); os.makedirs(vd, exist_ok=True); D = CFG.get('defaults', {})
    st, op, fi = merged(D, v, 'strands'), merged(D, v, 'opt'), merged(D, v, 'fit'); tint = v.get('tint', D.get('tint')) or {}
    # 1 cards (plain card json or a hd_mix recipe)
    cards = os.path.join(vd, 'cards.json')
    if v.get('mix'):
        m = v['mix']; ka, kb = cards_path(m['a']), cards_path(m['b'])
        key = sha(m, fhash(ka), fhash(kb), fhash(os.path.join(HD, 'hd_mix.py')))
        stage(vd, 'cards', key, lambda: run([T['tr_py'], os.path.join(HD, 'hd_mix.py'), ka, kb, cards] + [str(x) for x in m.get('args', [])], os.path.join(vd, 'mix.log')), ['cards.json'])
    else:
        src = cards_path(v['cards']); key = sha(fhash(src))
        stage(vd, 'cards', key, lambda: shutil.copyfile(src, cards), ['cards.json'])
    ck = open(os.path.join(vd, '.key_cards')).read()
    # 2 strands
    sk = sha(ck, st, fhash(os.path.join(HD, 'hd_strands.py')))
    stage(vd, 'strands', sk, lambda: run([T['hd_py'], os.path.join(HD, 'hd_strands.py'), cards, os.path.join(vd, 'strands.npz')] + opts(st), os.path.join(vd, 'strands.log')), ['strands.npz'])
    # 3 silhouette fit (hd_opt); opt.fix = {sx:..} keeps those parameters fixed, opt.w = weights, opt.rounds
    ok = sha(sk, op, fhash(os.path.join(HD, 'hd_opt.py')), fhash(os.path.join(HD, 'hd_sil.py')))
    ocmd = [T['tr_py'], os.path.join(HD, 'hd_opt.py'), T['head'], os.path.join(vd, 'strands.npz'), T['hull'], os.path.join(vd, 'opt')]
    if op.get('w'): ocmd += ['--w', str(op['w'])]
    if op.get('rounds') is not None: ocmd += ['--rounds', str(op['rounds'])]
    if op.get('fix'): ocmd += ['--fix', ','.join('%s=%s' % kv for kv in op['fix'].items())]
    stage(vd, 'opt', ok, lambda: run(ocmd, os.path.join(vd, 'opt.log')), ['opt/best.json', 'opt/fit_best.npz'])
    p = json.load(open(os.path.join(vd, 'opt', 'best.json')))['best']['params']
    # 4 Blender fit (clearance, coverage) with the fitted parameters
    fk = sha(ok, fi, p, fhash(os.path.join(HD, 'hd_fit.py')))
    stage(vd, 'fit', fk, lambda: run([T['blender'], '-b', '--factory-startup', '-P', os.path.join(HD, 'hd_fit.py'), '--', os.path.join(vd, 'strands.npz'), vd,
        'res=64'] + ['%s=%s' % (k, round(x, 6)) for k, x in p.items()] + ['%s=%s' % kv for kv in fi.items()], os.path.join(vd, 'fit.log')), ['hd_fit.blend'])
    # 5 preview renders (tint) + hair-only alpha pass
    rk = sha(fk, tint, fhash(os.path.join(HERE, 'sweep_render.py')))
    stage(vd, 'render', rk, lambda: run([T['blender'], '-b', os.path.join(vd, 'hd_fit.blend'), '-P', os.path.join(HERE, 'sweep_render.py'), '--', vd] + ['%s=%s' % kv for kv in tint.items()],
        os.path.join(vd, 'render.log')), ['prev_front.png', 'hair_back.png'])
    # 6 scores
    ctx = os.path.join(A.out, 'ctx.json'); json.dump(dict(head=T['head'], hull=T['hull'], refs=T['refs'], jut_mm=CFG.get('jut_mm', 6.0)), open(ctx, 'w'))
    sk2 = sha(rk, fhash(os.path.join(HERE, 'sweep_score.py')), CFG.get('jut_mm', 6.0))
    stage(vd, 'score', sk2, lambda: run([T['tr_py'], os.path.join(HERE, 'sweep_score.py'), vd, ctx], os.path.join(vd, 'score.log')), ['scores.json'])
    return json.load(open(os.path.join(vd, 'scores.json')))
W = CFG.get('weights', {})
def components(s):
    """each 0..1, higher is better; rank = sum(w * c) / sum(w)."""
    g = lambda k, d=0.0: s.get(k) if s.get(k) is not None else d
    return dict(iou_mean=g('iou_mean'), iou_frame=g('iou_frame'), iou_min=min(g('iou_front'), g('iou_side'), g('iou_back')),
        jut=max(0.0, 1 - g('jut_side_frac') / 0.10), color=max(0.0, 1 - g('dE_mean', 40) / 30.0), warm=1.0 - g('warm_back'),
        clear=max(0.0, 1 - g('clear_pushed_frac') / 0.5), cover=g('cover'))
def rank_of(s):
    c = components(s); w = {k: W.get(k, 0.0) for k in c}; t = sum(w.values()) or 1.0
    return sum(w[k] * c[k] for k in c) / t
t0 = time.time(); rows = []; only = set(x for x in A.only.split(',') if x)
for v in CFG['variants']:
    if only and v['id'] not in only: continue
    row = dict(id=v['id'], error='')
    if not A.sheet_only:
        try: s = do_variant(v)
        except Exception as e:
            row['error'] = str(e)[:300]; print('FAIL', v['id'], row['error'], flush=True); rows.append(row); continue
    else:
        try: s = json.load(open(os.path.join(VROOT, v['id'], 'scores.json')))
        except Exception: row['error'] = 'no scores'; rows.append(row); continue
    s.pop('params', None); row.update(s); row['rank'] = rank_of(s); row['label'] = v.get('label', '')
    row['params'] = json.dumps(json.load(open(os.path.join(VROOT, v['id'], 'opt', 'best.json')))['best']['params'])
    rows.append(row); print('DONE %-14s rank %.3f iou %.3f dE %.1f jut %.3f' % (v['id'], row['rank'], row['iou_mean'], row.get('dE_mean') or -1, row.get('jut_side_frac', -1)), flush=True)
ok = sorted([r for r in rows if not r['error']], key=lambda r: -r['rank']); bad = [r for r in rows if r['error']]
cols = ['id', 'rank'] + sorted({k for r in ok for k in r} - {'id', 'rank', 'error', 'label', 'params'}) + ['label', 'params', 'error']
with open(os.path.join(RD, 'scores.csv'), 'w', newline='') as f:
    w = csv.DictWriter(f, cols, extrasaction='ignore'); w.writeheader()
    for r in ok + bad: w.writerow({k: (round(x, 4) if isinstance(x, float) else x) for k, x in r.items()})
json.dump(ok, open(os.path.join(RD, 'ranking.json'), 'w'), indent=1)
subprocess.run([T['tr_py'], os.path.join(HERE, 'sweep_sheet.py'), RD, T['refs'], '--top', str(CFG.get('top', 4))])
print('SWEEP_DONE %d ok, %d failed, %.0f s -> %s' % (len(ok), len(bad), time.time() - t0, RD))
