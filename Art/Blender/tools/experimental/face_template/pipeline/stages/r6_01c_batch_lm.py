# facekeys env: mediapipe landmarks (px) for many images. usage: out.json img1.png img2.png ...  -> {img: [[x,y]*468]}   (env SCP_LM_JIT, see lm_detect.py)
import sys, json, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lm_detect
json.dump(lm_detect.Detector().detect_files(sys.argv[2:], os.environ.get('SCP_LM_JIT', '0')), open(sys.argv[1], 'w'))
