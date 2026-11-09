"""Shared run-directory access for the vision tools (read-only on the run dir except <run>/out/look/ which these tools create)."""
import os, sys, json, subprocess, numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
BLENDER = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
CZ, S2, R2 = 1.70, 0.34, 700            # clay camera (clay_views.py): fixed frame for every character
EC = [33, 263, 133, 362]                 # eye corners: the similarity that registers render -> photo (same as r6_10_score.py)


def lm_array(d):
    return np.array([d[str(i)] for i in range(len(d))], float)


class Run:
    def __init__(self, run_dir, tag=None):
        self.dir = run_dir.replace(chr(92), '/').rstrip('/'); self.name = os.path.basename(self.dir)
        self.fit, self.out, self.tgt = self.dir + '/fit/', self.dir + '/out/', self.dir + '/target/'
        self.look = self.out + 'look/'; os.makedirs(self.look, exist_ok=True)
        sj = self.out + 'score.json'; self.score = json.load(open(sj)) if os.path.exists(sj) else {}
        self.tag = tag or self.score.get('final_tag') or 'h6'
        self.mesh = self.fit + '_rr_%s.npz' % self.tag; self.rn = self.fit + 'rn_%s.png' % self.tag
        for p in (self.mesh, self.rn):
            if not os.path.exists(p):
                raise SystemExit('missing ' + p)
        self.cam = json.load(open(self.fit + '_cam_%s.json' % self.tag)); self.lm_render = lm_array(json.load(open(self.fit + 'lm_%s.json' % self.tag)))
        self.photo = cv2.imread(self.tgt + 'crop_src.png'); self.lm_photo = np.array(json.load(open(self.tgt + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]

    def final(self):
        f = self.score.get('final', {})
        return f.get(self.tag, f) if isinstance(f, dict) else {}

    def clay(self, view):
        return self.look + 'clay_%s.png' % view

    def ensure_clay(self, force=False):
        views = ['front', 'q34', 'side', 'top']
        if not force and all(os.path.exists(self.clay(v)) and os.path.getmtime(self.clay(v)) > os.path.getmtime(self.mesh) for v in views):
            return
        r = subprocess.run([BLENDER, '-b', '--factory-startup', '--python', HERE + '/clay_views.py', '--', self.mesh, self.look + 'clay', ','.join(views), str(R2)], capture_output=True, text=True)
        if 'CLAY_DONE' not in r.stdout:
            raise SystemExit('clay render failed:\n' + r.stdout[-1500:] + r.stderr[-800:])


def sim_render_to_photo(run):
    """similarity (2x3) mapping render px -> photo px from the 4 eye corners (the registration the scorer uses)"""
    M, _ = cv2.estimateAffinePartial2D(run.lm_render[EC].astype(np.float32), run.lm_photo[EC].astype(np.float32))
    return M


def render_px_to_xz(run, px, py):
    c = run.cam
    return c['cx'] + (px / c['R'] - 0.5) * c['S'], c['cz'] + (0.5 - py / c['R']) * c['S']


def xz_to_clay_px(x, z):
    return (x / S2 + 0.5) * R2, (0.5 - (z - CZ) / S2) * R2


CLAY_BG = (87, 81, 77)   # BGR grey the clay renders are composited on


def read_clay(path):
    """clay render (RGBA, transparent background) -> (bgr composited on grey, bool silhouette from alpha)"""
    im = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if im.shape[2] == 3:
        return im, bg_mask(im)
    a = im[..., 3:4].astype(np.float32) / 255; bgr = (im[..., :3] * a + np.array(CLAY_BG, np.float32) * (1 - a)).astype(np.uint8)
    return bgr, im[..., 3] > 127


def bg_mask(img, thr=25):
    from scipy import ndimage as ndi
    bg = img[2, 2].astype(int); m = (np.abs(img.astype(int) - bg).sum(2) > thr)
    return ndi.binary_opening(m, iterations=2)


def photo_head_mask(run):
    """same recipe as r6_10_score.py: mask_vis red-green <= 50, opened, the component through landmark 168, holes filled"""
    from scipy import ndimage as ndi
    from PIL import Image
    mv = np.array(Image.open(run.tgt + 'mask_vis.png').convert('RGB')).astype(int); h = ((mv[..., 0] - mv[..., 1]) <= 50); h = ndi.binary_opening(h, iterations=2)
    lab, n = ndi.label(h); p = run.lm_photo[168]
    return ndi.binary_fill_holes(lab == lab[int(p[1]), int(p[0])])
