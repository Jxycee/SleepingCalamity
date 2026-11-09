# facekeys env PERSISTENT landmark worker: one JSON request per stdin line {out: result.json, jit: 0|5, paths: [...]} -> writes {path: landmarks} to `out`, answers '@@DONE <n>' (or '@@ERR <text>').
# Same detector code as the one-shot r6_01c_batch_lm.py (lm_detect.py); the model loads once. Closing stdin ends the worker.
import sys, os, json, traceback
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lm_detect
D = lm_detect.Detector(); print('@@READY', flush=True)
for line in sys.stdin:
    try:
        q = json.loads(line); json.dump(D.detect_files(q['paths'], q['jit']), open(q['out'], 'w')); print('@@DONE', len(q['paths']), flush=True)
    except Exception: print('@@ERR', traceback.format_exc().replace('\n', ' | '), flush=True)
