# HairDeck cards -> finer strand ribbons (run with the HairDeck venv: C:/AI/hairdeck/.venv/Scripts/python.exe).
#   python hd_strands.py <cards_seedN.json> <out.npz> [--per 0.0062] [--kmin 2] [--kmax 5] [--core 0.8] [--seed 3]
# HairDeck generates ~90-150 wide cards per hairstyle (its set diffusion fixes the count), which read as clumps. Each card (centreline xyz,
# width, thickness, root width direction; frames by Bishop transport, as hairdeck.cards.mesh) is rebuilt as:
#   * a core ribbon, `core` x the card width, darker, slightly toward the head (keeps the volume, hides the scalp between strands);
#   * K = clip(mean width / per, kmin, kmax) strand ribbons spread across the width, each with its own length (ragged tips), a small
#     offset off the card plane (layering), a fan-out toward the tip, and a pointed taper.
# Output (HairDeck's head frame, y up, face +z, metres): v (N,3), f (M,3), uv (N,2: u across 0..1, v = card arc fraction root..tip),
# col (N,3 sRGB from the card's 16 colours along it x per-strand brightness), strand id per vertex. hd_fit.py reads it like a GLB.
import sys, os, json, argparse, numpy as np
sys.path.insert(0, 'C:/AI/hairdeck')
from hairdeck.cards.frames import bishop, tangents   # noqa: E402
ap = argparse.ArgumentParser(); ap.add_argument('cards'); ap.add_argument('out')
ap.add_argument('--per', type=float, default=0.0062); ap.add_argument('--kmin', type=int, default=2); ap.add_argument('--kmax', type=int, default=5)
ap.add_argument('--core', type=float, default=0.8); ap.add_argument('--seed', type=int, default=3)
ap.add_argument('--side_ext', type=float, default=0.0, help='0..1: side cards in front of the ears hanging short of --ext_target grow toward it (face-framing locks)')
ap.add_argument('--ext_target', type=float, default=0.345, help='HairDeck-frame height the face-framing locks grow toward (~jaw)')
a = ap.parse_args()
rng = np.random.default_rng(a.seed); HEAD_C = np.array([0.0, 0.422, 0.0])        # HairDeck head-proxy centre
cards = json.load(open(a.cards)); V, F, UV, COL, SID = [], [], [], [], []
def resample(P, W, Tk, C, s, n):
    """curve sampled at n arc fractions in [0, s] (linear in the control points)."""
    d = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]); d /= max(d[-1], 1e-9)
    q = np.linspace(0, s, n)
    return (np.stack([np.interp(q, d, P[:, k]) for k in range(3)], 1), np.interp(q, d, W), np.interp(q, d, Tk), q)
def ribbon(P, N, B, w, off_n, off_b, u0, u1, vv, col, sid):
    base = len(V) and sum(len(x) for x in V)
    L = P + N * (off_n - w / 2)[:, None] + B * off_b[:, None]; R = P + N * (off_n + w / 2)[:, None] + B * off_b[:, None]
    vs = np.empty((2 * len(P), 3)); vs[0::2] = L; vs[1::2] = R
    uv = np.empty((2 * len(P), 2)); uv[0::2, 0] = u0; uv[1::2, 0] = u1; uv[0::2, 1] = vv; uv[1::2, 1] = vv
    cs = np.repeat(col, 2, 0)
    f = []
    for i in range(len(P) - 1):
        k = base + 2 * i; f += [[k, k + 1, k + 3], [k, k + 3, k + 2]]
    V.append(vs); UV.append(uv); COL.append(cs); F.append(np.array(f)); SID.append(np.full(len(vs), sid))
nstr = 0; nlong = 0
for ci, c in enumerate(cards):
    S = np.array(c['seq'], float); P0, W0, T0 = S[:, :3], S[:, 3], S[:, 4]
    if len(P0) < 3: continue
    if a.side_ext > 0:   # face-framing locks: tip at the side (|x| > 0.04 HairDeck units) in front of the ear centre (z > 0), hanging down
        r0, tp = P0[0], P0[-1]; hang = (tp[1] - r0[1]) < -0.01
        short = tp[1] - a.ext_target                                                    # how far above jaw height the tip ends
        if abs(tp[0]) > 0.04 and tp[2] > 0.0 and hang and short > 0.004:
            Lx = short * a.side_ext * 1.3; m = max(2, int(round(Lx / 0.006)))
            step = Lx / m; T = P0[-1] - P0[-2]; T /= np.linalg.norm(T) + 1e-9; nlong += 1
            out = np.array([np.sign(r0[0]), 0.0, 0.0]); ext = [P0[-1]]
            for k in range(m):
                T = 0.7 * T + 0.3 * np.array([0.0, -1.0, 0.0]) + 0.03 * out; T /= np.linalg.norm(T)      # bend toward hanging straight down
                ext.append(ext[-1] + T * step)
            ext = np.array(ext[1:]); wk = np.linspace(1.0, 0.8, m + 1)[1:]
            P0 = np.vstack([P0, ext]); W0 = np.r_[W0, W0[-1] * wk]; T0 = np.r_[T0, T0[-1] * wk]
            S = np.vstack([S, np.c_[ext, W0[-m:], T0[-m:], np.repeat(S[-1:, 5:8], m, 0)]])
    cols = np.array(c.get('colors', [[0.3, 0.22, 0.18]] * 16), float)
    n = max(8, min(28, len(P0) * 2))
    P, W, Tk, q = resample(P0, W0, T0, None, 1.0, n)
    Tg = tangents(P); Nw = bishop(S[0, 5:8], Tg)
    Bn = np.cross(Nw, Tg); Bn /= np.linalg.norm(Bn, axis=1, keepdims=True) + 1e-12
    out = np.sign(((P - HEAD_C) * Bn).sum(1).mean()) or 1.0; Bn *= out                  # b points away from the head
    colq = lambda qq: np.stack([np.interp(qq, (np.arange(len(cols)) + 0.5) / len(cols), cols[:, k]) for k in range(3)], 1)
    # core: narrower, darker, a little toward the head
    taper = 1 - 0.85 * q ** 2.2
    ribbon(P, Nw, Bn, W * a.core * taper, np.zeros(n), -0.45 * Tk, 0.0, 1.0, q, colq(q) * 0.85, nstr); nstr += 1
    K = int(np.clip(round(W.mean() / a.per), a.kmin, a.kmax))
    jlong = int(np.argmin([abs(-0.5 + (j + 0.5) / K) for j in range(K)]))   # the centre strand keeps the card's full length (spiky outline)
    for j in range(K):
        x = (-0.5 + (j + 0.5) / K) + rng.uniform(-0.18, 0.18) / K                       # position across the card (-0.5..0.5)
        edge = abs(x) * 2                                                              # 0 centre .. 1 edge
        s_len = 1.0 if j == jlong else np.clip(rng.uniform(0.70, 1.0) - 0.12 * edge * rng.random(), 0.45, 1.0)  # outer strands end earlier
        nn = max(6, int(round(n * s_len)))
        Ps, Ws, Ts, qs = resample(P0, W0, T0, None, s_len, nn)
        Tgs = tangents(Ps); Ns = bishop(S[0, 5:8], Tgs); Bs = np.cross(Ns, Tgs); Bs /= np.linalg.norm(Bs, axis=1, keepdims=True) + 1e-12
        Bs *= np.sign(((Ps - HEAD_C) * Bs).sum(1).mean()) or 1.0
        t = qs / max(s_len, 1e-6)                                                      # 0 root .. 1 this strand's tip
        fan = rng.uniform(-0.35, 0.35) * Ws * t ** 1.6                                 # drift across toward the tip
        off_n = x * Ws * 0.95 + fan
        layer = rng.uniform(-0.2, 1.0) * Ts + 0.0015 * t * rng.random()               # mostly outside the card plane
        ws = Ws / K * rng.uniform(1.45, 1.95) * (1 - 0.88 * t ** 2.2)                    # overlap, pointed tip
        br = rng.uniform(0.86, 1.14)
        ribbon(Ps, Ns, Bs, ws, off_n, layer, 0.0, 1.0, qs, np.clip(colq(qs) * br, 0, 1), nstr); nstr += 1
V = np.concatenate(V); F = np.concatenate(F); UV = np.concatenate(UV); COL = np.concatenate(COL); SID = np.concatenate(SID)
np.savez(a.out, v=V.astype(np.float32), f=F.astype(np.int32), uv=UV.astype(np.float32), col=COL.astype(np.float32), sid=SID.astype(np.int32))
print('STRANDS cards %d ribbons %d verts %d tris %d lengthened %d' % (len(cards), nstr, len(V), len(F), nlong))
