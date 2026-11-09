"""Comparison strip out4/r2_r3_r4.png (venv python: PIL): row 1 photo | r2 | r3 | r4 front neutral (Cycles cells); rows 2-3 clay 3/4 and side (Workbench, r4_sheet.py -> fit4/clay_r234.png);
rows 4-5 mouth crops jawOpen 0.5 and aa for r2 | r3 | r4 (cells of out/, out3/, out4/)."""
from PIL import Image, ImageDraw
D2 = 'C:/AI/test_out/heroes/face_spike2/'; W = 560
def L(p, h=W):
    im = Image.open(p).convert('RGB'); return im.resize((int(im.width * h / im.height), h))
def row(ims, labs):
    h = max(i.height for i in ims); r = Image.new('RGB', (sum(i.width for i in ims) + 6 * len(ims), h), (25, 25, 25)); x = 0
    for i, lb in zip(ims, labs): r.paste(i, (x, 0)); ImageDraw.Draw(r).text((x + 6, 4), lb, fill=(255, 220, 120)); x += i.width + 6
    return r
cl = Image.open(D2 + 'fit4/clay_r234.png'); cw = cl.width // 3
def clay(i, j): return cl.crop((j * cw, i * cw, (j + 1) * cw, (i + 1) * cw)).resize((W, W))
ph = Image.open(D2 + 'target/crop_src.png').convert('RGB'); ph = ph.resize((int(ph.width * W / ph.height), W))
rows = [row([ph, L(D2 + 'out/cells/front_neutral_full.png'), L(D2 + 'out3/cells/front_neutral_full.png'), L(D2 + 'out4/cells/front_neutral_full.png')], ['photo', 'r2 front', 'r3 front', 'r4 front']),
        row([clay(0, 1), clay(1, 1), clay(2, 1)], ['r2 3/4 clay', 'r3 3/4 clay', 'r4 3/4 clay']), row([clay(0, 2), clay(1, 2), clay(2, 2)], ['r2 side clay', 'r3 side clay', 'r4 side clay'])]
for nm in ('jawOpen', 'aa'):
    rows.append(row([L(D2 + f'{d}/cells/mouth_{nm}_full.png', 300) for d in ('out', 'out3', 'out4')], [f'r2 {nm}', f'r3 {nm}', f'r4 {nm}']))
Wd = max(r.width for r in rows); im = Image.new('RGB', (Wd, sum(r.height for r in rows)), (25, 25, 25)); y = 0
for r in rows: im.paste(r, (0, y)); y += r.height
im.save(D2 + 'out4/r2_r3_r4.png'); print(im.size)
