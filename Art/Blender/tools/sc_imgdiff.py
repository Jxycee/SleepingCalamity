"""[Ported from FishGame, M8.] Mean per-channel difference between two renders (0-255). python imgdiff.py a.png b.png
Run before trusting an A/B: under ~0.5 the pair is effectively identical at review size, and any preference is noise."""
import sys
from PIL import Image, ImageChops, ImageStat
a, b = (Image.open(p).convert('RGB') for p in sys.argv[1:3])
print(round(sum(ImageStat.Stat(ImageChops.difference(a, b)).mean) / 3, 3))
