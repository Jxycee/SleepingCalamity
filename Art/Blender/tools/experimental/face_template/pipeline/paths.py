"""Single source of paths for the face pipeline.

The stage scripts in stages/ read their locations from SCP_* environment variables (so Blender's
python and the two other interpreters see the same values). face_pipeline.py builds them from the
config with `Paths(cfg).env()`; for a manual run of one stage script, `eval $(python paths.py cfg.json)`
prints the same exports.

Run directory layout (everything outside the repo):
  <out_dir>/target/    photo prep, landmarks, Hunyuan candidates + chosen shape.glb
  <out_dir>/fit/       template6, alignment, wraps (wrap_r<tag>_*), renders (rn_/lm_/_cam_), fit logs
  <out_dir>/bake/      reseat, teeth, map fit, textures, build inputs
  <out_dir>/out/       blend, cells, proof sheet, closeups, overlay, score.json
  <out_dir>/manifest.json
"""
import os, sys, json

HERE = os.path.dirname(os.path.abspath(__file__)).replace('\\', '/')
STAGES = HERE + '/stages'
REPO = os.path.abspath(os.path.join(HERE, *(['..'] * 6))).replace('\\', '/')   # .../Art/Blender/tools/experimental/face_template/pipeline -> repo root

TOOLS = dict(
    blender='C:/Program Files/Blender Foundation/Blender 5.2/blender.exe',
    py='C:/AI/trellis2/venv/Scripts/python.exe',        # numpy / scipy / trimesh / cv2 / skimage / PIL
    mp='C:/AI/envs/facekeys/Scripts/python.exe',        # mediapipe
    comfy_py='C:/AI/stablegen/ComfyUI/.venv/Scripts/python.exe',
    hy_script='C:/AI/stablegen/run_hy_shape.py',
    face_model='C:/AI/test_out/heroes/tobin/shapes_v2/dl/face_landmarker.task',
)


class Paths:
    def __init__(self, cfg):
        self.cfg = cfg
        self.run = cfg['out_dir'].replace('\\', '/').rstrip('/')
        self.target, self.fit, self.bake, self.out = (self.run + '/' + d for d in ('target', 'fit', 'bake', 'out'))
        t = cfg['template']
        self.tpl_root = t['root'].rstrip('/'); self.tpl_npz = t['npz']; self.sliders_dir = t['sliders_dir'].rstrip('/'); self.data_dir = t['data_dir'].rstrip('/')
        self.tools = dict(TOOLS); self.tools.update(cfg.get('tools', {}))
        for d in (self.target, self.fit, self.bake, self.out): os.makedirs(d, exist_ok=True)

    def env(self):
        e = dict(os.environ)
        e.update(SCP_RUN=self.run, SCP_TPL_ROOT=self.tpl_root, SCP_TPL_NPZ=self.tpl_npz, SCP_SLIDERS_DIR=self.sliders_dir, SCP_REPO=REPO,
                 SCP_TARGET_GLB=self.target + '/shape.glb', SCP_NAME=self.cfg['name'], SCP_IRIS=json.dumps(self.cfg.get('iris_colour')),
                 SCP_FACE_MODEL=self.tools['face_model'], PYTHONIOENCODING='utf-8')
        e.pop('SC_TPL', None)   # the stage common modules set it themselves
        return e


if __name__ == '__main__':
    cfg = json.load(open(sys.argv[1]))
    for k, v in Paths(cfg).env().items():
        if k.startswith('SCP_'): print("export %s='%s'" % (k, v))
