import numpy as np
from PIL import Image, ImageDraw

archs = ["layered-shag", "spiky-hero", "long-cascade", "wolf-cut", "sleek-bob", "asym-cascade",
         "dolphin-long", "ribbon-halfup", "swept-back", "high-ponytail", "twin-tails", "emo-sweep"]
BG = np.array([58, 59, 63], float)


def fg(a):
    return np.abs(a - BG).max(axis=-1) > 14


rows = []
sheet = Image.new('RGB', (2 * 3 * 340, 4 * 340 + 0), (20, 20, 20))
for n, a in enumerate(archs):
    w = Image.open('C:/Dev/scratch/web_out/batch_%s_web.png' % a).convert('RGB')
    u = Image.open('C:/Dev/scratch/parity_out/batch_%s_4view.png' % a).convert('RGB')
    ious, maes = [], []
    for (x, y) in [(0, 0), (512, 0), (0, 512), (512, 512)]:
        aw = np.array(w.crop((x, y, x + 512, y + 512))).astype(float)
        au = np.array(u.crop((x, y, x + 512, y + 512))).astype(float)
        mw, mu = fg(aw), fg(au)
        ious.append((mw & mu).sum() / max(1, (mw | mu).sum()))
        maes.append(np.abs(aw - au)[mw | mu].mean())
    print('%-14s IoU %.3f  MAE %.1f' % (a, np.mean(ious), np.mean(maes)))
    # contact: front web | front unity (512 crops scaled to 170) per style
    fw = w.crop((0, 0, 512, 512)).resize((170, 170)); fu = u.crop((0, 0, 512, 512)).resize((170, 170))
    bw = w.crop((512, 0, 1024, 512)).resize((170, 170)); bu = u.crop((512, 0, 1024, 512)).resize((170, 170))
    cx = (n % 3) * 680; cy = (n // 3) * 340
    sheet.paste(fw, (cx, cy)); sheet.paste(fu, (cx + 170, cy)); sheet.paste(bw, (cx + 340, cy)); sheet.paste(bu, (cx + 510, cy))
    sheet.paste(w.crop((0, 512, 512, 1024)).resize((170, 170)), (cx, cy + 170))
    sheet.paste(u.crop((0, 512, 512, 1024)).resize((170, 170)), (cx + 170, cy + 170))
    sheet.paste(w.crop((512, 512, 1024, 1024)).resize((170, 170)), (cx + 340, cy + 170))
    sheet.paste(u.crop((512, 512, 1024, 1024)).resize((170, 170)), (cx + 510, cy + 170))
    ImageDraw.Draw(sheet).text((cx + 4, cy + 4), a, fill=(255, 255, 255))
sheet.save('C:/Dev/scratch/batch_sheet.png')
