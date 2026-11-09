"""Axis-aligned ray occlusion for mesh vertices, numpy only (no rtree)."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import numpy as np

def occluded_along(V, F, axis, sign, tol=2e-4, chunk=250):
    """True where some triangle lies between the vertex and a viewer at infinity on the
    side `sign` of `axis` (sign=-1: viewer at -axis side, rays travel +axis)."""
    ax = [i for i in range(3) if i != axis]
    A = V[F[:, 0]][:, ax]; B = V[F[:, 1]][:, ax]; C = V[F[:, 2]][:, ax]
    dpt = V[:, axis] * (-sign)  # larger = closer to the viewer? depth measured from viewer: viewer at -axis => depth = coord
    # depth from viewer: smaller = nearer. sign=-1 => depth=coord ; sign=+1 => depth=-coord
    depth = V[:, axis] if sign < 0 else -V[:, axis]
    dF = depth[F]  # (nf,3)
    v0 = B - A; v1 = C - A
    den = v0[:, 0] * v1[:, 1] - v1[:, 0] * v0[:, 1]
    ok = np.abs(den) > 1e-14
    lo = np.minimum(np.minimum(A, B), C); hi = np.maximum(np.maximum(A, B), C)
    out = np.zeros(len(V), bool)
    for s in range(0, len(V), chunk):
        P = V[s:s + chunk][:, ax]
        inb = (P[:, None, 0] >= lo[None, :, 0] - 1e-9) & (P[:, None, 0] <= hi[None, :, 0] + 1e-9) & \
              (P[:, None, 1] >= lo[None, :, 1] - 1e-9) & (P[:, None, 1] <= hi[None, :, 1] + 1e-9)
        ii, tt = np.nonzero(inb)
        p = P[ii]; a = A[tt]
        w = p - a
        d = den[tt]
        u = (w[:, 0] * v1[tt, 1] - v1[tt, 0] * w[:, 1]) / np.where(ok[tt], d, 1)
        v = (v0[tt, 0] * w[:, 1] - w[:, 0] * v0[tt, 1]) / np.where(ok[tt], d, 1)
        inside = ok[tt] & (u >= -1e-9) & (v >= -1e-9) & (u + v <= 1 + 1e-9)
        z = (1 - u - v) * dF[tt, 0] + u * dF[tt, 1] + v * dF[tt, 2]
        near = inside & (z < depth[s + ii] - tol)
        occ = np.zeros(len(P), bool)
        occ[np.unique(ii[near])] = True
        out[s:s + chunk] = occ
    return out
