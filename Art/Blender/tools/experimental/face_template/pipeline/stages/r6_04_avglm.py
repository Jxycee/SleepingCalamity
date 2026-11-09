"""Average the mediapipe landmarks of several jittered front renders in mm and write them in the pixel frame of the primary render. usage: tag extra_tag1 extra_tag2 ..."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json, sys, numpy as np
F3 = _P_FIT + ''
def load(t):
    c = json.load(open(F3 + f'_cam_{t}.json')); l = json.load(open(F3 + f'lm_{t}.json')); P = np.array([l[str(i)] for i in range(len(l))])
    mm = np.stack([c['cx'] + (P[:, 0] / c['R'] - 0.5) * c['S'], c['cz'] + (0.5 - P[:, 1] / c['R']) * c['S']], 1); return c, mm
tags = sys.argv[1:]; c0, m0 = load(tags[0]); ms = [m0] + [load(t)[1] for t in tags[1:]]; m = np.mean(ms, 0)
print('landmark jitter (std over renders) mean %.3f mm max %.3f mm' % (np.std(ms, 0).mean() * 1e3, np.std(ms, 0).max() * 1e3))
px = np.stack([((m[:, 0] - c0['cx']) / c0['S'] + 0.5) * c0['R'], (0.5 - (m[:, 1] - c0['cz']) / c0['S']) * c0['R']], 1)
json.dump({str(i): px[i].tolist() for i in range(len(px))}, open(F3 + f'lm_{tags[0]}.json', 'w'))
