"""Strip out6/r4_r5.png (venv python: PIL): row 1 photo | r4 front | r5 front (Cycles cells); row 2 clay 3/4 and row 3 side (fit6/clay_r45.png from r6_sheet.py: rows r4, r5);
rows 4-5: jawOpen 0.5 and aa mouth crops for r4 | r5 (cells of out4/, out6/)."""
from PIL import Image, ImageDraw
D2 = 'C:/AI/test_out/heroes/face_spike2/'; W = 560
def L(p, h=W):
    im = Image.open(p).convert('RGB'); return im.resize((int(im.width * h / im.height), h))
def row(ims, labs):
    h = max(i.height for i in ims); r = Image.new('RGB', (sum(i.width for i in ims) + 6 * len(ims), h), (25, 25, 25)); x = 0
    for i, lb in zip(ims, labs): r.paste(i, (x, 0)); ImageDraw.Draw(r).text((x + 6, 4), lb, fill=(255, 220, 120)); x += i.width + 6
    return r
cl = Image.open(D2 + 'fit6/clay_r45.png'); cw = cl.width // 3
def clay(i, j): return cl.crop((j * cw, i * cw, (j + 1) * cw, (i + 1) * cw)).resize((W, W))
ph = Image.open(D2 + 'target/crop_src.png').convert('RGB'); ph = ph.resize((int(ph.width * W / ph.height), W))
rows = [row([ph, L(D2 + 'out4/cells/front_neutral_full.png'), L(D2 + 'out6/cells/front_neutral_full.png')], ['photo', 'r4 front', 'r5 front']),
        row([clay(0, 1), clay(1, 1)], ['r4 3/4 clay', 'r5 3/4 clay']), row([clay(0, 2), clay(1, 2)], ['r4 side clay', 'r5 side clay'])]
for nm in ('jawOpen', 'aa', 'oh'):
    rows.append(row([L(D2 + f'out{v}/cells/mouth_{nm}_full.png', 330) for v in (4, 5)], [f'r4 {nm}', f'r5 {nm}']))
Wd = max(r.width for r in rows); im = Image.new('RGB', (Wd, sum(r.height for r in rows)), (25, 25, 25)); y = 0
for r in rows: im.paste(r, (0, y)); y += r.height
im.save(D2 + 'out6/r4_r5.png'); print(im.size)
