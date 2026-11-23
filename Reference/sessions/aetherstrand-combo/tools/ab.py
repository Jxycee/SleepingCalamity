import sys
from PIL import Image
w=Image.open('C:/Dev/scratch/web_out/halfup059_4view_web.png').convert('RGB'); u=Image.open(sys.argv[1]).convert('RGB')
n=int(sys.argv[2]) if len(sys.argv)>2 else 0
# n: 0=all views side by side (scaled), 1..4 view index
if n==0:
    s=Image.new('RGB',(2048,1024)); s.paste(w,(0,0)); s.paste(u,(1024,0)); s=s.resize((1600,800))
else:
    k=n-1; box=((k%2)*512,(k//2)*512,(k%2)*512+512,(k//2)*512+512); s=Image.new('RGB',(1024,512)); s.paste(w.crop(box),(0,0)); s.paste(u.crop(box),(512,0))
s.save('C:/Dev/scratch/ab_out.png')
