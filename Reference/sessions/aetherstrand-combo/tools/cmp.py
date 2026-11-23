import sys
import numpy as np
from PIL import Image

web = Image.open(sys.argv[1]).convert('RGB')
uni = Image.open(sys.argv[2]).convert('RGB')
out = sys.argv[3] if len(sys.argv) > 3 else 'C:/Dev/scratch/cmp_out.png'


def hair(a):
    return (a[..., 0] > a[..., 1] * 1.8) & (a[..., 0] > 30)


names = ['FRONT', 'BACK', 'LEFT', 'RIGHT']
tot_iou = []
tot_mae = []
for k, (x, y) in enumerate([(0, 0), (512, 0), (0, 512), (512, 512)]):
    aw = np.array(web.crop((x, y, x + 512, y + 512))).astype(float)
    au = np.array(uni.crop((x, y, x + 512, y + 512))).astype(float)
    mw, mu = hair(aw), hair(au)
    iou = (mw & mu).sum() / (mw | mu).sum()
    both = mw & mu
    mae = np.abs(aw - au)[both].mean()
    mae_all = np.abs(aw - au)[mw | mu].mean()
    tot_iou.append(iou)
    tot_mae.append(mae_all)
    print(names[k], 'IoU %.3f' % iou, 'MAE(both) %.1f' % mae, 'MAE(union) %.1f' % mae_all,
          'meanW', aw[mw].mean(0).round(0), 'meanU', au[mu].mean(0).round(0))
print('AVG IoU %.3f  MAE(union) %.1f' % (np.mean(tot_iou), np.mean(tot_mae)))
# side by side + abs diff
d = np.abs(np.array(web).astype(float) - np.array(uni).astype(float)).clip(0, 255) * 2
s = Image.new('RGB', (3072, 1024))
s.paste(web, (0, 0))
s.paste(uni, (1024, 0))
s.paste(Image.fromarray(d.clip(0, 255).astype('uint8')), (2048, 0))
s.resize((1800, 600)).save(out)
