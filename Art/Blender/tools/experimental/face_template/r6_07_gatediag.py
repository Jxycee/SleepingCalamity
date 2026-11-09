"""Diagnostic: which feasibility gate does each slider column break at amplitude a (+ and -)?  usage: r6_07_gatediag.py <the same args as r6_06_sliderloop> """
import sys
src = open(__file__.replace('r6_07_gatediag', 'r6_06_sliderloop')).read().split("th = np.zeros(n)\nif W.A.seed")[0]
exec(compile(src, 'r6_06_head', 'exec'))
def why(th):
    P = W.ysnap(W.warp(th)); G = W.gates(P); out = []
    for k, v in G.items():
        if k.startswith('blink_'):
            b0 = _G0[k]
            if v['pen_moved_mm'] > max(v['pen_template_mm'] + PENTOL, b0['pen_moved_mm']) + 0.03: out.append('%s pen %.2f>%.2f' % (k, v['pen_moved_mm'], max(v['pen_template_mm'] + 0.25, b0['pen_moved_mm'])))
            if v['gap_max_mm'] > max(v['tpl_gap_max_mm'] + 0.5, b0['gap_max_mm']) + 0.03: out.append('%s gapmax %.2f' % (k, v['gap_max_mm']))
            if v['gap_mean_mm'] > max(v['tpl_gap_mean_mm'] + 0.5, b0['gap_mean_mm']) + 0.03: out.append('%s gapmean %.2f>%.2f' % (k, v['gap_mean_mm'], max(v['tpl_gap_mean_mm'] + 0.5, b0['gap_mean_mm'])))
        if k.startswith('lip_') and v['min_gap_mm'] < -0.3: out.append('%s lipgap %.2f' % (k, v['min_gap_mm']))
    for k, v in G['rings'].items():
        if v['min_ratio'] < 0.7 or v['max_ratio'] > 1.4 or v['flips'] > 0: out.append('ring %s %.2f-%.2f flips %d' % (k, v['min_ratio'], v['max_ratio'], v['flips']))
    if W.flips_of(P) > _F0: out.append('flips %d>%d' % (W.flips_of(P), _F0))
    return out
print('base', why(np.zeros(n)))
for j, c in enumerate(COLS):
    for a in (0.1, 0.5, -0.5):
        th = np.zeros(n); th[nw + j] = a; w = why(th)
        if w: print(c[0], a, w, flush=True)
G = W.gates(W.ysnap(W.warp(np.zeros(n)))); print({k: (round(v['pen_moved_mm'], 2), round(v['pen_template_mm'], 2), round(v['gap_mean_mm'], 2), round(v['tpl_gap_mean_mm'], 2)) for k, v in G.items() if k.startswith('blink_')}, PENTOL)
