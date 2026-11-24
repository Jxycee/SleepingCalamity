"""Compose hairBuilder.ts from per-archetype block files (blocks/<id>.ts). Lock-protected.
Usage: python C:\Dev\scratch\blocks\compose.py   (edit ONLY your own blocks/<id>.ts, then run this).
Each block file holds the full `if (arch === '<id>') { ... return specs; }` branch, 2-space indented, LF endings."""
import os, sys, time, glob, re
HB = r'C:\Dev\scratch\AetherStrand\src\engine\hairBuilder.ts'
BL = r'C:\Dev\scratch\blocks'
LOCK = os.path.join(BL, '.lock')
for _ in range(600):
    try:
        fd = os.open(LOCK, os.O_CREAT | os.O_EXCL | os.O_WRONLY); os.close(fd); break
    except FileExistsError:
        if time.time() - os.path.getmtime(LOCK) > 60: os.remove(LOCK)
        time.sleep(0.1)
try:
    s = open(HB, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r\n' in s[:5000] else '\n'
    done = []
    for f in sorted(glob.glob(os.path.join(BL, '*.ts'))):
        aid = os.path.basename(f)[:-3]
        body = open(f, encoding='utf-8', newline='').read().replace('\r\n', '\n').rstrip('\n')
        new = ('  // ARCH-BEGIN %s\n%s\n  // ARCH-END %s\n' % (aid, body, aid)).replace('\n', nl)
        b0 = '  // ARCH-BEGIN %s' % aid; b1 = '  // ARCH-END %s' % aid
        if b0 in s:
            a = s.index(b0); b = s.index(b1, a) + len(b1)
            if s[b:b+len(nl)] == nl: b += len(nl)
            s = s[:a] + new + s[b:]
        else:
            m = re.search(r'(?m)^  // ZONE A:', s)
            assert m, 'ZONE A anchor missing'
            s = s[:m.start()] + new + nl + s[m.start():]
        done.append(aid)
    open(HB, 'w', encoding='utf-8', newline='').write(s)
    print('composed', ' '.join(done))
finally:
    try: os.remove(LOCK)
    except OSError: pass
