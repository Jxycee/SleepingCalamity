# downstream chain for a final tag: bash r6_chain.sh <tag>   (reseat, teeth, mapfit, texture, eyes, build, render)
set -x
T=$1; F6=C:/AI/test_out/heroes/face_spike2/fit6; PY=C:/AI/trellis2/venv/Scripts/python.exe; BL="C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
cd C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/Art/Blender/tools/experimental/face_template
export SC_FINAL_TAG=$T
rm -f C:/AI/test_out/heroes/face_spike2/bake6/parts_nominal.npz
$PY c6_00_reseat.py > $F6/d_reseat.log 2>&1
$PY c6_00b_teeth_opt.py --apply > $F6/d_teeth.log 2>&1
$PY c6_01_mapfit.py > $F6/d_map.log 2>&1
$PY c6_02_texture.py > $F6/d_tex.log 2>&1
$PY c6_03_eyes.py > $F6/d_eyes.log 2>&1
"$BL" -b --factory-startup --python c6_04_build.py > $F6/build.log 2>&1
rm -f $F6/render.log
"$BL" -b --factory-startup --python c6_05_render.py > $F6/render.log 2>&1
echo FIN >> $F6/render.log
