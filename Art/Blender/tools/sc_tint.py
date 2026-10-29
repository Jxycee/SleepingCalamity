# sc_tint.py (2026-10-25, asset skills): the vertex-colour tint for a target colour, and the colour a tint gives. Plain Python 3, no Blender.
#
# The game draws albedo = lin(palette[slot]) x 2 x Col with Col = 0.5 x ao x tint (Toon.shader, _JitterEncoded 1; PaletteGlobals linearises the
# palette), so   albedo_linear = lin(slot) x ao x tint   and   tint = lin(target) / (lin(slot) x ao).   Col is clamped to 1, so tint x ao <= 2.
#
# Run:  python3 Art/Blender/tools/sc_tint.py tint  <#rrggbb> [--slot Wood|3] [--prefer Wood] [--lit [1.03]] [--ao 0.8] [--season summer]
#         -> the tint per slot (best first). --lit for README / painting hexes (they are lit results, not albedo). --prefer the material's own slot.
#       python3 Art/Blender/tools/sc_tint.py color <slot> <r,g,b tint> [--ao 0.8]                         -> the sRGB hex it renders (lit, no grade)
# With no --slot, `tint` ranks the permanent slots by how little the tint must bend the slot (closest to a grey tint, never clipping).
# --seasonal (leaves, moss, reeds) ranks the seasonal slots instead, the tint fitted to --season, and shows the colour that tint gives in every
# season (spring / summer / autumn / winter), so one call picks a slot that is right in summer and reads in autumn.
import math, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
NAMES = ['Grass', 'Foliage', 'FoliageDark', 'Wood', 'WoodDark', 'Sand', 'Rock', 'Reed', 'Dock', 'Roof', 'Accent', 'Snow', 'PineLight', 'PineDark',
         'Blossom', 'FoliageAlt']
SEASONAL = {0, 1, 2, 7, 14, 15}
PERMANENT = [3, 4, 5, 6, 8, 9, 10, 11]

def palette(season='Summer'):
    # Assets/_Project/Content/Art/ArtPalette.asset: seasons[].colors[16] (sRGB)
    src = open(os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Art', 'ArtPalette.asset'), encoding='utf-8').read()
    blocks = re.split(r'\n  - name: ', src)
    for b in blocks[1:]:
        if b.split('\n', 1)[0].strip().lower() == season.lower():
            cols = re.findall(r'\{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: [\d.]+\}', b)
            return [tuple(float(x) for x in c) for c in cols[:16]]
    raise SystemExit('season not found: ' + season)

def lin(c):
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)

def srgb(c):
    return tuple(12.92 * x if x <= 0.0031308 else 1.055 * x ** (1 / 2.4) - 0.055 for x in c)

def hexc(c):
    return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(255 * x)))) for x in c)

def parse_hex(h):
    h = h.lstrip('#'); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))

def slot_index(s):
    return int(s) if s.isdigit() else NAMES.index(s)

def opt(argv, name, default):
    if name in argv:
        i = argv.index(name); v = argv[i + 1]; del argv[i:i + 2]; return v
    return default

def main(argv):
    argv = list(argv)
    if not argv or argv[0] in ('-h', '--help') or argv[0] not in ('tint', 'color'):
        print(''.join(open(__file__, encoding='utf-8').readlines()[2:12]), end='')   # the header's formula and Run lines
        sys.exit(0 if argv and argv[0] in ('-h', '--help') else 2)
    ao = float(opt(argv, '--ao', '0.8')); season = opt(argv, '--season', 'Summer'); slot = opt(argv, '--slot', None)
    # --lit: the hex is a LIT colour (a concept README value, a painting pick). Calibrated 2026-10-25 by rendering known tints through
    # sc_asset_preview.py: a front-lit face renders at about 1.0x its albedo in value, a top face about 1.05x, a shade face about 0.8x, all a little
    # cooler (sky fill). So README hexes are close to albedo already: --lit (bare) divides by 1.03; pass a number to override.
    lit = 1.0
    if '--lit' in argv:
        i = argv.index('--lit')
        nxt = argv[i + 1] if i + 1 < len(argv) else ''
        try:
            lit = float(nxt); del argv[i:i + 2]
        except ValueError:
            lit = 1.03; del argv[i:i + 1]
    prefer = opt(argv, '--prefer', None)
    pal = palette(season.capitalize())
    if argv[0] == 'tint':
        target = tuple(x / lit for x in lin(parse_hex(argv[1])))
        seasonal = '--seasonal' in argv
        slots = [slot_index(slot)] if slot else (sorted(SEASONAL) if seasonal else PERMANENT)
        rows = []
        for j in slots:
            sl = lin(pal[j])
            t = tuple(target[i] / max(sl[i] * ao, 1e-4) for i in range(3))
            clip = max(t) * ao > 2.0
            bend = max(t) / max(min(t), 1e-4)   # 1 = a pure grey tint (the slot's hue as is)
            rows.append((clip, bend, j, t))
        rows.sort()
        if prefer:   # the material's own slot first, always (round 34: a clipping preferred slot sank down the list and was missed)
            pj = slot_index(prefer)
            if pj not in [r[2] for r in rows]:
                sl = lin(pal[pj]); t = tuple(target[i] / max(sl[i] * ao, 1e-4) for i in range(3))
                rows.append((max(t) * ao > 2.0, max(t) / max(min(t), 1e-4), pj, t))
            rows.sort(key=lambda r: (r[2] != pj, r[0]))   # first even when it clips (its CLIPS note says to pick another), then the rest
        print('target %s at ao %.2f (%s)%s' % (argv[1], ao, season, ' lit /%.2f' % lit if lit != 1 else ''))
        for clip, bend, j, t in rows:
            note = 'CLIPS: Col > 1, pick a lighter slot' if clip else ('seasonal slot!' if j in SEASONAL else '')
            if seasonal:
                note = 'CLIPS' if clip else ''
            print('  slot %2d %-11s tint (%.2f, %.2f, %.2f)  bend %.1f  %s' % (j, NAMES[j], t[0], t[1], t[2], bend, note))
            if seasonal:   # what that tint renders in each season (lit albedo at the same ao)
                cols = []
                for sn in ('Spring', 'Summer', 'Autumn', 'Winter'):
                    sl2 = lin(palette(sn)[j])
                    cols.append('%s %s' % (sn.lower()[:3], hexc(srgb([min(1.0, 0.5 * ao * t[i]) * 2 * sl2[i] for i in range(3)]))))
                print('        ' + '  '.join(cols))
    elif argv[0] == 'color':
        j = slot_index(argv[1]); t = tuple(float(x) for x in argv[2].split(','))
        sl = lin(pal[j])
        col = [min(1.0, 0.5 * ao * t[i]) * 2 * sl[i] for i in range(3)]
        print('%s on %s at ao %.2f -> %s (lit albedo, before light and grade)%s' % (argv[2], NAMES[j], ao, hexc(srgb(col)),
              '  [clamped]' if max(0.5 * ao * x for x in t) > 1 else ''))
    else:
        raise SystemExit(__doc__)

if __name__ == '__main__':
    try:
        main(sys.argv[1:])
        sys.stdout.flush()
    except BrokenPipeError:   # `| head` closed the pipe: stop quietly
        os.dup2(os.open(os.devnull, os.O_WRONLY), sys.stdout.fileno())
        sys.exit(0)
