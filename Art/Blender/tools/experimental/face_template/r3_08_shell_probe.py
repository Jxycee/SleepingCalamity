"""probe: mouth shell face sets and their boundary loops (welded vertices)"""
from r3_common import *
import scipy.sparse.csgraph as cg
t = Tpl(); rep = t.rep; F = rep[t.faces]
def vset(*names):
    s = np.zeros(len(t.basis), bool)
    for n in names: s[t.S(n)] = True
    return s
mi = vset('mouth_interior'); pu = vset('lip_patch_upper_verts'); pl = vset('lip_patch_lower_verts'); ll = vset('lip_line'); ss = vset('lip_seam_all')
print('sizes mouth_interior', mi.sum(), 'patch up', pu.sum(), 'patch lo', pl.sum(), 'lip_line', ll.sum(), 'seam_all', ss.sum())
def boundary(fmask):
    ff = F[fmask]; e = np.sort(np.concatenate([ff[:, [0, 1]], ff[:, [1, 2]], ff[:, [2, 0]]]), 1)
    u, c = np.unique(e, axis=0, return_counts=True); return u[c == 1], ff
for name, fm in (('mouth_interior faces (all verts in set)', mi[t.faces].all(1)), ('mouth_interior any vert', mi[t.faces].any(1)),
                 ('interior + patches all', (mi | pu | pl)[t.faces].all(1)), ('interior + patches any', (mi | pu | pl)[t.faces].any(1))):
    b, ff = boundary(fm); n = len(np.unique(b))
    A = sp.coo_matrix((np.ones(len(b)), (b[:, 0], b[:, 1])), shape=(F.max() + 1,) * 2); nc, lab = cg.connected_components(A, directed=False)
    used = np.unique(b); loops = len(np.unique(lab[used]))
    print(name, 'faces', fm.sum(), 'boundary edges', len(b), 'boundary verts', n, 'boundary loops', loops)
    z = t.basis[used][:, 2]; print('   boundary z range mm', z.min() * 1e3, z.max() * 1e3, ' on lip_line fraction', np.isin(used, np.unique(rep[np.where(ll)[0]])).mean())
