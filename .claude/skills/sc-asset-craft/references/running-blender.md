<!-- Covers: where Blender lives, the generator and audit command lines, and cloud-session differences. Moved from SKILL.md section 4. -->

Locally Blender 5.2 is at `C:/Program Files/Blender Foundation/Blender 5.2/blender.exe` (not on the Git Bash PATH; `blender` below means that path). Iterate with `--out <scratch>`; give generators `--only`, `--no-export` and `--out`. Export to the repo only at the end, and only when the output changed (unchanged FBX and PNG stay byte-identical: no LFS churn, no reimport).
```
blender -b --factory-startup --python-exit-code 1 --python Art/Blender/<area>/<gen>.py -- --out <scratch> [--only <stem>]
blender -b --factory-startup --python Art/Blender/tools/sc_asset_audit.py -- <scratch>/audit.json --class kit <scratch>/*.fbx
```
**Cloud sessions** (Linux, Blender 4.5, `~/apps/blender-4.5.3-linux-x64/blender`) commit the generator, not the FBX or textures: a different Blender writes different bytes. The PC session runs the generator to write the outputs. Blender 4.5's EEVEE engine id is `BLENDER_EEVEE_NEXT` (5.2: `BLENDER_EEVEE`).
