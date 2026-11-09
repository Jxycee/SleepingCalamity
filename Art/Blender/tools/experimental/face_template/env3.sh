# helper for round-3 runs (source it). rr3 <tag>: front-ortho render of fit3/wrap_r<tag>_mesh.npz + mediapipe landmarks.
F3=C:/AI/test_out/heroes/face_spike2/fit3
SC=C:/Dev/SleepingCalamity/.claude/worktrees/procgen-next-73e90d/Art/Blender/tools/experimental/face_template
BL="C:/Program Files/Blender Foundation/Blender 5.2/blender.exe"
PY=C:/AI/trellis2/venv/Scripts/python.exe
MPY=C:/AI/envs/facekeys/Scripts/python.exe
bl() { "$BL" -b -P $SC/$1 -- "${@:2}" 2>&1 | grep -E "^(EXPORT|OK|RES|PRINT)|Error|Traceback|line [0-9]+" | grep -v "Failed to remove"; }
rr3() {
  $PY $SC/r3_prep_render.py "$F3/wrap_r$1_mesh.npz" "$F3/_rr_$1.npz"
  bl r3_01_blender.py render $F3/_rr_$1.npz $F3/rn_$1.png $F3/_cam_$1.json 1.46 1.92
  $MPY $SC/r2_02_landmarks.py $F3/rn_$1.png $F3/lm_$1.json 2>&1 | grep faces
}
# rr3m <tag>: like rr3 but averages the landmarks over 3 jittered camera framings (reduces detector noise)
rr3m() {
  rr3 $1 >/dev/null
  for j in "b 1.455 1.915" "c 1.468 1.928"; do set -- $1 $j; 
    bl r3_01_blender.py render $F3/_rr_$1.npz $F3/rn_$1$2.png $F3/_cam_$1$2.json $3 $4
    $MPY $SC/r2_02_landmarks.py $F3/rn_$1$2.png $F3/lm_$1$2.json 2>&1 | grep faces >/dev/null; done
  $PY $SC/r3_04_avglm.py $1 ${1}b ${1}c
}
