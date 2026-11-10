"""Clean a TRELLIS multi-view head mesh before it becomes the fit target (multi-view mode, configs "mode": "multiview").
usage (trellis2 venv python, pymeshlab + trimesh + scipy):
  python mv_clean_shape.py <in_headframe.npz|glb|obj> <out.glb> [--taubin 14] [--face_keep 0.25] [--faces 40000] [--report out.json]
Steps: largest connected component (floaters off) -> drop non-manifold / duplicate / unreferenced -> close holes up to 40 boundary edges (the neck cut stays open)
-> Taubin smoothing (volume-preserving) of the dense mesh, blended per vertex: weight 1 on the skull (behind the ear plane, or above the brow line) where the faceted back and the
crown scratch lines are, weight --face_keep on the face/ear/neck-front so nose, lips, lids and ears keep their detail -> quadric decimation to --faces -> glb in the same frame/units as
mv_silhouette_iou.py --export_glb (Y up, face +Z, height 2), which `hunyuan.pinned_glb` and the pipeline's align stage (similarity to the template frame) take unchanged.
The report states the high-frequency roughness (mm at a nominal 270 mm bust height) before/after and the largest moved distance (a smoothing must not shift the shape)."""
import sys, os, json, argparse, numpy as np
from scipy.spatial import cKDTree


def load(path):
    if path.endswith('.npz'):
        d = np.load(path); return d['v'].astype(np.float64), d['f'].astype(np.int64)
    import trimesh
    m = trimesh.load(path, force='mesh'); return np.asarray(m.vertices, np.float64), np.asarray(m.faces, np.int64)


def roughness(v, f, mm_per_unit):
    """std of the offset between each vertex and the mean of its neighbours projected on the normal (about 1-2 mm scale facets and scratches), in mm"""
    import scipy.sparse as sp
    n = len(v); e = np.concatenate([f[:, [0, 1]], f[:, [1, 2]], f[:, [2, 0]]]); A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(n, n)).tocsr(); A = ((A + A.T) > 0).astype(float)
    deg = np.asarray(A.sum(1)).ravel(); m = (A @ v) / np.maximum(deg, 1)[:, None]; import trimesh
    nr = trimesh.Trimesh(v, f, process=False).vertex_normals; off = ((v - m) * nr).sum(1); return float(off[deg > 0].std() * mm_per_unit)


def main():
    ap = argparse.ArgumentParser(); ap.add_argument('inp'); ap.add_argument('out'); ap.add_argument('--taubin', type=int, default=14); ap.add_argument('--face_keep', type=float, default=0.25); ap.add_argument('--faces', type=int, default=40000); ap.add_argument('--voxel', type=float, default=0.0025, help='head-frame voxel pitch of the solid re-extraction (0 = skip): closes cracks, removes the thin double-sheet scratches, rounds the terraces; ~1.3 mm in template units'); ap.add_argument('--report', default=None)
    a = ap.parse_args(); v, f = load(a.inp); import pymeshlab, trimesh
    m = trimesh.Trimesh(v, f, process=False); parts = m.split(only_watertight=False); m = max(parts, key=lambda q: len(q.faces)); v, f = np.asarray(m.vertices), np.asarray(m.faces)
    ms = pymeshlab.MeshSet(); ms.add_mesh(pymeshlab.Mesh(v, f.astype(np.int32)))
    ms.meshing_remove_duplicate_vertices(); ms.meshing_remove_unreferenced_vertices(); ms.meshing_repair_non_manifold_edges(); ms.meshing_close_holes(maxholesize=40, newfaceselected=False)
    mm = ms.current_mesh(); v0, f0 = mm.vertex_matrix().copy(), mm.face_matrix().copy(); H = v0[:, 2].max() - v0[:, 2].min(); MM = 270.0 / H     # nominal: the bust is ~270 mm tall in template units (crown to neck cut); only used to report mm
    rough0 = roughness(v0, f0, MM)
    if a.voxel > 0:   # solidify: surface voxels -> closing (cracks) -> fill (needs a closed shell: the neck cut is capped first) -> 1 opening (sheets thinner than ~2 voxels) -> gaussian -> marching cubes
        from scipy import ndimage as ndi; from skimage import measure
        mc = ms.current_mesh(); vv, ff = mc.vertex_matrix().copy(), mc.face_matrix().copy(); tm = trimesh.Trimesh(vv, ff, process=False)
        from trimesh.voxel import creation as vc_; vg = vc_.voxelize(tm, a.voxel, method='subdivide'); pts = vg.points; org = pts.min(0) - 6 * a.voxel; ijk = np.round((pts - org) / a.voxel).astype(int); shp = ijk.max(0) + 7
        occ = np.zeros(shp, bool); occ[ijk[:, 0], ijk[:, 1], ijk[:, 2]] = True
        st = ndi.generate_binary_structure(3, 1); occ = ndi.binary_closing(occ, st, iterations=3)
        for k in range(8): occ[:, :, 6 + k] = ndi.binary_fill_holes(occ[:, :, 6 + k]) | occ[:, :, 6 + k]       # cap the open neck cut (a closed ring in the lowest slices)
        sol = ndi.binary_fill_holes(occ); sol = ndi.binary_opening(sol, st, iterations=1)
        lab, n = ndi.label(sol); sol = lab == (1 + int(np.argmax(ndi.sum(sol, lab, range(1, n + 1)))))
        fl = ndi.gaussian_filter(sol.astype(np.float32), 1.5); vs_, fs_, _, _ = measure.marching_cubes(fl, 0.5); vs_ = vs_ * a.voxel + org
        ms = pymeshlab.MeshSet(); ms.add_mesh(pymeshlab.Mesh(vs_.astype(np.float64), fs_[:, ::-1].astype(np.int32)))   # marching_cubes winding -> outward
        d0 = cKDTree(vv).query(vs_)[0]; print('voxel re-extraction: %d verts, distance to the capped original: mean %.2f p95 %.2f max %.2f (template mm)' % (len(vs_), d0.mean() * 270 / (vv[:, 2].max() - vv[:, 2].min()), np.percentile(d0, 95) * 270 / (vv[:, 2].max() - vv[:, 2].min()), d0.max() * 270 / (vv[:, 2].max() - vv[:, 2].min())))
    mm = ms.current_mesh(); v0, f0 = mm.vertex_matrix().copy(), mm.face_matrix().copy(); H = v0[:, 2].max() - v0[:, 2].min()
    ms.apply_coord_taubin_smoothing(lambda_=0.5, mu=-0.53, stepsmoothnum=a.taubin); vs = ms.current_mesh().vertex_matrix().copy()
    # blend weights: skull = behind the ear plane (y beyond the head centre line + 6 % H) or above the brow line (top 28 % of the bust height measured from the crown); face/ear/front-neck keep detail
    top = v0[:, 2].max(); ys = v0[:, 1]; yc = np.percentile(ys[v0[:, 2] > top - 0.35 * H], 50)         # face looks to -y: median y of the head slab
    skull = (ys > yc + 0.02 * H) | (v0[:, 2] > top - 0.12 * H)
    w = np.where(skull, 1.0, a.face_keep)
    import scipy.sparse as sp
    e = np.concatenate([f0[:, [0, 1]], f0[:, [1, 2]], f0[:, [2, 0]]]); A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(v0),) * 2).tocsr(); A = ((A + A.T) > 0).astype(float); deg = np.asarray(A.sum(1)).ravel()
    for _ in range(40): w = 0.5 * w + 0.5 * (A @ w) / np.maximum(deg, 1)          # soft transition between the two zones
    vc = v0 + w[:, None] * (vs - v0)
    ms2 = pymeshlab.MeshSet(); ms2.add_mesh(pymeshlab.Mesh(vc, f0)); rough1 = roughness(vc, f0, MM); moved = np.linalg.norm(vc - v0, axis=1) * MM
    ms2.meshing_decimation_quadric_edge_collapse(targetfacenum=a.faces, preservenormal=True); mo = ms2.current_mesh(); V = mo.vertex_matrix(); F = mo.face_matrix()
    V = V / (V[:, 2].max() - V[:, 2].min()) * 2.0; g = np.stack([V[:, 0], V[:, 2], -V[:, 1]], 1); trimesh.Trimesh(g, F, process=False).export(a.out)
    rep = dict(faces_in=int(len(f)), faces_out=int(len(F)), roughness_mm_before=round(rough0, 3), roughness_mm_after=round(rough1, 3), moved_mm_mean=round(float(moved.mean()), 3), moved_mm_max=round(float(moved.max()), 3),
               skull_vertex_share=round(float((w > 0.6).mean()), 3), taubin=a.taubin, face_keep=a.face_keep)
    print(json.dumps(rep)); np.savez(os.path.splitext(a.out)[0] + '_headframe.npz', v=vc.astype(np.float32), f=f0.astype(np.int32))
    if a.report: json.dump(rep, open(a.report, 'w'), indent=1)


if __name__ == '__main__': main()
