"""Build the face-spike template data from the raw export (export_template.py).
Run with trellis2 venv python (numpy/scipy/PIL/matplotlib):
  python build_template.py <raw.npz> <outdir> <render_dir>
Coordinates: glTF imported into Blender, Z up, face looks toward -Y, character-left = +X, metres,
head mesh-local == world (identity matrix).
All indices are RAW AvatarHead vertex indices (7321). Never merge vertices.
"""
import sys, json, collections, os
import numpy as np
import scipy.sparse as sp, scipy.sparse.csgraph as cg
from scipy.spatial import cKDTree


def contains_points(poly, P):
    x, y = P[:, 0], P[:, 1]; inside = np.zeros(len(P), bool)
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]; x2, y2 = poly[(i + 1) % n]
        inside ^= ((y1 > y) != (y2 > y)) & (x < (x2 - x1) * (y - y1) / (y2 - y1 + 1e-30) + x1)
    return inside
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from occlusion import occluded_along

raw, outdir, rdir = sys.argv[1:4]
d = np.load(raw, allow_pickle=False)
V = d["head/basis"].astype(np.float64); F = d["head/faces"]; N = len(V)
K = d["head/keys"].astype(np.float64); names = [str(x) for x in d["head/key_names"]]
KD = lambda n: np.linalg.norm(K[names.index(n)] - K[0], axis=1)
out = {}
R = {}

# ---- 2. key classification
i0, i1 = names.index("General+"), names.index("Nose_Wings_Up")
a0, a1 = names.index("browDownLeft"), names.index("noseSneerRight")
v0 = names.index("CH")
slider = names[i0:i1 + 1]; arkit = names[a0:a1 + 1]; visemes = names[v0:]
assert i0 == 1 and i1 + 1 == a0 and a1 + 1 == v0
out["key_slider_idx"] = np.arange(i0, i1 + 1); out["key_arkit_idx"] = np.arange(a0, a1 + 1); out["key_viseme_idx"] = np.arange(v0, len(names))
out["key_slider_names"] = np.array(slider); out["key_arkit_names"] = np.array(arkit); out["key_viseme_names"] = np.array(visemes)
bn = [str(x) for x in d["body/key_names"]]
R["slider_count"] = len(slider); R["arkit_count"] = len(arkit); R["viseme_count"] = len(visemes)
R["head_body_key_overlap"] = [n for n in names[1:] if n in bn]
R["body_only_keys"] = [n for n in bn[1:] if n not in names]
R["neck_shoulder_key_head_motion"] = {n: {"max_mm": float(KD(n).max() * 1000), "nverts_moved": int((KD(n) > 1e-6).sum())} for n in ["General+", "General-", "Neck+", "Neck-", "Shoulders+", "Shoulders-"]}
bk = d["body/keys"]; bb = d["body/basis"]
R["neck_shoulder_key_body_motion"] = {n: {"max_mm": float(np.linalg.norm(bk[bn.index(n)] - bb, axis=1).max() * 1000)} for n in ["General+", "General-", "Shoulders+", "Shoulders-"]}
R["neck_key_zrange_head"] = {}
for n in ["General+", "Neck+", "Shoulders+"]:
    m = KD(n) > 1e-6
    R["neck_key_zrange_head"][n] = [float(V[m, 2].min()), float(V[m, 2].max())]

# ---- 3. coincident groups
pairs = cKDTree(V).query_pairs(1e-6, output_type="ndarray")
par = list(range(N))
def fd(x):
    while par[x] != x:
        par[x] = par[par[x]]; x = par[x]
    return x
for a, b in pairs: par[fd(a)] = fd(b)
rep = np.array([fd(i) for i in range(N)])
grp = collections.defaultdict(list)
for i in range(N): grp[rep[i]].append(i)
groups = sorted(sorted(g) for g in grp.values() if len(g) > 1)
out["split_groups_flat"] = np.array([i for g in groups for i in g], np.int32)
out["split_groups_id"] = np.array([k for k, g in enumerate(groups) for _ in g], np.int32)
out["weld_rep"] = rep.astype(np.int32)
R["coincident_groups"] = len(groups); R["coincident_verts"] = int(len(out["split_groups_flat"]))
R["group_sizes"] = dict(collections.Counter(map(len, groups))); R["unique_positions"] = len(grp)
R["max_key_divergence_in_groups_mm"] = float(max(np.abs(K[:, g[0]] - K[:, j]).max() for g in groups for j in g[1:]) * 1000)

# raw face components (mouth interior pieces are separate raw components)
em = collections.defaultdict(list)
for fi, f in enumerate(F):
    for a, b in ((0, 1), (1, 2), (2, 0)): em[(min(f[a], f[b]), max(f[a], f[b]))].append(fi)
adj = collections.defaultdict(list)
for e, fs in em.items():
    if len(fs) == 2: adj[fs[0]].append(fs[1]); adj[fs[1]].append(fs[0])
comp = -np.ones(len(F), int); sizes = []
for s in range(len(F)):
    if comp[s] >= 0: continue
    c = len(sizes); comp[s] = c; st = [s]; n = 1
    while st:
        x = st.pop()
        for y in adj[x]:
            if comp[y] < 0: comp[y] = c; st.append(y); n += 1
    sizes.append(n)
R["raw_face_components"] = sizes
vcomp = -np.ones(N, int)
for c in range(len(sizes)):
    vs = np.unique(F[comp == c]); vcomp[vs] = c
main_c = int(np.argmax(sizes))
in_mouth_comp = (vcomp >= 0) & (vcomp != main_c)
mcomps = [c for c in range(len(sizes)) if c != main_c]
cz = {c: V[vcomp == c, 2].mean() for c in mcomps}
c_up = max(mcomps, key=lambda c: cz[c]); c_lo = min(mcomps, key=lambda c: cz[c])
seam_up, seam_lo, rim_main, rim_patch, rim_up_g, rim_lo_g = [], [], [], [], [], []
for g in groups:
    cs = [vcomp[i] for i in g]
    if c_up in cs and c_lo in cs:
        seam_up += [i for i in g if vcomp[i] == c_up]; seam_lo += [i for i in g if vcomp[i] == c_lo]
    elif main_c in cs and any(c in mcomps for c in cs):
        rim_main += [i for i in g if vcomp[i] == main_c]; rim_patch += [i for i in g if vcomp[i] != main_c]
        (rim_up_g if c_up in cs else rim_lo_g).extend(g)
seam_up = np.array(sorted(seam_up), np.int32); seam_lo = np.array(sorted(seam_lo), np.int32)
out["lip_seam_upper"] = seam_up; out["lip_seam_lower"] = seam_lo   # the lip line: coincident upper/lower copies
out["lip_seam_all"] = np.sort(np.concatenate([seam_up, seam_lo])).astype(np.int32)
out["lip_patch_rim_main"] = np.array(sorted(rim_main), np.int32); out["lip_patch_rim_patch"] = np.array(sorted(rim_patch), np.int32)
out["lip_patch_upper_verts"] = np.where(vcomp == c_up)[0].astype(np.int32); out["lip_patch_lower_verts"] = np.where(vcomp == c_lo)[0].astype(np.int32)
jaw = KD("jawOpen")
hid_front = occluded_along(V, F, 1, -1)
mouth_interior = np.where(in_mouth_comp & hid_front & (V[:, 1] > -0.10) & ~np.isin(np.arange(N), out["lip_seam_all"]))[0].astype(np.int32)
out["mouth_interior"] = mouth_interior
sa = out["lip_seam_all"]
# seam is a closed ellipse: front arc (visible lip line, ends at the mouth corners ~|x|=0.0256 m) + sides/back inside the cheeks.
# threshold on y picks the visible lip line; vertex y < -0.111 m
out["lip_seam_front"] = sa[V[sa, 1] < -0.111]; out["lip_seam_back"] = sa[V[sa, 1] >= -0.111]
lip_int = out["lip_patch_rim_patch"]
# visible lip line = patch rims (both copies) + the front end of the seam arc; corners are its |x| extremes (~0.0256 m)
lo = np.unique(np.concatenate([out["lip_patch_rim_main"], out["lip_patch_rim_patch"], out["lip_seam_front"]])).astype(np.int32)
out["lip_line"] = lo
out["lip_line_upper"] = np.unique(np.array(rim_up_g, np.int32)); out["lip_line_lower"] = np.unique(np.array(rim_lo_g, np.int32))
R["lip_ring"] = {"seam_upper": int(len(seam_up)), "seam_lower": int(len(seam_lo)), "patch_rim_main": int(len(rim_main)), "patch_rim_patch": int(len(rim_patch)),
                 "patch_verts_upper_lower": [int(len(out["lip_patch_upper_verts"])), int(len(out["lip_patch_lower_verts"]))], "mouth_interior_verts": int(len(mouth_interior)),
                 "seam_front_n": int(len(lo)), "seam_back_n": int(len(out["lip_seam_back"])), "seam_x": [float(V[lo, 0].min()), float(V[lo, 0].max())], "seam_z": [float(V[lo, 2].min()), float(V[lo, 2].max())],
                 "upper_copy_jaw_disp_mm_max": float(jaw[seam_up].max() * 1000), "lower_copy_jaw_disp_mm_min": float(jaw[seam_lo].min() * 1000),
                 "lower_copy_jaw_disp_mm_mean": float(jaw[seam_lo].mean() * 1000), "upper_copy_jaw_disp_mm_mean": float(jaw[seam_up].mean() * 1000)}

# welded boundary loops
E = collections.Counter()
for f in F:
    r = rep[f]
    for a, b in ((0, 1), (1, 2), (2, 0)): E[(min(r[a], r[b]), max(r[a], r[b]))] += 1
badj = collections.defaultdict(list)
for (a, b), c in E.items():
    if c == 1: badj[a].append(b); badj[b].append(a)
seen = set(); loops = []
for s in badj:
    if s in seen: continue
    loop = [s]; seen.add(s); prev = None; cur = s
    while True:
        nx = [y for y in badj[cur] if y != prev and y not in seen]
        if not nx: break
        prev = cur; cur = nx[0]; loop.append(cur); seen.add(cur)
    loops.append(loop)
raw_of = lambda loop: np.array(sorted(i for r in loop for i in grp[r]), np.int32)
R["boundary_loops"] = [{"n_welded": len(l), "centroid": V[l].mean(0).round(4).tolist(), "z": [float(V[l, 2].min()), float(V[l, 2].max())]} for l in loops]
neck = [l for l in loops if V[l, 2].mean() < 1.6][0]
eyes = [l for l in loops if V[l, 2].mean() > 1.6]
eyeL_l = [l for l in eyes if V[l, 0].mean() > 0][0]; eyeR_l = [l for l in eyes if V[l, 0].mean() < 0][0]
out["neck_ring_ordered_welded_rep"] = np.array(neck, np.int32); out["neck_ring_raw"] = raw_of(neck)
out["eyelid_ring_L_ordered_welded_rep"] = np.array(eyeL_l, np.int32); out["eyelid_ring_R_ordered_welded_rep"] = np.array(eyeR_l, np.int32)
out["eyelid_ring_L"] = raw_of(eyeL_l); out["eyelid_ring_R"] = raw_of(eyeR_l)
for s in "LR":
    r = out["eyelid_ring_" + s]; zm = V[r, 2].mean()
    out[f"eyelid_ring_{s}_upper"] = r[V[r, 2] > zm]; out[f"eyelid_ring_{s}_lower"] = r[V[r, 2] <= zm]
bL = KD("eyeBlinkLeft"); bR = KD("eyeBlinkRight")
R["eyelid_ring"] = {"L_raw": int(len(out["eyelid_ring_L"])), "R_raw": int(len(out["eyelid_ring_R"])),
                    "blinkL_ring_mean_mm": float(bL[out["eyelid_ring_L"]].mean() * 1000), "blinkL_upper_mean_mm": float(bL[out["eyelid_ring_L_upper"]].mean() * 1000),
                    "blinkL_lower_mean_mm": float(bL[out["eyelid_ring_L_lower"]].mean() * 1000), "blinkL_global_max_mm": float(bL.max() * 1000),
                    "blinkR_ring_mean_mm": float(bR[out["eyelid_ring_R"]].mean() * 1000)}
R["neck_ring"] = {"n_welded": len(neck), "n_raw": int(len(out["neck_ring_raw"])), "z": [float(V[neck, 2].min()), float(V[neck, 2].max())]}

# ---- regions
Fn = np.cross(V[F[:, 1]] - V[F[:, 0]], V[F[:, 2]] - V[F[:, 0]]); Fn /= np.linalg.norm(Fn, axis=1)[:, None] + 1e-20
Vn = np.zeros_like(V)
for k in range(3): np.add.at(Vn, F[:, k], Fn)
Vn /= np.linalg.norm(Vn, axis=1)[:, None] + 1e-20
ee = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]]); we = rep[ee]
G = sp.coo_matrix((np.ones(len(we)), (we[:, 0], we[:, 1])), shape=(N, N)).tocsr(); G = G + G.T
def hops(srcs, lim):
    return cg.dijkstra(G, directed=False, indices=np.unique(rep[srcs]), unweighted=True, limit=lim, min_only=True)[rep]
eye_socket = np.where((hops(out["eyelid_ring_L"], 3) <= 2) | (hops(out["eyelid_ring_R"], 3) <= 2))[0].astype(np.int32)
out["eye_socket"] = eye_socket
nb = np.where((np.abs(V[:, 0]) > 0.002) & (np.abs(V[:, 0]) < 0.0125) & (V[:, 1] > -0.142) & (V[:, 1] < -0.118) & (V[:, 2] > 1.650) & (V[:, 2] < 1.676) & (Vn[:, 2] > -0.25))[0].astype(np.int32)
out["nostril"] = nb
ear_mask = (np.abs(V[:, 0]) > 0.074) & (V[:, 1] > -0.075) & (V[:, 1] < 0.008) & (V[:, 2] > 1.645) & (V[:, 2] < 1.745)
out["ear_L"] = np.where(ear_mask & (V[:, 0] > 0))[0].astype(np.int32); out["ear_R"] = np.where(ear_mask & (V[:, 0] < 0))[0].astype(np.int32)

# ---- 5. landmarks
vis = ~hid_front & ~in_mouth_comp
LM = {}
def sel(mask, key, largest=False):
    ii = np.where(mask)[0]
    assert len(ii), "empty mask"
    return int(ii[np.argmax(key[ii])] if largest else ii[np.argmin(key[ii])])
midline = (np.abs(V[:, 0]) < 0.0016) & (V[:, 1] < -0.04) & ~in_mouth_comp
for s, sx in (("L", 1), ("R", -1)):
    ring = out["eyelid_ring_" + s]
    LM[f"eye_inner_{s}"] = int(ring[np.argmin(V[ring, 0] * sx)]); LM[f"eye_outer_{s}"] = int(ring[np.argmax(V[ring, 0] * sx)])
zc = float(np.mean([V[LM["eye_inner_L"], 2], V[LM["eye_inner_R"], 2]]))
LM["nose_tip"] = sel(midline & (V[:, 2] > 1.64) & (V[:, 2] < 1.72), V[:, 1])
LM["sellion"] = sel(midline & (V[:, 2] > zc - 0.012) & (V[:, 2] < zc + 0.012), V[:, 1], True)
LM["glabella"] = sel(midline & (V[:, 2] > zc + 0.008) & (V[:, 2] < zc + 0.04), V[:, 1])
LM["brow_center"] = sel(midline & (V[:, 2] > zc + 0.03) & (V[:, 2] < zc + 0.06), V[:, 1])
LM["forehead_top"] = sel(midline & (V[:, 1] < -0.05), np.abs(V[:, 2] - (zc + 0.085)))
tipz = V[LM["nose_tip"], 2]
LM["subnasale"] = sel(midline & (V[:, 2] > 1.655) & (V[:, 2] < tipz - 0.004), V[:, 1], True)
for s, sx in (("L", 1), ("R", -1)):
    m = (V[:, 0] * sx > 0.008) & (V[:, 0] * sx < 0.03) & (V[:, 1] < -0.105) & (V[:, 2] > 1.655) & (V[:, 2] < 1.70) & ~in_mouth_comp
    LM[f"nostril_wing_{s}"] = sel(m, V[:, 0] * sx, True)
    LM[f"mouth_corner_{s}"] = int(lo[np.argmax(V[lo, 0] * sx)])
lipz = float(V[lo, 2].mean())
LM["lip_seam_center"] = int(lo[np.argmin(np.abs(V[lo, 0]) + 0.01 * (V[lo, 1] > -0.12))])
LM["upper_lip_center"] = sel(midline & vis & (V[:, 2] > lipz + 0.002) & (V[:, 2] < lipz + 0.022), V[:, 1])
LM["lower_lip_center"] = sel(midline & vis & (V[:, 2] < lipz - 0.002) & (V[:, 2] > lipz - 0.025), V[:, 1])
llz = V[LM["lower_lip_center"], 2]
LM["chin_lip_fold"] = sel(midline & vis & (V[:, 2] < llz - 0.004) & (V[:, 2] > llz - 0.03), V[:, 1], True)
cfz = V[LM["chin_lip_fold"], 2]
LM["chin_tip"] = sel(midline & vis & (V[:, 2] < cfz - 0.004) & (V[:, 2] > cfz - 0.05), V[:, 1])
for s, sx in (("L", 1), ("R", -1)):
    LM[f"temple_{s}"] = sel((V[:, 0] * sx > 0.05) & (np.abs(V[:, 2] - (zc + 0.03)) < 0.004) & (V[:, 1] < -0.05) & vis, V[:, 0] * sx, True)
    LM[f"cheekbone_{s}"] = sel((V[:, 0] * sx > 0.045) & (V[:, 0] * sx < 0.08) & (V[:, 2] > zc - 0.05) & (V[:, 2] < zc - 0.012) & (V[:, 1] < -0.06) & vis, V[:, 1])
    LM[f"jaw_angle_{s}"] = sel((V[:, 0] * sx > 0.04) & (V[:, 1] > -0.09) & (V[:, 1] < 0.0) & (V[:, 2] > 1.575) & (V[:, 2] < 1.635), V[:, 0] * sx, True)
    ear = out["ear_" + s]
    et = ear[np.abs(V[ear, 0]) >= 0.082]; LM[f"ear_top_{s}"] = int(et[np.argmax(V[et, 2])]); LM[f"ear_lobe_{s}"] = int(ear[np.argmin(V[ear, 2])])
for k in LM: LM[k] = int(min(grp[rep[LM[k]]]))   # canonical raw index = lowest of coincident group
lm = {k: {"index": v, "pos": V[v].round(5).tolist(), "split_group": [int(i) for i in grp[rep[v]]]} for k, v in LM.items()}
R["n_landmarks"] = len(LM)
out["landmark_names"] = np.array(list(LM.keys())); out["landmark_idx"] = np.array(list(LM.values()), np.int32)

# ---- 4. weights
fz = V[LM["forehead_top"], 2]; cz_ = V[LM["chin_tip"], 2]; tz = V[LM["temple_L"], 2]
tx = abs(V[LM["temple_L"], 0])
half = [(0.0, fz), (tx, tz), (0.080, 1.68), (0.070, 1.612), (0.045, cz_ - 0.004), (0.0, cz_ - 0.004)]
poly = np.array([(-x, z) for x, z in half] + [(x, z) for x, z in reversed(half)])  # hexagon-ish face outline, front view (x,z)
facefront = contains_points(poly, V[:, [0, 2]]) & (V[:, 1] < -0.04) & (Vn[:, 1] < 0.35)
w = np.full(N, 0.3)
w[V[:, 1] > 0.0] = 0.1
w[(V[:, 1] > -0.03) & (V[:, 2] > 1.62)] = 0.1
w[facefront] = 1.0
for k in ("ear_L", "ear_R"): w[out[k]] = np.minimum(w[out[k]], 0.2)
w[eye_socket] = 0.2; w[nb] = 0.2; w[mouth_interior] = 0.1
w[out["neck_ring_raw"]] = 0.3
out["region_weight"] = w.astype(np.float32)
out["neck_seam_flag"] = np.isin(np.arange(N), out["neck_ring_raw"])
R["weights"] = {"1.0": int((w >= .999).sum()), "0.3": int(np.isclose(w, .3).sum()), "0.2": int(np.isclose(w, .2).sum()), "0.1": int(np.isclose(w, .1).sum())}
R["set_sizes"] = {k: int(len(v)) for k, v in out.items() if v.ndim == 1 and v.dtype.kind in "iu" and not k.startswith(("key_", "split", "weld", "landmark"))}

# ---- save
os.makedirs(outdir, exist_ok=True)
allv = {k: d[k] for k in d.files}
for k, v in out.items(): allv["tpl/" + k] = v
np.savez_compressed(os.path.join(outdir, "tobin_head_template.npz"), **allv)
json.dump({"coordinate_system": "glTF imported into Blender: Z up, face toward -Y, character-left = +X, metres, mesh-local == world (identity)",
           "mesh": "AvatarHead raw vertex indices (7321, never merged); index = lowest raw index of its coincident group (see split_group)", "landmarks": lm},
          open(os.path.join(outdir, "landmarks_template.json"), "w"), indent=1)
json.dump(R, open(os.path.join(outdir, "template_report.json"), "w"), indent=1, default=str)
print(json.dumps(R, indent=1, default=str))
for k, v in lm.items(): print(k, v["index"], v["pos"])

from PIL import Image, ImageDraw
def draw(view, path):
    im = Image.open(os.path.join(rdir, f"tpl_{view}.png")).convert("RGB"); dr = ImageDraw.Draw(im)
    for k, v in LM.items():
        x, y, z = V[v]
        if view == "front": u, vv = 800 + x * 4000, 800 - (z - 1.66) * 4000
        else:
            if x > 0.0016: continue
            u, vv = 800 + (-y - 0.02) * 4000, 800 - (z - 1.66) * 4000
        col = (255, 60, 60) if k.endswith("_L") else (60, 160, 255) if k.endswith("_R") else (255, 230, 0)
        dr.ellipse([u - 5, vv - 5, u + 5, vv + 5], fill=col)
        dr.text((u + 7, vv - 6), k.replace("_L", "L").replace("_R", "R"), fill=(255, 255, 255))
    im.save(path)
draw("front", os.path.join(outdir, "landmarks_front.png")); draw("right", os.path.join(outdir, "landmarks_right.png"))

# weight preview (front view dots coloured by weight)
im = Image.open(os.path.join(rdir, "tpl_front.png")).convert("RGB"); dr = ImageDraw.Draw(im)
for i in range(N):
    if V[i, 1] > 0.0 and w[i] > 0: continue
    u, vv = 800 + V[i, 0] * 4000, 800 - (V[i, 2] - 1.66) * 4000
    c = int(255 * w[i]); col = (255, 255 - c, 0) if w[i] > 0.5 else (0, 120 + int(300 * w[i]), 255)
    dr.point((u, vv), fill=col); dr.point((u + 1, vv), fill=col)
im.save(os.path.join(outdir, "weights_front.png"))
