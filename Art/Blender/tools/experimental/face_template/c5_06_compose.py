"""Compose round-2 proof sheets from out/cells (venv python: PIL). Writes out/cloud5_proof_sheet.png (2048 wide), out/cloud5_closeups.png, out/cloud5_overlay.png."""
import json
from PIL import Image, ImageDraw, ImageFont
D2 = 'C:/AI/test_out/heroes/face_spike2/'; C = D2 + 'out5/cells/'; O = D2 + 'out5/'
rep = json.load(open(O + 'build_report.json'))
try: F = ImageFont.truetype('arial.ttf', 26); Fs = ImageFont.truetype('arial.ttf', 20)
except Exception: F = Fs = ImageFont.load_default()
BG = (28, 28, 30)
photo = Image.open(D2 + 'target/crop_src.png').convert('RGB')
def cell(name, w, h):
    if name == 'PHOTO':
        sq = photo.crop((0, 0, 670, 670)).resize((w, h), Image.LANCZOS); return sq
    try: im = Image.open(C + name + '.png').convert('RGB')
    except FileNotFoundError: return None
    return im.resize((w, h), Image.LANCZOS)
def sheet(path, rows, cols, W, cw, ch, foot, label_w, photo_first=False):
    top = 70; fh = 60
    S = Image.new('RGB', (W, top + len(rows) * ch + fh), BG); d = ImageDraw.Draw(S)
    for ci, (cname, ctitle) in enumerate(cols): d.text((label_w + ci * cw + 10, 20), ctitle, fill=(240, 240, 240), font=F)
    for ri, (view, row, rlabel) in enumerate(rows):
        y = top + ri * ch; d.text((10, y + ch // 2 - 14), rlabel, fill=(255, 220, 120), font=F)
        for ci, (cname, ctitle) in enumerate(cols):
            if cname == 'ref': im = cell('PHOTO', cw, ch) if (photo_first and ri == 0) else cell(f'{view}_{row}_tpl', cw, ch)
            else: im = cell(f'{view}_{row}_{cname}', cw, ch)
            if im is not None: S.paste(im, (label_w + ci * cw, y))
    for i, line in enumerate(foot): d.text((10, top + len(rows) * ch + 8 + i * 24), line, fill=(200, 200, 200), font=Fs)
    S.save(path); print(path, S.size)
foot = ['head 7321v/14212f/150 keys  lashes 6300/4900/68  eyeL,eyeR 512/791/4  teethLower 2511/3464/21  teethUpper 2497/3654/2 : identical to template, UV loops array_equal, no NaN (invariants_all_ok=%s)' % rep['invariants_all_ok']]
cols = [('ref', 'reference: photo (row 1) / Tobin template'), ('slider', 'slider-only result'), ('full', 'full wrap result')]
rows = [('front', 'neutral', 'neutral'), ('front', 'blink', 'eyeBlink L+R 1.0'), ('front', 'jawOpen', 'jawOpen 0.5'), ('front', 'aa', 'viseme aa'),
        ('front', 'oh', 'viseme oh'), ('front', 'smile', 'smile L+R 1.0'), ('q34', 'neutral', 'neutral 3/4')]
sheet(O + 'cloud5_proof_sheet.png', rows, cols, 2048, 619, 619, foot, 191, photo_first=True)
cols2 = [('tpl', 'Tobin template'), ('slider', 'slider-only'), ('full', 'full wrap')]
rows2 = [('eyes', 'neutral', 'eyes open'), ('eyes', 'blink', 'eyes blink'), ('mouth', 'neutral', 'mouth neutral'), ('mouth', 'jawOpen', 'jawOpen 0.5'),
         ('mouth', 'aa', 'aa'), ('mouth', 'oh', 'oh'), ('mouth', 'smile', 'smile')]
sheet(O + 'cloud5_closeups.png', rows2, cols2, 2048, 620, 344, foot, 188)
# overlay: photo | render | 50% blend
r = Image.open(C + 'overlay_full.png').convert('RGBA'); bg = photo.convert('RGBA')
solid = Image.new('RGBA', r.size, (70, 70, 74, 255)); solid.alpha_composite(r)
half = bg.copy(); rr = r.copy(); a = rr.getchannel('A').point(lambda v: v // 2); rr.putalpha(a); half.alpha_composite(rr)
full_ov = bg.copy(); full_ov.alpha_composite(r)
S = Image.new('RGB', (670 * 4, 710 + 50), BG); d = ImageDraw.Draw(S)
for i, (im, t) in enumerate(((photo, 'photo'), (solid, 'full result, photo framing'), (full_ov, 'result over photo (100%)'), (half, '50% blend'))):
    S.paste(im.convert('RGB'), (i * 670, 50)); d.text((i * 670 + 10, 12), t, fill=(240, 240, 240), font=F)
S.save(O + 'cloud5_overlay.png'); print('overlay', S.size)
