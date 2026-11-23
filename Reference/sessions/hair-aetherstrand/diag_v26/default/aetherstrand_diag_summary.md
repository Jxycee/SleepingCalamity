# AetherStrand AI Diagnostic Report (v2.6.0)
- **Character:** `Tobin_AetherStrand` | **Head Bone:** `Head` | **LossyScale:** `(1, 1, 1)`
- **Head Axes in Char Space:** Fwd `(0, 0.1219, 0.9925)`, Up `(0, 0.9925, -0.1219)`
- **Chains:** 44 total (`Bangs=10`, `Crown=9`, `Sides=1`, `Rear=24`) | **Colliders:** 6
- **Shader:** `AetherStrand/SculptedHair_URP` | Colors `#2C1E15 / #5A3F2C / #7A563C` | Textures `4/4`

## Head vs. Hair Alignment (Head-Local Space)
- **Head Bounds:** min `(-0.097, -0.05, -0.0667)` .. max `(0.095, 0.1877, 0.1669)`
- **Hair Bounds:** min `(-0.1269, -0.0279, -0.0926)` .. max `(0.1247, 0.2601, 0.1444)`
- **Deltas (mm):** Front Overhang `-22.5 mm` | Crown Lift `+72.4 mm` | Back Reach `+25.9 mm` | Width Delta `+59.7 mm`

## Deterministic Motion Audits
| Scenario | Verdict | Score | Penetrating | Inter-Lock Clips | Max Kink (deg) | Max Stretch (%) | Mean Sway (cm) |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| Rest_0.5s | FAIL | 54 | 0 | 1 | 53.5 | 24.34 | 2.25 |
| HeadTurn60_0.4s | FAIL | 54 | 0 | 1 | 53.6 | 23.93 | 4.49 |
| Sprint5msStop_0.4s | FAIL | 54 | 0 | 1 | 53.6 | 24.16 | 8.27 |

## Automated Flags
- ALIGNMENT: Hair front sits 22.5 mm BEHIND forehead (receding hairline)
- ALIGNMENT: Hair crown sits 72.4 mm above skull top (possible crown loops)
