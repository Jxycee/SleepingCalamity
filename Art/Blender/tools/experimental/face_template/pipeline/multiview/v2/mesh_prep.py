"""Report floaters, keep the largest piece, quadric-decimate. usage: mesh_prep.py <in.glb> <out.glb> [faces=150000]; prints pieces/faces JSON."""
import sys, json, numpy as np, trimesh, pymeshlab
src, dst = sys.argv[1:3]; n = int(sys.argv[3]) if len(sys.argv) > 3 else 150000
m = trimesh.load(src, force='mesh'); parts = m.split(only_watertight=False); big = max(parts, key=lambda p: len(p.faces))
info = dict(faces_in=len(m.faces), pieces=len(parts), largest_faces=len(big.faces), floater_faces=len(m.faces) - len(big.faces))
ms = pymeshlab.MeshSet(); ms.add_mesh(pymeshlab.Mesh(np.asarray(big.vertices, np.float64), np.asarray(big.faces, np.int32)))
if len(big.faces) > n: ms.meshing_decimation_quadric_edge_collapse(targetfacenum=n, preservenormal=True)
mm = ms.current_mesh(); trimesh.Trimesh(mm.vertex_matrix(), mm.face_matrix(), process=False).export(dst); info['faces_out'] = int(mm.face_number())
b = big.bounds; info['extent'] = (b[1] - b[0]).round(3).tolist(); print(json.dumps(info))
