#!/usr/bin/env python3
"""Generates the tileable water normal map used by SC_Water (Assets/_Project/Content/Textures/Water/T_Water_N.png).

A height field made of random plane waves with INTEGER wave vectors (so it tiles exactly), amplitude ~ 1/|k|^1.15, wave vectors between 3 and 40
cycles per tile, plus a few sharper ridged ripples; the normal comes from the analytic gradient. Output: RGB tangent-space normal (z up), 512x512.
Deterministic (fixed seed). Run:  python3 Tools/town/sc_water_normal.py
"""
import numpy as np
from PIL import Image
N = 512
rng = np.random.default_rng(1031)
y, x = np.mgrid[0:N, 0:N] / N
gx = np.zeros((N, N)); gy = np.zeros((N, N))
for i in range(90):
    ang = rng.uniform(0, 2 * np.pi)
    mag = np.exp(rng.uniform(np.log(3), np.log(40)))
    kx, ky = int(round(np.cos(ang) * mag)), int(round(np.sin(ang) * mag))
    if kx == 0 and ky == 0: continue
    k = np.hypot(kx, ky)
    a = 1.0 / k ** 1.15
    ph = rng.uniform(0, 2 * np.pi)
    arg = 2 * np.pi * (kx * x + ky * y) + ph
    # ridged ripple: |sin| gives sharper crests; mix with the plain wave
    d = -a * 2 * np.pi * np.sin(arg)
    gx += d * kx; gy += d * ky
s = 0.55 / np.sqrt((gx ** 2 + gy ** 2).mean())
nx, ny = -gx * s, -gy * s
nz = np.ones_like(nx)
l = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
rgb = np.stack([nx / l, ny / l, nz / l], -1) * 0.5 + 0.5
Image.fromarray((rgb * 255 + 0.5).astype(np.uint8), "RGB").save("Assets/_Project/Content/Textures/Water/T_Water_N.png")
print("written, slope rms", float(np.sqrt((nx ** 2 + ny ** 2).mean())))
