#!/bin/bash
# round-3 bake chain for FINAL_TAG (reseat -> mapfit -> texture -> eyes -> teeth fit -> build -> render -> compose). usage: bash r3_run_bake.sh <tag>
cd "$(dirname "$0")"; source ./env3.sh; T=$1
B=C:/AI/test_out/heroes/face_spike2/bake3
cp $F3/_cam_$T.json $B/wrap_cam.json; cp $F3/lm_$T.json $B/wrap_lm.json; cp $F3/rn_$T.png $B/wrap_front.png; rm -f $B/parts_nominal.npz
$PY c3_00_reseat.py >/dev/null 2>&1; $PY c3_01_mapfit.py 2>&1 | grep "affine rms" | head -1
$PY c3_02_texture.py 2>&1 | grep -E "SKIN|DONE|Trace|Error"; $PY c3_03_eyes.py >/dev/null 2>&1
$PY c3_00b_teeth_opt.py --apply 2>&1 | grep -E "^(template|result|final)"
"$BL" -b --factory-startup --python c3_04_build.py 2>&1 | grep -E "BUILD DONE|Error|Traceback" | cut -c1-40
"$BL" -b --factory-startup --python c3_05_render.py 2>&1 | grep -E "RENDER DONE|Error|Traceback"
$PY c3_06_compose.py | tail -3
$MPY r3_06_ratios.py $F3/ratios_final.json r2=$F3/rn_r2.png +r3=$F3/rn_$T.png 2>&1 | grep -v -E "^(W0|INFO|WARN)"
$PY c3_07_r2_vs_r3.py
"$BL" -b --factory-startup --python r3_09_visible_regions.py 2>&1 | grep -E "^RES" | grep -o '"lip_patch": {"n": 1434, "front_visible": [0-9]*'
$MPY r3_10_score.py r2=$F3/rn_r2.png r3=$F3/rn_$T.png --tex $B/texture.json 2>&1 | grep -v -E "^(W0|INFO|WARN)"
echo BAKE_DONE
