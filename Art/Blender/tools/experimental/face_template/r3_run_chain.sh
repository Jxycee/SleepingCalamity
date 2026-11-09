#!/bin/bash
# round-3 chain: W1 wrap -> 2 closed-loop corrections -> bake/build/render/compose. usage: bash r3_run_chain.sh
cd "$(dirname "$0")"; source ./env3.sh
B=C:/AI/test_out/heroes/face_spike2/bake3
run() { $PY $SC/r3_03_corr.py $F3/wrap_r$1_basis.npy $F3/lm_$1.json $F3/_cam_$1.json $F3/rn_$1.png $2 "${@:3}" 2>&1 | grep -E "silh|field|flips|GATES"; }
$PY r3_02_wrap.py 1 --pass2 0.01,0.006 --prewarp 80 --eyelm 0 --sliders slider_verts_noeye.npy 2>&1 | grep -E "inner|flips|PASS"
rr3 1 >/dev/null
run 1 22 --smooth 100 --rowsbelow -30 --sil 2; rr3 22 >/dev/null
run 22 23 --smooth 100 --rowsbelow -30 --sil 2; rr3 23 >/dev/null
$MPY $SC/r3_06_ratios.py $F3/ratios_final.json +c2=$F3/rn_23.png 2>&1 | grep -v -E "^(W0|INFO|WARN)"
cp $F3/_cam_23.json $B/wrap_cam.json; cp $F3/lm_23.json $B/wrap_lm.json; cp $F3/rn_23.png $B/wrap_front.png; rm -f $B/parts_nominal.npz
$PY c3_00_reseat.py >/dev/null 2>&1; $PY c3_01_mapfit.py 2>&1 | grep "affine rms" | head -1
$PY c3_02_texture.py 2>&1 | grep -E "SKIN|DONE|Trace|Error"; $PY c3_03_eyes.py >/dev/null 2>&1
$PY c3_00b_teeth_opt.py --apply 2>&1 | grep -E "^(template|result|final)"
"$BL" -b --factory-startup --python c3_04_build.py 2>&1 | grep -E "BUILD DONE|Error|Traceback" | cut -c1-40
"$BL" -b --factory-startup --python c3_05_render.py 2>&1 | grep -E "RENDER DONE|Error|Traceback"
$PY c3_06_compose.py | tail -3
"$BL" -b --factory-startup --python r3_09_visible_regions.py 2>&1 | grep -E "^RES" | cut -c1-1500
echo CHAIN_DONE
