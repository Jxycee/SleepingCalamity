# Whole-area sheets for the Area 1 world build (from Run 6's sheet_round.py).
# usage: python sheet_area.py <round name> <dir of this round's PNG or JPG> <dir of previous round or "none"> <out dir>
# Writes every camera as a 1280 px wide JPG (quality 88) into <out dir>, plus <round>_sheet_A.jpg (concept row, cameras 1-10) and
# <round>_sheet_B.jpg (cameras 11-20, then the three high views). Each tile is labelled with the camera name and p10/p50/p90 of the max
# channel (0-1); the previous round's image is an inset (30% of the tile width) in the lower right corner.
# Also prints the stats and flags p50 < 0.12 or more than 35% of pixels darker than 0.03 ("probably blocked or inside geometry").
import sys, os, glob
from PIL import Image, ImageDraw

A = ['Eye_Spawn', 'Eye_Postcard', 'Eye_West', 'Eye_Bakery', 'Eye_InnFront', 'Eye_Climb', 'Eye_Balcony', 'Eye_BellYard', 'Eye_Churchyard', 'Eye_HillWest', 'Eye_MarketStreet']
B = ['Eye_LowerLane', 'Eye_RingWest', 'Eye_RingSouth', 'Eye_Orchard', 'Eye_Mill', 'Eye_Meadow', 'Eye_Lake', 'Eye_JettyBack', 'Eye_RoadOut', 'Eye_RoadBack']
HIGH = ['Overview', 'High_Lake', 'High_East']
CONCEPTS = r'C:\Dev\SleepingCalamity\Reference\private\concepts'
REFS = [('H1', 'H1 wide square, midday.jpg'), ('H4', 'H4 bakery (ruined, to be rebuilt).jpg'), ('H5', 'H5 church and bell tower.jpg'), ('H8', 'H8 orchard edge.jpg')]
SW = 1400


def find(folder, cam):
    if not folder or folder.lower() == 'none':
        return None
    for ext in ('.png', '.jpg'):
        p = os.path.join(folder, 'HM_%s_Ruined%s' % (cam, ext))
        if os.path.exists(p):
            return p
    return None


def fit(im, w, h):
    sc = max(w / im.width, h / im.height)
    im = im.resize((max(w, round(im.width * sc)), max(h, round(im.height * sc))), Image.LANCZOS)
    l, t = (im.width - w) // 2, (im.height - h) // 2
    return im.crop((l, t, l + w, t + h))


def stats(im):
    g = im.convert('RGB').resize((480, 270), Image.BILINEAR)
    v = sorted(max(p) / 255.0 for p in g.getdata()); n = len(v)
    dark = sum(1 for x in v if x < 0.03) / n
    return v[int(n * 0.10)], v[int(n * 0.50)], v[int(n * 0.90)], dark


def tile(sheet, d, x, y, w, h, cam, cur, prev, rnd, out, st):
    p = find(cur, cam)
    if p is None:
        d.text((x + 6, y + 4), cam + ' MISSING', fill=(255, 80, 80)); return
    im = Image.open(p).convert('RGB')
    p10, p50, p90, dark = st[cam]
    sheet.paste(fit(im, w, h), (x, y))
    q = find(prev, cam)
    if q is not None:
        iw = int(w * 0.30); ih = round(iw * 9 / 16)
        sheet.paste(fit(Image.open(q).convert('RGB'), iw, ih), (x + w - iw - 3, y + h - ih - 3))
        d.rectangle((x + w - iw - 4, y + h - ih - 4, x + w - 3, y + h - 3), outline=(255, 255, 255))
    d.rectangle((x, y, x + 330, y + 14), fill=(0, 0, 0))
    d.text((x + 4, y + 2), '%s  %.2f/%.2f/%.2f' % (cam, p10, p50, p90), fill=(255, 255, 255))


def main():
    rnd, cur, prev, out = sys.argv[1:5]
    os.makedirs(out, exist_ok=True)
    st = {}
    names = sorted(os.path.basename(f)[3:-len('_Ruined.png')] for f in glob.glob(os.path.join(cur, 'HM_*_Ruined.*')) if f.lower().endswith(('.png', '.jpg')))
    for cam in names:
        im = Image.open(find(cur, cam)).convert('RGB')
        im.resize((1280, round(im.height * 1280 / im.width)), Image.LANCZOS).save(os.path.join(out, 'HM_%s_Ruined.jpg' % cam), quality=88)
        st[cam] = stats(im)
        p10, p50, p90, dark = st[cam]
        flag = '  FLAG probably blocked or inside geometry' if p50 < 0.12 or dark > 0.35 else ''
        print('%s %-16s p10/p50/p90 %.3f/%.3f/%.3f dark<0.03 %.1f%%%s' % (rnd, cam, p10, p50, p90, dark * 100, flag))
    for cam in A + B + HIGH:
        st.setdefault(cam, (0, 0, 0, 0))
    # sheet A: concept row + 10 tiles; sheet B: 10 tiles + high row
    TW = SW // 2; TH = round(TW * 9 / 16)
    refs = [(l, os.path.join(CONCEPTS, f)) for l, f in REFS]
    have_refs = os.path.isdir(CONCEPTS) and any(os.path.exists(p) for _, p in refs)
    if not have_refs:
        print('note: concept folder missing, reference row skipped')
    RW = SW // 4; RH = round(RW * 9 / 16); top = RH if have_refs else 0
    sa = Image.new('RGB', (SW, top + ((len(A) + 1) // 2) * TH), (20, 20, 20)); da = ImageDraw.Draw(sa)
    if have_refs:
        for i, (l, p) in enumerate(refs):
            if os.path.exists(p):
                sa.paste(fit(Image.open(p).convert('RGB'), RW, RH), (i * RW, 0))
            da.rectangle((i * RW, 0, i * RW + 40, 14), fill=(0, 0, 0)); da.text((i * RW + 4, 2), 'ref ' + l, fill=(255, 255, 0))
    for i, cam in enumerate(A):
        tile(sa, da, (i % 2) * TW, top + (i // 2) * TH, TW, TH, cam, cur, prev, rnd, out, st)
    sa.save(os.path.join(out, rnd + '_sheet_A.jpg'), quality=88)
    HW = SW // 3; HH = round(HW * 9 / 16)
    sb = Image.new('RGB', (SW, 5 * TH + HH), (20, 20, 20)); db = ImageDraw.Draw(sb)
    for i, cam in enumerate(B):
        tile(sb, db, (i % 2) * TW, (i // 2) * TH, TW, TH, cam, cur, prev, rnd, out, st)
    for i, cam in enumerate(HIGH):
        tile(sb, db, i * HW, 5 * TH, HW, HH, cam, cur, prev, rnd, out, st)
    sb.save(os.path.join(out, rnd + '_sheet_B.jpg'), quality=88)
    print('sheets', os.path.join(out, rnd + '_sheet_A.jpg'), sa.size, os.path.join(out, rnd + '_sheet_B.jpg'), sb.size)


main()
