# blender -b --factory-startup -P cards_from_strands.py -- <strands_world.npz> <out.blend> [K=230] [seed=3] [variants=8]
# Strand polylines (N,M,3, world frame of Tobin_Base_v2_sliders.glb, root first) -> tapered, outward-facing hair CARDS (ribbons, 3 verts across, R rings) in object 'Tobin_Hair'.
# Clusters strands by root + tip position; per cluster: mean curve, width from the lateral spread, 2 cards (full + shorter, shifted) for layering; UV into a V-variant strand atlas (hair_atlas.py).
import bpy, bmesh, numpy as np, sys, os, math
a = sys.argv[sys.argv.index('--') + 1:]; src, out = a[0], a[1]; kw = dict(x.split('=') for x in a[2:])
K = int(kw.get('K', 230)); V = int(kw.get('variants', 8)); R = int(kw.get('rings', 9)); rng = np.random.default_rng(int(kw.get('seed', 3))); HC = np.array([0.0, -0.005, 1.70])
Q = np.load(src)['positions'].astype(np.float64)
if len(Q) > 30000: Q = Q[rng.choice(len(Q), 30000, replace=False)]
def resample(P, n):
    d = np.linalg.norm(np.diff(P, axis=0), axis=1); s = np.concatenate([[0], np.cumsum(d)]); L = s[-1]
    if L < 1e-6: return None, 0
    t = np.linspace(0, L, n); return np.stack([np.interp(t, s, P[:, i]) for i in range(3)], 1), L
RS, LEN = [], []
for P in Q:
    d = np.linalg.norm(np.diff(P, axis=0), axis=1); keep = np.concatenate([[True], d > 1e-5]); r, L = resample(P[keep], R)
    if r is not None and L > 0.02: RS.append(r); LEN.append(L)
RS = np.array(RS); LEN = np.array(LEN); print('strands', len(RS), 'mean length mm', LEN.mean() * 1000)
feat = np.concatenate([RS[:, 0], 0.5 * RS[:, -1]], 1); cen = feat[rng.choice(len(feat), K, replace=False)].copy()
for _ in range(20):
    lab = np.concatenate([np.argmin(((feat[i:i + 4000, None, :] - cen[None]) ** 2).sum(2), 1) for i in range(0, len(feat), 4000)])
    for k in range(K):
        m = lab == k
        if m.any(): cen[k] = feat[m].mean(0)
verts, faces, uvs = [], [], []
def sstep(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)
ncards = 0
for k in range(K):
    m = np.where(lab == k)[0]
    if len(m) < 4: continue
    G = RS[m].mean(0); spread = RS[m] - G[None]; T = G[-1] - G[0]; T /= np.linalg.norm(T) + 1e-9
    rad = G[R // 2] - HC; rad /= np.linalg.norm(rad) + 1e-9; wax = np.cross(T, rad); wax /= np.linalg.norm(wax) + 1e-9
    lat = np.abs((spread[:, :R * 2 // 3] @ wax)).mean() * 2.0
    width = float(np.clip(float(kw.get('cover', 1.35)) * (2.4 * lat + 0.008), 0.012, 0.038)); glen = float(LEN[m].mean())
    for ci, (shift, lfac) in enumerate(((0.0, 1.0), (0.32, 0.8))):
        ln = max(4, int(round(R * lfac))); idx = np.linspace(0, R - 1, ln).astype(float)
        C = np.stack([np.interp(idx, np.arange(R), G[:, i]) for i in range(3)], 1)
        var = int(rng.integers(0, V)); base = len(verts)
        tang = np.gradient(C, axis=0); tang /= np.linalg.norm(tang, axis=1)[:, None] + 1e-9
        for i in range(ln):
            t = i / (ln - 1); w = width * (0.82 + 0.18 * t) * (1 - 0.88 * sstep((t - 0.45) / 0.55)) * (1.0 + 0.1 * math.sin(k * 3.7 + i))
            rr = C[i] - HC; rr /= np.linalg.norm(rr) + 1e-9; wx = np.cross(tang[i], rr); wx /= np.linalg.norm(wx) + 1e-9
            ctr = C[i] + wx * shift * width + rr * 0.0015 * (1 + ci)
            for j, (off, bulge) in enumerate(((-0.5, 0.0), (0.0, 0.0016), (0.5, 0.0))):
                verts.append(ctr + wx * off * w + rr * bulge); uvs.append(((var + 0.04 + 0.92 * (off + 0.5)) / V, 1.0 - t))
        for i in range(ln - 1):
            for j in range(2):
                p0, p1, p2, p3 = base + i * 3 + j, base + i * 3 + j + 1, base + (i + 1) * 3 + j + 1, base + (i + 1) * 3 + j
                nrm = np.cross(np.array(verts[p1]) - np.array(verts[p0]), np.array(verts[p3]) - np.array(verts[p0])); rr = np.array(verts[p0]) - HC
                faces.append((p0, p1, p2, p3) if nrm @ rr >= 0 else (p3, p2, p1, p0))
        ncards += 1
print('cards', ncards, 'verts', len(verts), 'quads', len(faces))
bpy.ops.wm.read_factory_settings(use_empty=True)
me = bpy.data.meshes.new('Tobin_Hair'); me.from_pydata([tuple(v) for v in verts], [], faces); me.update()
uv = me.uv_layers.new(name='UVMap')
for p in me.polygons:
    for li in p.loop_indices: uv.data[li].uv = uvs[me.loops[li].vertex_index]
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free(); me.update()
ob = bpy.data.objects.new('Tobin_Hair', me); bpy.context.scene.collection.objects.link(ob)
for p in me.polygons: p.use_smooth = True
bpy.ops.wm.save_as_mainfile(filepath=out); print('CARDS_DONE', len(me.polygons), 'tris')
