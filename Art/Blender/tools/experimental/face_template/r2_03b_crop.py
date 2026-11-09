# crop aligned target above zcut (template frame), keep largest connected component -> target_head.npz
import numpy as np, scipy.sparse.csgraph as cg
from r2_common import *
Z = np.load(FIT + 'target_aligned_full.npz'); av, tf, zc = Z['v'], Z['f'], float(Z['zcut'])
keepv = av[:, 2] > zc; fk = keepv[tf].all(1); idx = -np.ones(len(av), int); idx[keepv] = np.arange(keepv.sum())
cv, cf = av[keepv], idx[tf[fk]]
_, wid = np.unique(np.round(cv, 6), axis=0, return_inverse=True); wid = wid.ravel()
e = edges_of(wid[cf]); A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(wid.max() + 1,) * 2)
nc, lab = cg.connected_components(A, directed=False); lab = lab[wid]; big = np.argmax(np.bincount(lab))
kv = lab == big; fk2 = kv[cf].all(1); idx2 = -np.ones(len(cv), int); idx2[kv] = np.arange(kv.sum())
cv, cf = cv[kv], idx2[cf[fk2]]
np.savez(FIT + 'target_head.npz', v=cv, f=cf); print('cropped', cv.shape, cf.shape, cv.min(0), cv.max(0))
