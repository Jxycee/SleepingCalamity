"""python sheet.py out.png cols w img1 img2 ... -> grid contact sheet (now sc_ab_sheet.py --grid; this entry point still works)"""
import os, subprocess, sys
sys.exit(subprocess.call([sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), 'sc_ab_sheet.py'), sys.argv[1], '--grid'] + sys.argv[2:]))
