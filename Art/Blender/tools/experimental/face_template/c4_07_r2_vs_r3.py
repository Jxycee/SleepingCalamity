"""Before/after strip round 2 vs round 3 (venv python: PIL). Writes out4/r2_vs_r3.png:
 row 1: front neutral (Cycles cells) round 2 | round 3 ; row 2: eye-aligned Workbench overlay on the photo (50% blend) round 2 | round 3 ; row 3: photo | r2 overlay render | r3 overlay render.
Needs fit4/rn_r2_overlay.png (r4_06_ratios on the round-2 front render) and fit4/rn_45_overlay.png (round-3 final)."""
from PIL import Image, ImageDraw
D2 = 'C:/AI/test_out/heroes/face_spike2/'; O4 = D2 + 'out4/'; F3 = D2 + 'fit4/'
try:
    from PIL import ImageFont; F = ImageFont.truetype('arial.ttf', 24)
except Exception: F = None
r2 = Image.open(D2 + 'out/cells/front_neutral_full.png').convert('RGB'); r3 = Image.open(O4 + 'cells/front_neutral_full.png').convert('RGB')
o2 = Image.open(F3 + 'rn_r2_overlay.png').convert('RGB'); o3 = Image.open(F3 + 'rn_45_overlay.png').convert('RGB')    # each: photo | render warped to photo | 50% blend
pw = o2.width // 3
def panel(o, k): return o.crop((k * pw, 0, (k + 1) * pw, o.height))
W = 4 * 640; S = Image.new('RGB', (W, 60 + 640 + 710 + 40), (28, 28, 30)); d = ImageDraw.Draw(S)
S.paste(r2.resize((640, 640)), (0, 60)); S.paste(r3.resize((640, 640)), (640, 60))
d.text((10, 14), 'round 2: front neutral', fill=(240, 240, 240), font=F); d.text((650, 14), 'round 3: front neutral', fill=(240, 240, 240), font=F)
ph = panel(o3, 0); S.paste(ph.resize((640, int(ph.height * 640 / ph.width))), (1280, 60)); d.text((1290, 14), 'photo', fill=(240, 240, 240), font=F)
y0 = 60 + 640 + 20
for i, (lab, o) in enumerate((('round 2: eye-aligned 50% blend', o2), ('round 3: eye-aligned 50% blend', o3))):
    p = panel(o, 2); S.paste(p, (i * pw, y0)); d.text((i * pw + 10, y0 - 22), lab, fill=(255, 220, 120), font=F)
for i, (lab, o) in enumerate((('round 2: render over photo', o2), ('round 3: render over photo', o3))):
    p = panel(o, 1); S.paste(p, ((2 + i) * pw, y0)); d.text(((2 + i) * pw + 10, y0 - 22), lab, fill=(255, 220, 120), font=F)
S = S.crop((0, 0, 4 * pw, y0 + o2.height + 10)); S.save(O4 + 'r2_vs_r3.png'); print(S.size)
