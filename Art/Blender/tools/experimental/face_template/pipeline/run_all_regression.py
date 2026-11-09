"""Regression gate for the face pipeline: reruns the configured characters (stages whose script/param/input hash changed re-run, the rest are skipped; --force-from STAGE forces a rerun
from that stage) and prints one scorecard table plus a PASS/FAIL summary. Exit code 0 only if every character passes every criterion and `status` shows no stale stage.

  python run_all_regression.py                       # char2_zero char3b char4 cloud_v1, 4 in parallel (CPU), then the table
  python run_all_regression.py --force-from align    # from-scratch rerun of everything after prep / Hunyuan
  python run_all_regression.py --only char2_zero char3b --serial
  python run_all_regression.py --score-only          # just print the table from the existing score.json files
  python run_all_regression.py --expect-fail cloud_v1   # known-failing characters do not fail the gate (still printed, marked KNOWN)
Logs: <out_dir>/../<name>_regress.log. A GPU stage (Hunyuan) only runs for a character without a pinned shape / cached seeds; check nvidia-smi before running new characters in parallel."""
import argparse, json, os, subprocess, sys, time
HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
DEFAULT = ['char2_zero', 'char3b', 'char4', 'cloud_v1']
KEYS = [('ratio_mae', 'ratio MAE'), ('ratio_worst', 'worst ratio'), ('iou_head_jaw', 'IoU head+jaw'), ('iou_skull', 'IoU skull'), ('skin_rms_3d', '3D skin RMS'), ('skin_dE76', 'skin dE76'), ('flips', 'flips'), ('invariants', 'invariants'), ('head_keys', 'keys')]

def cfg_path(n): return os.path.join(HERE, 'configs', n + '.json')
def load(n):
    from face_pipeline import load_cfg
    return load_cfg(cfg_path(n))
def stale(n):
    r = subprocess.run([sys.executable, os.path.join(HERE, 'face_pipeline.py'), 'status', '--config', cfg_path(n)], capture_output=True, text=True)
    return [l.split()[0] for l in r.stdout.splitlines() if len(l.split()) > 1 and l.split()[1] in ('stale', 'failed', 'pending', 'running')]
def table(names, known):
    rows = {}; ok_all = True
    for n in names:
        p = os.path.join(load(n)['out_dir'], 'out', 'score.json')
        rows[n] = json.load(open(p))['criteria'] if os.path.exists(p) else None
    crit = []
    for n in names:
        if rows[n]:
            for k in rows[n]:
                if k not in crit: crit.append(k)
    print('\n%-24s %-10s ' % ('criterion', 'limit') + ' '.join('%-16s' % n for n in names))
    for k in crit:
        lim = next((('%s %s' % (rows[n][k]['op'], rows[n][k]['threshold'])) for n in names if rows[n] and k in rows[n]), '')
        cells = []
        for n in names:
            v = rows[n].get(k) if rows[n] else None
            if v is None: cells.append('%-16s' % 'n/a'); continue
            val = '%.3f' % v['value'] if isinstance(v['value'], float) else str(v['value'])
            cells.append('%-16s' % ('%s %s' % (val, 'ok' if v['pass'] else 'FAIL')))
        print('%-24s %-10s ' % (k, lim) + ' '.join(cells))
    print('\nsummary')
    for n in names:
        fails = [k for k, v in (rows[n] or {}).items() if not v['pass']]
        tag = 'PASS' if rows[n] and not fails else ('KNOWN-FAIL' if n in known and rows[n] else 'FAIL')
        if tag == 'FAIL': ok_all = False
        print('  %-12s %-10s %s' % (n, tag, ('failing: ' + ', '.join(fails)) if fails else ('no score' if not rows[n] else '')))
    return ok_all

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--only', nargs='+'); ap.add_argument('--force-from', choices=None); ap.add_argument('--serial', action='store_true'); ap.add_argument('--score-only', action='store_true')
    ap.add_argument('--expect-fail', nargs='*', default=[]); ap.add_argument('--to'); a = ap.parse_args()
    names = a.only or DEFAULT
    if not a.score_only:
        procs = []; t0 = time.time()
        env = dict(os.environ); env.setdefault('SCP_JAC_WORKERS', str(max(2, ((os.cpu_count() or 8) - 2) // (1 if a.serial else len(names)))))   # Jacobian pool size per pipeline: share the cores between the parallel runs
        for n in names:
            cmd = [sys.executable, os.path.join(HERE, 'face_pipeline.py'), 'run', '--config', cfg_path(n)] + (['--force', a.force_from] if a.force_from else []) + (['--to', a.to] if a.to else [])
            od = load(n)['out_dir']; os.makedirs(os.path.dirname(od.rstrip('/')), exist_ok=True); log = open(od.rstrip('/') + '_regress.log', 'w')
            p = subprocess.Popen(cmd, stdout=log, stderr=subprocess.STDOUT, env=env); procs.append((n, p, log))
            if a.serial: p.wait()
        for n, p, log in procs:
            p.wait(); log.close(); print('%-12s exit %d (%.0f s elapsed)' % (n, p.returncode, time.time() - t0))
    ok = table(names, set(a.expect_fail))
    st = {n: stale(n) for n in names}
    for n, s in st.items():
        if s: print('  status %-12s not clean: %s' % (n, ', '.join(s))); ok = False
    print('\nREGRESSION %s' % ('PASS' if ok else 'FAIL')); sys.exit(0 if ok else 1)

if __name__ == '__main__': main()
