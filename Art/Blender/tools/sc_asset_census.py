# sc_asset_census.py (2026-10-25, asset skills): audits every exported model of a region and ranks what needs work. Plain Python 3; runs Blender.
#
# Run:  python3 Art/Blender/tools/sc_asset_census.py [--region Millpond] [--blender <path>] [--out <dir>]
#   Audits Assets/_Project/Content/Models/<region>/{Props,Buildings,Trees,Rocks} with sc_asset_audit.py (class per folder), then prints:
#   gate FAILs, the darkest models (mean albedo value), the flattest vertex colour (no AO / variation), the most smeared shading, hygiene counts.
#   JSON per folder goes to --out (default: a temp dir). Use it at the start of a convergence session to pick the highest-value fixes.
#   The class is per model (class_of): the folder's, unless a name rule below says otherwise (asset duo, 2026-10-25), so animals, cover
#   plants, wall runs and outbuildings meet their own budgets instead of failing the prop or building ceiling. At most 2 Blenders at once.
import json, os, re, shutil, subprocess, sys, tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
CLASSES = {'Props': 'prop', 'Buildings': 'building', 'Trees': 'tree', 'Rocks': 'rock'}
NAME_CLASSES = [   # (regex on the file name without .fbx, class), first match wins
    (r'_Cards$', 'card'),
    (r'_Life_', 'creature'),
    (r'_Prop_(FlagIris|MarshMarigold|FloatingDebris)(_Flowers)?$', 'cover'),   # the margin plants' flowers split off (2026-10-25)
    (r'_Prop_(StoneWall|StoneWallTumbled|FenceRail|PathEdge|RaceWall)_Tri$', 'run'),
    (r'_Bldg_warden_hut', 'outbuilding'),
    (r'_FishLadder$', 'building'),             # a stone structure in the race, not a hand-sized prop
    (r'_Prop_FallenTree_Tri$', 'tree'),
]

KNOWN_DARK = [   # dark by design, reviewed (asset duo, 2026-10-25): left out of the Darkest list
    r'_Prop_Fingerpost_',                      # dark posts with cream boards (the landing fingerpost's look)
    r'_Bush_Evergreen_Tri$',                   # a 20-tri trunk stub under the cards
    r'_Bldg_boathouse_Tri$',                   # only the roof's dark underlay, seen through the gaps in the board roof (the boards are the base half)
]

def class_of(folder, name):
    for pat, cls in NAME_CLASSES:
        if re.search(pat, name):
            return cls
    return CLASSES[folder]

def opt(argv, name, default):
    if name in argv:
        i = argv.index(name); v = argv[i + 1]; del argv[i:i + 2]; return v
    return default

def main(argv):
    argv = list(argv)
    region = opt(argv, '--region', 'Millpond')
    blender = opt(argv, '--blender', shutil.which('blender') or os.path.expanduser('~/apps/blender-4.5.3-linux-x64/blender'))
    if not os.path.exists(blender) and os.path.exists('/root/apps/blender-4.5.3-linux-x64/blender'):
        blender = '/root/apps/blender-4.5.3-linux-x64/blender'
    out = opt(argv, '--out', tempfile.mkdtemp(prefix='fgcensus_'))
    os.makedirs(out, exist_ok=True)
    base = os.path.join(ROOT, 'Assets', '_Project', 'Content', 'Models', region)
    audit = os.path.join(ROOT, 'Art', 'Blender', 'tools', 'sc_asset_audit.py')
    groups, rows = {}, []
    for folder in CLASSES:
        d = os.path.join(base, folder)
        if not os.path.isdir(d): continue
        for f in sorted(os.listdir(d)):
            if f.lower().endswith('.fbx'):
                groups.setdefault((folder, class_of(folder, f[:-4])), []).append(os.path.join(d, f))
    jobs = sorted(groups.items())
    for i in range(0, len(jobs), 2):
        procs = []
        for (folder, cls), fbx in jobs[i:i + 2]:
            j = os.path.join(out, '%s_%s.json' % (folder, cls))
            procs.append((j, subprocess.Popen([blender, '-b', '--factory-startup', '--python', audit, '--', j, '--class', cls] + fbx,
                                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)))
        for j, p in procs:
            p.wait()
            if os.path.exists(j): rows += json.load(open(j))
    print('# %s census: %d models, %d FAIL' % (region, len(rows), sum(1 for r in rows if not r['pass'])))
    print('\n## Gate FAILs')
    for r in rows:
        if not r['pass']: print('- %s: %s' % (r['file'], '; '.join(r['FAIL'])))
    print('\n## Darkest (mean albedo value, sRGB; shipped median ~0.35-0.45)')
    for r in sorted([r for r in rows if r.get('albedo_value') and not any(re.search(p, r['file'][:-4]) for p in KNOWN_DARK)],
                    key=lambda r: r['albedo_value'][1])[:12]:
        print('- %s %.2f' % (r['file'], r['albedo_value'][1]))
    print('\n## Flattest vertex colour (no AO / patch variation; tris > 200)')
    for r in sorted([r for r in rows if 'col_std' in r and r['tris'] > 200], key=lambda r: r['col_std'])[:10]:
        print('- %s std %.4f' % (r['file'], r['col_std']))
    print('\n## Most smeared shading (share of corner normals > 45 deg off their face)')
    for r in sorted(rows, key=lambda r: -r.get('smear_share', 0))[:8]:
        print('- %s %.2f' % (r['file'], r['smear_share']))
    print('\n## Hygiene: %d models with loose verts, %d with degenerate faces, %d with non-manifold edges' % (
        sum(1 for r in rows if r['loose']), sum(1 for r in rows if r['degenerate']), sum(1 for r in rows if r['nonmanifold'])))
    nh = [r for r in rows if any('no headroom' in w for w in r['WARN'])]
    print('\n## No headroom: %d models (Col p90 near 1, so edge wear cannot lighten them)' % len(nh))
    print('- known, by design: the legacy kit constants B.GREY_TIMBER and TEAL on Wood carry most of these (no paint pass runs on shipped\n'
          '  models; coordinator 2026-10-25). New work takes the Rock rows of sc-asset-craft\'s materials table (section 2) instead.')
    print('\nJSON: ' + out)

if __name__ == '__main__':
    main(sys.argv[1:])
