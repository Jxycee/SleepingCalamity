# bash sheet.sh <round_dir> : reference row above the preview row (front, 3/4, side, back)
R=$1; T=C:/AI/test_out/heroes/tobin; PY=C:/AI/trellis2/venv/Scripts/python.exe
CS=$(dirname "$0")/../../face_template/pipeline/multiview/v2/contact_sheet.py
$PY $CS $R/sheet.png 420 "hair $(basename $R): reference (top) vs build" ref_front=$T/refs/crop_front.png ref_q34=$T/refs/crop_front2.png ref_side=$T/refs/crop_side.png ref_back=$T/refs/crop_back.png "|" front=$R/prev_front.png q34=$R/prev_q34.png side=$R/prev_side.png back=$R/prev_back.png ${2:+q34b=$R/prev_q34b.png}
