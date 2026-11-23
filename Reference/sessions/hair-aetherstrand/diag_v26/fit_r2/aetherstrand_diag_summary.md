# AetherStrand AI Diagnostic Report (v2.6.0)
- **Character:** `Tobin_AetherStrand` | **Head Bone:** `Head` | **LossyScale:** `(1, 1, 1)`
- **Head Axes in Char Space:** Fwd `(0, 0.1219, 0.9925)`, Up `(0, 0.9925, -0.1219)`
- **Chains:** 44 total (`Bangs=10`, `Crown=9`, `Sides=1`, `Rear=24`) | **Colliders:** 6
- **Shader:** `AetherStrand/SculptedHair_URP` | Colors `#2C1E15 / #5A3F2C / #7A563C` | Textures `4/4`

## Head vs. Hair Alignment (Head-Local Space)
- **Head Bounds:** min `(-0.097, -0.05, -0.0667)` .. max `(0.095, 0.1877, 0.1669)`
- **Hair Bounds:** min `(-0.1573, -0.0975, -0.1132)` .. max `(0.1446, 0.2481, 0.1713)`
- **Deltas (mm):** Front Overhang `+4.5 mm` | Crown Lift `+60.4 mm` | Back Reach `+46.5 mm` | Width Delta `+110.0 mm`

## Deterministic Motion Audits
| Scenario | Verdict | Score | Penetrating | Inter-Lock Clips | Max Kink (deg) | Max Stretch (%) | Mean Sway (cm) |
| :--- | :--- | ---: | ---: | ---: | ---: | ---: | ---: |
| Rest_0.5s | WARN | 82 | 0 | 0 | 37.7 | 8.62 | 4.72 |
| HeadTurn60_0.4s | WARN | 78 | 0 | 0 | 40.8 | 9.32 | 6.62 |
| Sprint5msStop_0.4s | WARN | 65 | 0 | 0 | 43.4 | 14.90 | 7.08 |

## Automated Flags
- ALIGNMENT: Hair crown sits 60.4 mm above skull top (possible crown loops)
