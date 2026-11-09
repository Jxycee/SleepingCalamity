"""Compose the proof sheets from out/cells (venv python: PIL). Writes out/face2_proof_sheet.png (2048 wide) and out/face2_closeups.png."""
import json
from PIL import Image, ImageDraw, ImageFont
D = 'C:/AI/test_out/heroes/face_spike/'; C = D + 'out/cells/'
rep = json.load(open(D + 'out/build_report.json'))
try: F = ImageFont.truetype('arial.ttf', 26); Fs = ImageFont.truetype('arial.ttf', 20)
except Exception: F = Fs = ImageFont.load_default()
BG = (28, 28, 30)
def cell(name, w, h):
    try: im = Image.open(C + name + '.png').convert('RGB')
    except FileNotFoundError: return None
    return im.resize((w, h), Image.LANCZOS)
def sheet(path, rows, cols, W, cw, ch, foot, label_w=150):
    top = 70; fh = 60
    S = Image.new('RGB', (W, top + len(rows) * ch + fh), BG); d = ImageDraw.Draw(S)
    for ci, (cname, ctitle) in enumerate(cols): d.text((label_w + ci * cw + 10, 20), ctitle, fill=(240, 240, 240), font=F)
    for ri, (view, row, rlabel) in enumerate(rows):
        y = top + ri * ch; d.text((10, y + ch // 2 - 14), rlabel, fill=(255, 220, 120), font=F)
        for ci, (cname, ctitle) in enumerate(cols):
            if cname == 'target' and row != 'neutral': im = None
            else: im = cell(f'{view}_{row}_{cname}', cw, ch)
            if cname == 'tpl_or_target': im = cell(f'{view}_{row}_' + ('target' if row == 'neutral' else 'tpl'), cw, ch)
            if im is not None: S.paste(im, (label_w + ci * cw, y))
    for i, line in enumerate(foot): d.text((10, top + len(rows) * ch + 8 + i * 24), line, fill=(200, 200, 200), font=Fs)
    S.save(path); print(path, S.size)
foot = ['verts/faces/keys  head 7321/14212/150  lashes 6300/4900/68  eyeL,eyeR 512/791/4  teethLower 2511/3464/21  teethUpper 2497/3654/2 : identical to template, UV loops array_equal, no NaN (invariants_all_ok=%s)' % rep['invariants_all_ok']]
cols = [('tpl_or_target', 'target (neutral) / Tobin template (expr rows)'), ('slider', 'slider-only result'), ('full', 'full wrap result')]
rows = [('front', 'neutral', 'neutral'), ('front', 'blink', 'eyeBlink L+R 1.0'), ('front', 'jawOpen', 'jawOpen 0.5'), ('front', 'aa', 'viseme aa'),
        ('front', 'oh', 'viseme oh'), ('front', 'smile', 'smile L+R 1.0'), ('q34', 'neutral', 'neutral 3/4')]
sheet(D + 'out/face2_proof_sheet.png', rows, cols, 2048, 619, 619, foot, label_w=191)
cols2 = [('target', 'target'), ('tpl', 'Tobin template'), ('slider', 'slider-only'), ('full', 'full wrap')]
rows2 = [('eyes', 'neutral', 'eyes open'), ('eyes', 'blink', 'eyes blink'), ('mouth', 'neutral', 'mouth neutral'), ('mouth', 'jawOpen', 'jawOpen 0.5'),
         ('mouth', 'aa', 'aa'), ('mouth', 'oh', 'oh'), ('mouth', 'smile', 'smile')]
sheet(D + 'out/face2_closeups.png', rows2, cols2, 2048, 475, 264, foot, label_w=148)
