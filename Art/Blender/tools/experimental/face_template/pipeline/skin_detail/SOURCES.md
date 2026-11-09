# Skin detail sources

Checked 2026-11-09 (listing queries only, nothing downloaded):
- Poly Haven API (`api.polyhaven.com/assets?t=textures`, 869 textures): no human-skin texture (no skin tag/name hit).
- ambientCG API (`q=skin`): only `Leather008` (tagged "skin" = hide). Not usable for human skin.
- Texturing.xyz free samples: not used (licence for game use not verified).

Result: the detail is fully procedural (`synth.py`: Worley pores + Worley furrow network + value-noise fBm, evaluated from 3D position in mm). No third-party asset is included; nothing to attribute.
