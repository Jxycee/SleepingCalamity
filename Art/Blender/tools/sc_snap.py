"""[Ported from FishGame, M8.] Snapshot a baked prop (its .blend AND its baked textures) under a tag, or restore one.
python snap.py save|restore|list <props_dir> <Name> [tag]
A prop .blend references tex/<Name>_* by path, so copying only the .blend is not a baseline: the next bake
overwrites the textures and the "old" copy renders with the new maps (a copper A/B compared v2 with itself).
Snapshots live in <props_dir>/snap/<tag>/."""
import sys, os, shutil, glob

cmd, pdir, name = sys.argv[1], sys.argv[2], sys.argv[3]
tag = sys.argv[4] if len(sys.argv) > 4 else None
sdir = os.path.join(pdir, 'snap')
if cmd == 'list':
    for t in sorted(os.listdir(sdir)) if os.path.isdir(sdir) else []:
        if os.path.exists(os.path.join(sdir, t, name + '.blend')): print(t)
    sys.exit(0)
d = os.path.join(sdir, tag)
files = [os.path.join(pdir, name + '.blend')] + glob.glob(os.path.join(pdir, 'tex', name + '_*'))
if cmd == 'save':
    os.makedirs(os.path.join(d, 'tex'), exist_ok=True)
    for f in files:
        shutil.copy2(f, os.path.join(d, os.path.relpath(f, pdir)))
    print('saved', len(files), 'files ->', d)
elif cmd == 'restore':
    src = [os.path.join(d, name + '.blend')] + glob.glob(os.path.join(d, 'tex', name + '_*'))
    if not os.path.exists(src[0]): sys.exit(f'no snapshot {tag} for {name}')
    for f in src:
        shutil.copy2(f, os.path.join(pdir, os.path.relpath(f, d)))
    print('restored', len(src), 'files <-', d)
else:
    sys.exit(__doc__)
