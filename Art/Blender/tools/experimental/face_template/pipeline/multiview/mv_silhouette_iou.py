"""Multi-view silhouette IoU table: a head mesh vs the generated views (cutout alpha), with a held-out 3/4 check, plus the side-prior score of the mesh's own side silhouette.
usage (trellis2 venv python, needs trimesh/cv2/scipy; facekeys python for the prior part is NOT needed, the prior part imports side_prior which only needs numpy/cv2/scipy):
  python mv_silhouette_iou.py <mesh.glb|obj|npz> <cutout_dir> <tag> <out_dir> [--train front,back,left,right] [--label mv1]
Cutouts = <cutout_dir>/mv1_<tag>_<view>.png (RGBA, written by mv_make_cutouts.py; also the TRELLIS inputs).
Method: ORTHOGRAPHIC silhouettes of the mesh (cv2.fillPoly of all projected triangles). Silhouette and mask are both normalised by their own bounding box height (crown to the
neck cut; TRELLIS crops each input independently and the cutouts share one neck cut row) and centred on the bbox centre in x, so the table is independent of mesh scale/position.
The 24 proper rotations of the axis frame are searched: the mesh frame is unknown (TRELLIS output), the best rotation on the TRAIN views is used and then reported for every view
(3/4 views are not given to TRELLIS = held-out validation). Left/right label swap is also scored. Writes <out_dir>/<label>_iou.json/.txt and <label>_iou_sheet.png (mask red, mesh green)."""
import sys, os, json, itertools, argparse, numpy as np, cv2
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

YAW = dict(front=0, q34l=40, left=90, right=-90, q34r=-40, back=180)


def load_mesh(path):
    if path.endswith('.npz'):
        d = np.load(path); return d['v'].astype(np.float64), d['f'].astype(np.int64)
    import trimesh
    m = trimesh.load(path, force='mesh'); parts = m.split(only_watertight=False)        # TRELLIS leaves floaters (2305 pieces on the 4-view head): keep the largest piece
    if len(parts) > 1: m = max(parts, key=lambda q: len(q.faces))
    return np.asarray(m.vertices, np.float64), np.asarray(m.faces, np.int64)


def rotations():
    out = []
    for perm in itertools.permutations(range(3)):
        for sg in itertools.product([1, -1], repeat=3):
            R = np.zeros((3, 3)); 
            for i, (p, s) in enumerate(zip(perm, sg)): R[i, p] = s
            if np.linalg.det(R) > 0: out.append(R)
    return out


def sil_mesh(v, f, yaw_deg, res=256):
    """v already in head frame: z up, face toward -y. Camera at yaw (0 front, +90 subject's left): image x = right vector."""
    th = np.radians(yaw_deg); cam = np.array([np.sin(th), -np.cos(th), 0.0]); fwd = -cam; right = np.cross(fwd, [0, 0, 1.0]); right /= np.linalg.norm(right)
    x = v @ right; y = v[:, 2]
    return norm_raster(np.stack([x, y], 1), f, res)


def norm_raster(xy, f, res):
    x0, x1, y0, y1 = xy[:, 0].min(), xy[:, 0].max(), xy[:, 1].min(), xy[:, 1].max(); s = 0.9 * (res - 1) / (y1 - y0)
    px = (xy[:, 0] - 0.5 * (x0 + x1)) * s + res / 2; py = (y1 - xy[:, 1]) * s + 0.05 * res
    tri = np.stack([px[f], py[f]], -1); img = np.zeros((res, res), np.uint8)
    cv2.fillPoly(img, np.round(tri * 16).astype(np.int32).reshape(len(f), 3, 2), 255, shift=4)
    k = 3 if res <= 300 else 7; img = cv2.morphologyEx(img, cv2.MORPH_CLOSE, np.ones((k, k), np.uint8))      # tiny triangles leave pinholes at high res
    from scipy import ndimage as ndi; return ndi.binary_fill_holes(img > 0)


def sil_mask(path, res=256):
    a = cv2.imread(path, cv2.IMREAD_UNCHANGED)[..., 3] > 127; ys, xs = np.where(a)
    xy = np.stack([xs, -ys.astype(float)], 1).astype(np.float64)
    x0, x1, y0, y1 = xs.min(), xs.max(), -ys.max(), -ys.min(); s = 0.9 * (res - 1) / (y1 - y0)
    px = (xs - 0.5 * (x0 + x1)) * s + res / 2; py = (y1 - (-ys)) * s + 0.05 * res; img = np.zeros((res, res), bool)
    img[np.clip(np.round(py).astype(int), 0, res - 1), np.clip(np.round(px).astype(int), 0, res - 1)] = True
    img = cv2.morphologyEx(img.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8)); return cv2.dilate(img, np.ones((2, 2), np.uint8)) > 0


def iou(a, b): return float((a & b).sum() / max((a | b).sum(), 1))


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('mesh'); ap.add_argument('cut'); ap.add_argument('tag'); ap.add_argument('out'); ap.add_argument('--train', default='front,back,left,right'); ap.add_argument('--label', default='mv1'); ap.add_argument('--fixed_rot', default=None, help='<label>_iou.json of another mesh from the same TRELLIS pipeline: reuse its rotation (a front-only mesh cannot tell front from back)'); ap.add_argument('--export_glb', default=None, help='write the head-frame mesh as glTF (Y up, face +Z, 40k faces) for pipeline hunyuan.pinned_glb')
    a = ap.parse_args(); os.makedirs(a.out, exist_ok=True); v, f = load_mesh(a.mesh); v = v - v.mean(0); train = a.train.split(',')
    masks = {k: sil_mask(f'{a.cut}/mv1_{a.tag}_{k}.png') for k in YAW}
    best = None; allrot = rotations()
    for ri, R in (enumerate(allrot) if not a.fixed_rot else [(json.load(open(a.fixed_rot))['rotation_index'],)*0 or (json.load(open(a.fixed_rot))['rotation_index'], allrot[json.load(open(a.fixed_rot))['rotation_index']])]):
        vr = v @ R.T; s = {k: iou(sil_mesh(vr, f, YAW[k]), masks[k]) for k in train}; m = float(np.mean(list(s.values())))
        if best is None or m > best[0]: best = (m, ri, R, s)
    m, ri, R, s = best; vr = v @ R.T
    # finer yaw refinement about the vertical axis (+-12 deg)
    bestyaw = (m, 0.0)
    for dy in (np.arange(-12, 12.1, 3) if not a.fixed_rot else [json.load(open(a.fixed_rot))['extra_yaw']]):
        c, sn = np.cos(np.radians(dy)), np.sin(np.radians(dy)); Rz = np.array([[c, -sn, 0], [sn, c, 0], [0, 0, 1]]); vv = vr @ Rz.T
        mm = float(np.mean([iou(sil_mesh(vv, f, YAW[k]), masks[k]) for k in train]))
        if mm > bestyaw[0]: bestyaw = (mm, float(dy))
    c, sn = np.cos(np.radians(bestyaw[1])), np.sin(np.radians(bestyaw[1])); vr = vr @ np.array([[c, -sn, 0], [sn, c, 0], [0, 0, 1]]).T
    tab = {k: iou(sil_mesh(vr, f, YAW[k]), masks[k]) for k in YAW}; swap = {'left': iou(sil_mesh(vr, f, YAW['right']), masks['left']), 'right': iou(sil_mesh(vr, f, YAW['left']), masks['right'])}
    L = ['rotation index %d, extra yaw %.0f deg, mean TRAIN IoU %.3f' % (ri, bestyaw[1], bestyaw[0]), 'view   IoU   (train = given to TRELLIS, HELD-OUT = not given)']
    for k in YAW: L.append('  %-6s %.3f  %s' % (k, tab[k], 'train' if k in train else 'HELD-OUT'))
    L.append('left/right label check: left mask vs mesh right-view %.3f (vs correct %.3f); right mask vs mesh left-view %.3f (vs correct %.3f)' % (swap['left'], tab['left'], swap['right'], tab['right']))
    # side prior score of the mesh's own left silhouette (left-facing, nose to image left)
    out = dict(rotation_index=ri, extra_yaw=bestyaw[1], train_iou_mean=bestyaw[0], iou=tab, swap_check=swap, train=train)
    try:
        import mv_common as MC
        P = MC.prior(); sl = sil_mesh(vr, f, 90, res=900)
        m_, lines, js = MC.score_profile('MESH left silhouette (neck cut)', sl, P)
        if lines: L += [''] + lines; out['side_prior'] = js
        else: L.append('side-prior anchors failed on the mesh silhouette')
    except Exception as e: L.append('side prior skipped: %r' % e)
    txt = '\n'.join(L); print(txt); open(f'{a.out}/{a.label}_iou.txt', 'w').write(txt + '\n'); json.dump(out, open(f'{a.out}/{a.label}_iou.json', 'w'), indent=1)
    cells = []
    for k in YAW:
        ms = sil_mesh(vr, f, YAW[k]); im = np.zeros((256, 256, 3), np.uint8); im[masks[k]] = (0, 0, 220); im[ms & ~masks[k]] = (0, 200, 0); im[ms & masks[k]] = (60, 200, 220)
        cv2.putText(im, '%s %.2f' % (k, tab[k]), (6, 18), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1); cells.append(im)
    cv2.imwrite(f'{a.out}/{a.label}_iou_sheet.png', np.vstack([np.hstack(cells[:3]), np.hstack(cells[3:])]))
    if a.export_glb:
        import pymeshlab, trimesh
        ms_ = pymeshlab.MeshSet(); ms_.add_mesh(pymeshlab.Mesh(vr.astype(np.float64), f.astype(np.int32))); ms_.meshing_decimation_quadric_edge_collapse(targetfacenum=40000, preservenormal=True)
        mm = ms_.current_mesh(); V = mm.vertex_matrix(); F = mm.face_matrix(); V = V / (V[:, 2].max() - V[:, 2].min()) * 2.0       # unit-ish scale like the Hunyuan shapes
        g = np.stack([V[:, 0], V[:, 2], -V[:, 1]], 1); trimesh.Trimesh(g, F, process=False).export(a.export_glb); print('exported', a.export_glb, len(F), 'faces')
    np.savez(f'{a.out}/{a.label}_headframe.npz', v=vr.astype(np.float32), f=f.astype(np.int32))


if __name__ == '__main__':
    main()
