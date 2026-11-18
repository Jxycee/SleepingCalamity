# Build a blind scoring folder: anchors A01-A16 + our frames, normalised to 1280x720 q92, shuffled under X01.. names.
# usage: python make_set.py <out dir> <seed> <label=path> [<label=path> ...]
# Writes <out>/X##.jpg and <out>/../<outname>_key.txt (key stays with the controller, outside the folder).
import sys, os, random, glob
from PIL import Image

ANCH = r'C:\Dev\SleepingCalamity\Reference\private\judging-v2\anchors'
out, seed = sys.argv[1], int(sys.argv[2])
items = [(os.path.splitext(os.path.basename(p))[0], p) for p in sorted(glob.glob(os.path.join(ANCH, 'A*.jpg')))]
for a in sys.argv[3:]:
    lab, p = a.split('=', 1)
    items.append(('ours_' + lab, p))
random.Random(seed).shuffle(items)
os.makedirs(out, exist_ok=True)
key = []
for i, (lab, p) in enumerate(items, 1):
    im = Image.open(p).convert('RGB')
    if im.size != (1280, 720):
        sc = max(1280 / im.width, 720 / im.height)
        im = im.resize((round(im.width * sc), round(im.height * sc)), Image.LANCZOS)
        l, t = (im.width - 1280) // 2, (im.height - 720) // 2
        im = im.crop((l, t, l + 1280, t + 720))
    n = 'X%02d.jpg' % i
    im.save(os.path.join(out, n), quality=92)
    key.append('%s %s' % (n, lab))
open(os.path.normpath(out) + '_key.txt', 'w').write('\n'.join(key) + '\n')
print('\n'.join(key))
