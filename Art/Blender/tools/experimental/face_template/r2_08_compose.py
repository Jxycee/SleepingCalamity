from PIL import Image, ImageDraw
F = 'C:/AI/test_out/heroes/face_spike2/fit/'
rows = ['template', 'sliders (no eye keys)', 'wrap round 3', 'target (aligned, cropped)']; cols = ['front', 'side', 'q34']
W = Image.new('RGB', (3 * 600 + 150, 4 * 600), (30, 30, 30)); d = ImageDraw.Draw(W)
for r in range(4):
    d.text((8, r * 600 + 280), rows[r], fill=(255, 255, 255))
    for c, v in enumerate(cols): W.paste(Image.open(F + f'sh_{r}_{v}.png').convert('RGB').resize((600, 600)), (150 + c * 600, r * 600))
W.save(F + 'before_after_sheet.png'); print(W.size)
